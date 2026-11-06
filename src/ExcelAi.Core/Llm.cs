using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ExcelAi
{
    public sealed class ChatMessage
    {
        public ChatMessage(bool fromUser, string text)
        {
            FromUser = fromUser;
            Text = text;
        }

        public bool FromUser { get; }

        public string Text { get; }
    }

    /// <summary>モデルへの 1 回の依頼。</summary>
    public sealed class LlmRequest
    {
        public string Model { get; set; } = Models.DefaultId;

        public string System { get; set; } = string.Empty;

        public IReadOnlyList<ChatMessage> Messages { get; set; } = Array.Empty<ChatMessage>();

        public int MaxTokens { get; set; } = 4096;

        public string Effort { get; set; } = "low";
    }

    public sealed class LlmResult
    {
        public string Text { get; set; } = string.Empty;

        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        public long CacheReadTokens { get; set; }

        /// <summary>end_turn / max_tokens / refusal など。</summary>
        public string StopReason { get; set; } = string.Empty;

        /// <summary>最大トークンで途中で切れた。</summary>
        public bool Truncated => string.Equals(StopReason, "max_tokens", StringComparison.OrdinalIgnoreCase);

        /// <summary>キャッシュから返した（API を呼んでいない）。</summary>
        public bool FromCache { get; set; }
    }

    /// <summary>モデルを呼ぶ部分（本物は <see cref="OpenAiLlm"/> / <see cref="AnthropicLlm"/>。テストでは差し替える）。</summary>
    public interface ILlm
    {
        Task<LlmResult> CompleteAsync(LlmRequest request, CancellationToken cancel);

        /// <summary>応答を少しずつ <paramref name="onDelta"/> に渡す（チャットの表示用）。</summary>
        Task<LlmResult> StreamAsync(LlmRequest request, Action<string> onDelta, CancellationToken cancel);
    }

    public enum AiErrorKind
    {
        NoKey,
        NoConsent,
        Auth,
        Billing,
        RateLimit,
        Overloaded,
        Network,
        Timeout,
        BadRequest,
        Refused,
        Cancelled,
        Other,
    }

    /// <summary>利用者に見せられる形のエラー。</summary>
    public sealed class AiException : Exception
    {
        public AiException(AiErrorKind kind, string detail = "", int status = 0, Exception? inner = null)
            : base(kind + (detail.Length > 0 ? ": " + detail : string.Empty), inner)
        {
            Kind = kind;
            Detail = detail;
            Status = status;
        }

        public AiErrorKind Kind { get; }

        public string Detail { get; }

        public int Status { get; }

        /// <summary>再試行する価値があるか（混み合い・通信・時間切れ）。</summary>
        public bool IsTransient => Kind == AiErrorKind.RateLimit || Kind == AiErrorKind.Overloaded || Kind == AiErrorKind.Network || Kind == AiErrorKind.Timeout;

        /// <summary>画面・セルに出す文（今の言語）。</summary>
        public string UserMessage
        {
            get
            {
                string text = Loc.T("err." + Kind.ToString().ToLowerInvariant());
                return Detail.Length > 0 && Kind != AiErrorKind.NoKey && Kind != AiErrorKind.NoConsent ? text + " (" + Detail + ")" : text;
            }
        }
    }
}
