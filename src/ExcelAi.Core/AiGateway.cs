using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExcelAi
{
    /// <summary>
    /// モデルを呼ぶ入口。同意・API キーの確認、キャッシュ、同時実行数の制限、混み合い・通信エラーのときの再試行、使用量の記録をまとめて行う。
    /// </summary>
    public sealed class AiGateway
    {
        private readonly ILlm llm;
        private readonly Func<AiSettings> settings;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;
        private readonly bool requireConsent;
        private readonly object gate = new object();
        private SemaphoreSlim limiter;
        private int limiterSize;

        public AiGateway(ILlm llm, Func<AiSettings> settings, ResultCache cache, UsageMeter usage, bool requireConsent = true, Func<TimeSpan, CancellationToken, Task>? delay = null)
        {
            this.llm = llm;
            this.settings = settings;
            Cache = cache;
            Usage = usage;
            this.requireConsent = requireConsent;
            this.delay = delay ?? ((span, ct) => Task.Delay(span, ct));
            limiterSize = Math.Max(1, settings().MaxConcurrency);
            limiter = new SemaphoreSlim(limiterSize, limiterSize);
        }

        public ResultCache Cache { get; }

        public UsageMeter Usage { get; }

        private static readonly Lazy<AiGateway> Shared = new Lazy<AiGateway>(() =>
            new AiGateway(new RoutingLlm(() => SettingsStore.Current), () => SettingsStore.Current, new ResultCache(AppPaths.CacheFile), new UsageMeter(AppPaths.UsageFile)));

        /// <summary>アドイン全体で使う入口。</summary>
        public static AiGateway Default => Shared.Value;

        /// <summary>設定の既定値で依頼を作る。</summary>
        public LlmRequest NewRequest(string? system, IReadOnlyList<ChatMessage> messages, string? model = null)
        {
            var s = settings();
            return new LlmRequest
            {
                Model = string.IsNullOrWhiteSpace(model) ? s.Model : model!,
                System = system ?? s.SystemPrompt,
                Messages = messages,
                MaxTokens = s.MaxTokens,
                Effort = s.Effort,
            };
        }

        public async Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken cancel)
        {
            var s = settings();
            Check(s);
            string key = ResultCache.KeyOf(request);
            if (s.UseCache && Cache.TryGet(key, s.CacheDays, out var cached))
            {
                Usage.Record(request.Model, cached);
                return cached;
            }

            var result = await WithRetryAsync(() => llm.CompleteAsync(request, cancel), cancel, request.Model).ConfigureAwait(false);
            Finish(request, result, key, s);
            return result;
        }

        public async Task<LlmResult> StreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken cancel)
        {
            var s = settings();
            Check(s);
            bool started = false;
            var result = await WithRetryAsync(
                () => llm.StreamAsync(request, d => { started = true; onDelta(d); }, cancel),
                cancel,
                request.Model,
                () => !started).ConfigureAwait(false);
            Finish(request, result, string.Empty, s);
            return result;
        }

        private void Check(AiSettings s)
        {
            if (s.EffectiveApiKey.Length == 0)
            {
                throw new AiException(AiErrorKind.NoKey);
            }

            if (requireConsent && !s.ConsentAccepted)
            {
                throw new AiException(AiErrorKind.NoConsent);
            }
        }

        private void Finish(LlmRequest request, LlmResult result, string key, AiSettings s)
        {
            if (string.Equals(result.StopReason, "refusal", StringComparison.OrdinalIgnoreCase))
            {
                throw new AiException(AiErrorKind.Refused);
            }

            Usage.Record(request.Model, result);
            AppLog.Info($"call model={request.Model} in={result.InputTokens} out={result.OutputTokens} stop={result.StopReason}");
            // 最後まで書けた結果だけを覚える（途中で切れたものは覚えない）
            if (key.Length > 0 && s.UseCache && !result.Truncated && result.Text.Length > 0)
            {
                Cache.Put(key, result, s.CacheDays);
            }
        }

        // 原因の例外を「型: 文」でつないだ文字列（ログ用。API キー・セルの内容は含まれない）
        private static string Chain(Exception e)
        {
            var parts = new List<string>();
            for (Exception? x = e.InnerException; x != null && parts.Count < 5; x = x.InnerException)
            {
                parts.Add(x.GetType().Name + ": " + x.Message);
            }

            return string.Join(" <- ", parts);
        }

        private SemaphoreSlim Limiter()
        {
            lock (gate)
            {
                int wanted = Math.Max(1, settings().MaxConcurrency);
                if (wanted != limiterSize)
                {
                    // 設定が変わった：新しい呼び出しから新しい数を使う（実行中のものは前の制限のまま終わる）
                    limiterSize = wanted;
                    limiter = new SemaphoreSlim(wanted, wanted);
                }

                return limiter;
            }
        }

        private async Task<LlmResult> WithRetryAsync(Func<Task<LlmResult>> call, CancellationToken cancel, string model, Func<bool>? canRetry = null)
        {
            const int maxAttempts = 4;
            for (int attempt = 1; ; attempt++)
            {
                var slot = Limiter();
                try
                {
                    await slot.WaitAsync(cancel).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new AiException(AiErrorKind.Cancelled);
                }

                AiException failure;
                try
                {
                    return await call().ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested)
                {
                    throw new AiException(AiErrorKind.Cancelled);
                }
                catch (Exception e)
                {
                    failure = AnthropicLlm.Translate(e, cancel);
                }
                finally
                {
                    slot.Release();
                }

                AppLog.Warn("attempt " + attempt + " failed: " + failure.Kind + " " + failure.Status + " " + Chain(failure));
                if (!failure.IsTransient || attempt >= maxAttempts || (canRetry is not null && !canRetry()))
                {
                    throw failure;
                }

                // 待つ間は同時実行の枠を空けておく。混み合いは長めに待つ（4 秒・8 秒・12 秒の目安 + ゆらぎ）
                double baseSeconds = (failure.Kind == AiErrorKind.RateLimit || failure.Kind == AiErrorKind.Overloaded ? 4 : 2) * attempt;
                try
                {
                    await delay(TimeSpan.FromSeconds(baseSeconds + new Random().NextDouble()), cancel).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw new AiException(AiErrorKind.Cancelled);
                }
            }
        }
    }
}
