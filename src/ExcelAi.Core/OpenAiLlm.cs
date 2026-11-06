using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ExcelAi
{
    /// <summary>OpenAI 互換の Chat Completions API（POST {base}/chat/completions）を呼ぶ実装。DeepSeek をはじめ、キーで使える多くのサービスがこの形。</summary>
    public sealed class OpenAiLlm : ILlm
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        private readonly Func<AiSettings> settings;

        public OpenAiLlm(Func<AiSettings> settings)
        {
            Tls.Enable();
            this.settings = settings;
        }

        public async Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken cancel)
        {
            var s = settings();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                try
                {
                    timeout.CancelAfter(TimeSpan.FromSeconds(s.TimeoutSeconds));
                    using (var response = await SendAsync(s, request, stream: false, timeout.Token).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        EnsureSuccess(response, body);
                        return Parse(body);
                    }
                }
                catch (Exception e) when (!(e is AiException))
                {
                    throw AnthropicLlm.Translate(e, cancel);
                }
            }
        }

        public async Task<LlmResult> StreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken cancel)
        {
            var s = settings();
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            {
                try
                {
                    TimeSpan limit = TimeSpan.FromSeconds(s.TimeoutSeconds);
                    timeout.CancelAfter(limit);
                    using (var response = await SendAsync(s, request, stream: true, timeout.Token).ConfigureAwait(false))
                    {
                        if (!response.IsSuccessStatusCode)
                        {
                            EnsureSuccess(response, await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                        }

                        var text = new StringBuilder();
                        var result = new LlmResult();
                        using (var reader = new StreamReader(await response.Content.ReadAsStreamAsync().ConfigureAwait(false), Encoding.UTF8))
                        {
                            string? line;
                            while ((line = await reader.ReadLineAsync().ConfigureAwait(false)) != null)
                            {
                                // 受信があるたびに時間切れを延ばす（長い応答を途中で切らない）
                                timeout.CancelAfter(limit);
                                if (!line.StartsWith("data:", StringComparison.Ordinal))
                                {
                                    continue;
                                }

                                string data = line.Substring(5).Trim();
                                if (data.Length == 0)
                                {
                                    continue;
                                }

                                if (data == "[DONE]")
                                {
                                    break;
                                }

                                using (var doc = JsonDocument.Parse(data))
                                {
                                    var root = doc.RootElement;
                                    if (root.TryGetProperty("error", out var error))
                                    {
                                        throw new AiException(AiErrorKind.Other, Shorten(ErrorText(error)));
                                    }

                                    ReadUsage(root, result);
                                    if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                                    {
                                        var choice = choices[0];
                                        if (choice.TryGetProperty("delta", out var delta) && delta.ValueKind == JsonValueKind.Object
                                            && delta.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                                        {
                                            string piece = content.GetString() ?? string.Empty;
                                            if (piece.Length > 0)
                                            {
                                                text.Append(piece);
                                                onDelta(piece);
                                            }
                                        }

                                        if (choice.TryGetProperty("finish_reason", out var finish) && finish.ValueKind == JsonValueKind.String)
                                        {
                                            result.StopReason = MapFinish(finish.GetString());
                                        }
                                    }
                                }
                            }
                        }

                        result.Text = text.ToString();
                        return result;
                    }
                }
                catch (Exception e) when (!(e is AiException))
                {
                    throw AnthropicLlm.Translate(e, cancel);
                }
            }
        }

        private static Task<HttpResponseMessage> SendAsync(AiSettings s, LlmRequest request, bool stream, CancellationToken cancel)
        {
            string key = s.EffectiveApiKey;
            if (key.Length == 0)
            {
                throw new AiException(AiErrorKind.NoKey);
            }

            string baseUrl = s.ResolvedBaseUrl.TrimEnd('/');
            if (baseUrl.Length == 0)
            {
                throw new AiException(AiErrorKind.BadRequest, "Base URL");
            }

            var message = new HttpRequestMessage(HttpMethod.Post, baseUrl + "/chat/completions")
            {
                Content = new StringContent(BuildBody(s, request, stream), new UTF8Encoding(false), "application/json"),
            };
            message.Headers.TryAddWithoutValidation("Authorization", "Bearer " + key);
            message.Headers.TryAddWithoutValidation("Accept", stream ? "text/event-stream" : "application/json");
            return Http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancel);
        }

        internal static string BuildBody(AiSettings s, LlmRequest request, bool stream)
        {
            using (var buffer = new MemoryStream())
            {
                using (var json = new Utf8JsonWriter(buffer))
                {
                    json.WriteStartObject();
                    json.WriteString("model", request.Model);
                    json.WriteStartArray("messages");
                    if (request.System.Length > 0)
                    {
                        WriteMessage(json, "system", request.System);
                    }

                    foreach (var m in request.Messages)
                    {
                        WriteMessage(json, m.FromUser ? "user" : "assistant", m.Text);
                    }

                    json.WriteEndArray();
                    json.WriteNumber(s.ProviderInfo.UsesMaxCompletionTokens ? "max_completion_tokens" : "max_tokens", request.MaxTokens);
                    if (stream)
                    {
                        json.WriteBoolean("stream", true);
                        json.WriteStartObject("stream_options");
                        json.WriteBoolean("include_usage", true);
                        json.WriteEndObject();
                    }

                    json.WriteEndObject();
                }

                return Encoding.UTF8.GetString(buffer.ToArray());
            }
        }

        private static void WriteMessage(Utf8JsonWriter json, string role, string content)
        {
            json.WriteStartObject();
            json.WriteString("role", role);
            json.WriteString("content", content);
            json.WriteEndObject();
        }

        private static LlmResult Parse(string body)
        {
            try
            {
                using (var doc = JsonDocument.Parse(body))
                {
                    var root = doc.RootElement;
                    var result = new LlmResult();
                    if (root.TryGetProperty("error", out var error))
                    {
                        throw new AiException(AiErrorKind.Other, Shorten(ErrorText(error)));
                    }

                    ReadUsage(root, result);
                    if (root.TryGetProperty("choices", out var choices) && choices.ValueKind == JsonValueKind.Array && choices.GetArrayLength() > 0)
                    {
                        var choice = choices[0];
                        if (choice.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.Object
                            && message.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                        {
                            result.Text = content.GetString() ?? string.Empty;
                        }

                        if (choice.TryGetProperty("finish_reason", out var finish) && finish.ValueKind == JsonValueKind.String)
                        {
                            result.StopReason = MapFinish(finish.GetString());
                        }
                    }

                    return result;
                }
            }
            catch (JsonException e)
            {
                throw new AiException(AiErrorKind.Other, "invalid response", 0, e);
            }
        }

        // DeepSeek は prompt_cache_hit_tokens / prompt_cache_miss_tokens、OpenAI は prompt_tokens_details.cached_tokens。
        // InputTokens は「キャッシュに当たらなかった入力」にそろえる（Claude と同じ扱い）
        private static void ReadUsage(JsonElement root, LlmResult result)
        {
            if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            long prompt = Number(usage, "prompt_tokens");
            long hit = Number(usage, "prompt_cache_hit_tokens");
            if (hit == 0 && usage.TryGetProperty("prompt_tokens_details", out var details) && details.ValueKind == JsonValueKind.Object)
            {
                hit = Number(details, "cached_tokens");
            }

            result.CacheReadTokens = hit;
            result.InputTokens = Math.Max(0, prompt - hit);
            result.OutputTokens = Number(usage, "completion_tokens");
        }

        private static long Number(JsonElement element, string name) =>
            element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out long n) ? n : 0;

        private static string MapFinish(string? reason)
        {
            switch (reason)
            {
                case "length":
                    return "max_tokens";
                case "content_filter":
                    return "refusal";
                case "stop":
                    return "end_turn";
                default:
                    return reason ?? string.Empty;
            }
        }

        private static void EnsureSuccess(HttpResponseMessage response, string body)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            int status = (int)response.StatusCode;
            string detail = Shorten(AnthropicLlm.ApiMessage(body));
            switch (status)
            {
                case 401:
                case 403:
                    throw new AiException(AiErrorKind.Auth, string.Empty, status);
                case 402:
                    throw new AiException(AiErrorKind.Billing, string.Empty, status);
                case 429:
                    throw new AiException(AiErrorKind.RateLimit, string.Empty, status);
                case 500:
                case 502:
                case 503:
                case 504:
                case 529:
                    throw new AiException(AiErrorKind.Overloaded, "HTTP " + status, status);
                default:
                    throw new AiException(status >= 400 && status < 500 ? AiErrorKind.BadRequest : AiErrorKind.Other, detail.Length > 0 ? detail : "HTTP " + status, status);
            }
        }

        private static string ErrorText(JsonElement error) =>
            error.ValueKind == JsonValueKind.Object && error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String ? message.GetString() ?? string.Empty : error.ToString();

        private static string Shorten(string text) => text.Length > 200 ? text.Substring(0, 200) + "…" : text;
    }

    /// <summary>設定のサービスに合わせて、OpenAI 互換 / Claude の実装に振り分ける。</summary>
    public sealed class RoutingLlm : ILlm
    {
        private readonly Func<AiSettings> settings;
        private readonly OpenAiLlm openAi;
        private readonly AnthropicLlm anthropic;

        public RoutingLlm(Func<AiSettings> settings)
        {
            this.settings = settings;
            openAi = new OpenAiLlm(settings);
            anthropic = new AnthropicLlm(settings);
        }

        private ILlm Pick() => settings().ProviderInfo.Protocol == ApiProtocol.Anthropic ? (ILlm)anthropic : openAi;

        public Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken cancel) => Pick().CompleteAsync(request, cancel);

        public Task<LlmResult> StreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken cancel) => Pick().StreamAsync(request, onDelta, cancel);
    }
}
