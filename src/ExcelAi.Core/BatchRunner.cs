using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExcelAi
{
    public sealed class BatchItem
    {
        public BatchItem(int index, string prompt, string? system = null)
        {
            Index = index;
            Prompt = prompt;
            System = system;
        }

        public int Index { get; }

        public string Prompt { get; }

        public string? System { get; }
    }

    public sealed class BatchResult
    {
        public int Index { get; set; }

        public string Text { get; set; } = string.Empty;

        public AiException? Error { get; set; }

        public bool FromCache { get; set; }

        public bool Truncated { get; set; }

        public bool Ok => Error is null;
    }

    public sealed class BatchProgress
    {
        public int Done { get; set; }

        public int Failed { get; set; }

        public int Total { get; set; }
    }

    /// <summary>複数の依頼を同時実行の数を守って処理する。1 件ごとに結果を通知する（Excel への書き込みはその都度できる）。</summary>
    public static class BatchRunner
    {
        public static async Task<IReadOnlyList<BatchResult>> RunAsync(
            IReadOnlyList<BatchItem> items,
            AiGateway gateway,
            Action<BatchResult, BatchProgress> onResult,
            CancellationToken cancel,
            string? model = null)
        {
            var results = new BatchResult[items.Count];
            var progress = new BatchProgress { Total = items.Count };
            var gate = new object();
            // 認証・同意のエラーは全件同じなので、最初に出たら止める
            using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancel);

            async Task One(int i)
            {
                var item = items[i];
                BatchResult result;
                try
                {
                    stop.Token.ThrowIfCancellationRequested();
                    var request = gateway.NewRequest(item.System, new[] { new ChatMessage(true, item.Prompt) }, model);
                    var answer = await gateway.CompleteAsync(request, stop.Token).ConfigureAwait(false);
                    result = new BatchResult { Index = item.Index, Text = answer.Text, FromCache = answer.FromCache, Truncated = answer.Truncated };
                }
                catch (OperationCanceledException)
                {
                    result = new BatchResult { Index = item.Index, Error = new AiException(AiErrorKind.Cancelled) };
                }
                catch (AiException e)
                {
                    result = new BatchResult { Index = item.Index, Error = e };
                    if (e.Kind == AiErrorKind.NoKey || e.Kind == AiErrorKind.NoConsent || e.Kind == AiErrorKind.Auth || e.Kind == AiErrorKind.Billing)
                    {
                        stop.Cancel();
                    }
                }

                results[i] = result;
                lock (gate)
                {
                    progress.Done++;
                    if (!result.Ok)
                    {
                        progress.Failed++;
                    }

                    onResult(result, new BatchProgress { Done = progress.Done, Failed = progress.Failed, Total = progress.Total });
                }
            }

            // 同時実行の数は AiGateway が守る。ここでは全件を投入する（多すぎるときのために少しずつ）
            const int window = 64;
            for (int start = 0; start < items.Count; start += window)
            {
                var tasks = Enumerable.Range(start, Math.Min(window, items.Count - start)).Select(One).ToList();
                await Task.WhenAll(tasks).ConfigureAwait(false);
                if (stop.IsCancellationRequested)
                {
                    for (int i = start + window; i < items.Count; i++)
                    {
                        results[i] = new BatchResult { Index = items[i].Index, Error = new AiException(AiErrorKind.Cancelled) };
                    }

                    break;
                }
            }

            return results;
        }

        /// <summary>費用の概算（USD）。入力は 1 トークン約 2 文字、出力は入力と同じくらい + 考える分として見積もる。</summary>
        public static decimal EstimateCost(IReadOnlyList<BatchItem> items, string? systemPrompt, AiSettings settings)
        {
            long input = 0, output = 0;
            foreach (var item in items)
            {
                long inTokens = (item.Prompt.Length + (item.System ?? systemPrompt ?? string.Empty).Length) / 2 + 20;
                input += inTokens;
                output += Math.Min(settings.MaxTokens, Math.Max(60, item.Prompt.Length / 2)) + 100;
            }

            return Models.Cost(settings.Model, input, output);
        }
    }
}
