using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Messages;

namespace ExcelAi
{
    /// <summary>Anthropic 公式 SDK（Claude の Messages API）を呼ぶ実装。</summary>
    public sealed class AnthropicLlm : ILlm
    {
        private readonly Func<AiSettings> settings;

        public AnthropicLlm(Func<AiSettings> settings)
        {
            Tls.Enable();
            this.settings = settings;
        }

        public async Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken cancel)
        {
            try
            {
                var client = CreateClient();
                var message = await client.Messages.Create(BuildParams(request), cancel).ConfigureAwait(false);
                var result = new LlmResult
                {
                    Text = string.Concat(message.Content.Select(b => b.Value).OfType<TextBlock>().Select(t => t.Text)),
                    InputTokens = message.Usage.InputTokens,
                    OutputTokens = message.Usage.OutputTokens,
                    CacheReadTokens = message.Usage.CacheReadInputTokens ?? 0,
                    StopReason = Raw(message.StopReason),
                };
                return result;
            }
            catch (Exception e) when (!(e is AiException))
            {
                throw Translate(e, cancel);
            }
        }

        public async Task<LlmResult> StreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken cancel)
        {
            try
            {
                var client = CreateClient();
                var text = new StringBuilder();
                var result = new LlmResult();
                await foreach (RawMessageStreamEvent streamEvent in client.Messages.CreateStreaming(BuildParams(request), cancel).WithCancellation(cancel).ConfigureAwait(false))
                {
                    if (streamEvent.TryPickStart(out var start))
                    {
                        result.InputTokens = start.Message.Usage.InputTokens;
                        result.CacheReadTokens = start.Message.Usage.CacheReadInputTokens ?? 0;
                    }
                    else if (streamEvent.TryPickContentBlockDelta(out var block) && block.Delta.TryPickText(out var delta))
                    {
                        text.Append(delta.Text);
                        onDelta(delta.Text);
                    }
                    else if (streamEvent.TryPickDelta(out var messageDelta))
                    {
                        result.OutputTokens = messageDelta.Usage.OutputTokens;
                        result.StopReason = Raw(messageDelta.Delta.StopReason) is { Length: > 0 } stop ? stop : result.StopReason;
                    }
                }

                result.Text = text.ToString();
                return result;
            }
            catch (Exception e) when (!(e is AiException))
            {
                throw Translate(e, cancel);
            }
        }

        private IAnthropicClient CreateClient()
        {
            var s = settings();
            string key = s.EffectiveApiKey;
            if (key.Length == 0)
            {
                throw new AiException(AiErrorKind.NoKey);
            }

            IAnthropicClient client = new AnthropicClient
            {
                ApiKey = key,
                Timeout = TimeSpan.FromSeconds(s.TimeoutSeconds),
                MaxRetries = 1,
            };
            if (s.BaseUrl.Length > 0)
            {
                client = client.WithOptions(o => o with { BaseUrl = s.BaseUrl });
            }

            return client;
        }

        private static MessageCreateParams BuildParams(LlmRequest r)
        {
            var messages = r.Messages
                .Select(m => m.FromUser ? (MessageParam)new UserMessageParam { Content = m.Text } : new AssistantMessageParam { Content = m.Text })
                .ToList();
            var p = new MessageCreateParams
            {
                Model = r.Model,
                MaxTokens = r.MaxTokens,
                Messages = messages,
                OutputConfig = new OutputConfig { Effort = ParseEffort(r.Effort) },
            };
            return r.System.Length > 0 ? p with { System = r.System } : p;
        }

        private static Effort ParseEffort(string effort)
        {
            switch ((effort ?? string.Empty).ToLowerInvariant())
            {
                case "medium":
                    return Effort.Medium;
                case "high":
                    return Effort.High;
                case "max":
                    return Effort.Max;
                default:
                    return Effort.Low;
            }
        }

        /// <summary>SDK・通信の例外を、利用者に説明できる <see cref="AiException"/> にする。</summary>
        public static AiException Translate(Exception e, CancellationToken cancel)
        {
            if (e is AiException ai)
            {
                return ai;
            }

            if (cancel.IsCancellationRequested && (e is OperationCanceledException || e is TaskCanceledException))
            {
                return new AiException(AiErrorKind.Cancelled, string.Empty, 0, e);
            }

            if (e is AnthropicRateLimitException)
            {
                return new AiException(AiErrorKind.RateLimit, string.Empty, 429, e);
            }

            if (e is AnthropicUnauthorizedException || e is AnthropicForbiddenException)
            {
                return new AiException(AiErrorKind.Auth, string.Empty, e is AnthropicUnauthorizedException ? 401 : 403, e);
            }

            if (e is Anthropic5xxException five)
            {
                int status = (int)five.StatusCode;
                return new AiException(status == 529 || status == 503 ? AiErrorKind.Overloaded : AiErrorKind.Other, "HTTP " + status, status, e);
            }

            if (e is AnthropicApiException api)
            {
                int status = (int)api.StatusCode;
                return new AiException(status >= 400 && status < 500 ? AiErrorKind.BadRequest : AiErrorKind.Other, Shorten(ApiMessage(api.Message)), status, e);
            }

            for (Exception? x = e; x != null; x = x.InnerException)
            {
                if (x is TimeoutException || x is TaskCanceledException || x is OperationCanceledException)
                {
                    return new AiException(AiErrorKind.Timeout, string.Empty, 0, e);
                }

                if (x is HttpRequestException || x is System.IO.IOException || x is System.Net.WebException || x is System.Net.Sockets.SocketException || x.GetType().Name.Contains("IOException"))
                {
                    return new AiException(AiErrorKind.Network, Shorten(Innermost(x).Message), 0, e);
                }
            }

            return new AiException(AiErrorKind.Other, Shorten(e.Message), 0, e);
        }

        private static Exception Innermost(Exception e)
        {
            while (e.InnerException != null)
            {
                e = e.InnerException;
            }

            return e;
        }

        // SDK の例外の文は「Status Code: BadRequest {JSON}」の形。サーバーが返した error.message だけを取り出す
        internal static string ApiMessage(string text)
        {
            int open = text.IndexOf('{');
            if (open >= 0)
            {
                try
                {
                    using (var doc = System.Text.Json.JsonDocument.Parse(text.Substring(open)))
                    {
                        if (doc.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("message", out var message) && message.ValueKind == System.Text.Json.JsonValueKind.String)
                        {
                            return message.GetString() ?? text;
                        }
                    }
                }
                catch (System.Text.Json.JsonException)
                {
                    // JSON でなければそのまま
                }
            }

            return text;
        }

        // ApiEnum の ToString は JSON の形（"end_turn"）なので、引用符を外す
        private static string Raw(object? value) => value?.ToString()?.Trim('"') ?? string.Empty;

        private static string Shorten(string text) => text.Length > 200 ? text.Substring(0, 200) + "…" : text;
    }
}
