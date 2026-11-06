using System;
using System.Collections.Generic;
using System.Linq;

namespace ExcelAi
{
    /// <summary>モデルと 1M トークンあたりの料金（USD。費用の概算だけに使う）。料金が 0 のものは「不明」。</summary>
    public sealed class ModelInfo
    {
        public ModelInfo(string id, string display, decimal inputPerMillion, decimal outputPerMillion, string noteKey, decimal? cacheReadPerMillion = null)
        {
            Id = id;
            Display = display;
            InputPerMillion = inputPerMillion;
            OutputPerMillion = outputPerMillion;
            NoteKey = noteKey;
            CacheReadPerMillion = cacheReadPerMillion ?? inputPerMillion / 10m;
        }

        public string Id { get; }

        public string Display { get; }

        public decimal InputPerMillion { get; }

        public decimal OutputPerMillion { get; }

        /// <summary>入力のうちキャッシュに当たった分の料金。</summary>
        public decimal CacheReadPerMillion { get; }

        public string NoteKey { get; }

        public bool PriceKnown => InputPerMillion > 0 || OutputPerMillion > 0;

        public override string ToString() => Display;
    }

    public enum ApiProtocol
    {
        /// <summary>POST {base}/chat/completions（OpenAI 互換。DeepSeek など多くのサービス）。</summary>
        OpenAi,

        /// <summary>Anthropic の Messages API。</summary>
        Anthropic,
    }

    /// <summary>AI サービス（接続先）。キーは各サービスごとに別々に保存する。</summary>
    public sealed class ProviderInfo
    {
        public ProviderInfo(string id, string display, ApiProtocol protocol, string baseUrl, string envVar, string keyUrl, string defaultModel, params ModelInfo[] models)
        {
            Id = id;
            Display = display;
            Protocol = protocol;
            BaseUrl = baseUrl;
            EnvVar = envVar;
            KeyUrl = keyUrl;
            DefaultModel = defaultModel;
            Models = models;
        }

        public string Id { get; }

        public string Display { get; }

        public ApiProtocol Protocol { get; }

        /// <summary>既定の接続先（空なら SDK の既定、または利用者が必ず指定する）。</summary>
        public string BaseUrl { get; }

        /// <summary>キーがないときに読む環境変数。</summary>
        public string EnvVar { get; }

        /// <summary>キーの取得先（案内の表示用）。</summary>
        public string KeyUrl { get; }

        public string DefaultModel { get; }

        public IReadOnlyList<ModelInfo> Models { get; }

        /// <summary>OpenAI の新しいモデルは max_tokens ではなく max_completion_tokens を使う。</summary>
        public bool UsesMaxCompletionTokens => Id == "openai";

        /// <summary>接続先を必ず利用者が入れる（Custom）。</summary>
        public bool NeedsBaseUrl => Id == "custom";

        /// <summary>考える深さ（Effort）を送れる（Claude だけ）。</summary>
        public bool SupportsEffort => Protocol == ApiProtocol.Anthropic;

        public override string ToString() => Display;
    }

    public static class Providers
    {
        public const string DefaultId = "deepseek";

        // 料金は公開情報からの目安（DeepSeek はピーク時間帯の料金）。正確な請求額は各サービスの画面で確認する
        public static readonly IReadOnlyList<ProviderInfo> All = new[]
        {
            new ProviderInfo("deepseek", "DeepSeek", ApiProtocol.OpenAi, "https://api.deepseek.com", "DEEPSEEK_API_KEY", "https://platform.deepseek.com/api_keys", "deepseek-flash",
                new ModelInfo("deepseek-flash", "DeepSeek Flash", 0.30m, 1.20m, "model.dsflash", 0.006m),
                new ModelInfo("deepseek-v4-pro", "DeepSeek V4 Pro", 1.32m, 3.96m, "model.dspro", 0.044m)),
            new ProviderInfo("openai", "OpenAI", ApiProtocol.OpenAi, "https://api.openai.com/v1", "OPENAI_API_KEY", "https://platform.openai.com/api-keys", "gpt-4o-mini",
                new ModelInfo("gpt-4o-mini", "GPT-4o mini", 0.15m, 0.60m, "model.other", 0.075m),
                new ModelInfo("gpt-4o", "GPT-4o", 2.50m, 10m, "model.other", 1.25m)),
            new ProviderInfo("anthropic", "Anthropic Claude", ApiProtocol.Anthropic, string.Empty, "ANTHROPIC_API_KEY", "https://console.anthropic.com", "claude-opus-5-5",
                new ModelInfo("claude-opus-5-5", "Claude Opus 5.5", 4m, 20m, "model.opus"),
                new ModelInfo("claude-sonnet-5-5", "Claude Sonnet 5.5", 2m, 10m, "model.sonnet"),
                new ModelInfo("claude-haiku-5-5", "Claude Haiku 5.5", 0.10m, 0.50m, "model.haiku"),
                new ModelInfo("claude-fable-5-1", "Claude Fable 5.1", 10m, 50m, "model.fable")),
            new ProviderInfo("qwen", "Qwen (Alibaba Cloud)", ApiProtocol.OpenAi, "https://dashscope.aliyuncs.com/compatible-mode/v1", "DASHSCOPE_API_KEY", "https://bailian.console.aliyun.com", "qwen-plus",
                new ModelInfo("qwen-plus", "Qwen Plus", 0, 0, "model.other"),
                new ModelInfo("qwen-turbo", "Qwen Turbo", 0, 0, "model.other")),
            new ProviderInfo("moonshot", "Moonshot (Kimi)", ApiProtocol.OpenAi, "https://api.moonshot.cn/v1", "MOONSHOT_API_KEY", "https://platform.moonshot.cn", "moonshot-v1-8k",
                new ModelInfo("moonshot-v1-8k", "Moonshot v1 8k", 0, 0, "model.other")),
            new ProviderInfo("zhipu", "Zhipu (GLM)", ApiProtocol.OpenAi, "https://open.bigmodel.cn/api/paas/v4", "ZHIPUAI_API_KEY", "https://open.bigmodel.cn", "glm-4-flash",
                new ModelInfo("glm-4-flash", "GLM-4 Flash", 0, 0, "model.other")),
            new ProviderInfo("siliconflow", "SiliconFlow", ApiProtocol.OpenAi, "https://api.siliconflow.cn/v1", "SILICONFLOW_API_KEY", "https://cloud.siliconflow.cn", "deepseek-ai/DeepSeek-V3",
                new ModelInfo("deepseek-ai/DeepSeek-V3", "DeepSeek V3 (SiliconFlow)", 0, 0, "model.other")),
            new ProviderInfo("openrouter", "OpenRouter", ApiProtocol.OpenAi, "https://openrouter.ai/api/v1", "OPENROUTER_API_KEY", "https://openrouter.ai/keys", "deepseek/deepseek-chat",
                new ModelInfo("deepseek/deepseek-chat", "DeepSeek (OpenRouter)", 0, 0, "model.other")),
            new ProviderInfo("custom", "Custom (OpenAI-compatible)", ApiProtocol.OpenAi, string.Empty, "AI_API_KEY", string.Empty, string.Empty),
        };

        public static ProviderInfo Find(string? id) =>
            All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
    }

    public static class Models
    {
        /// <summary>既定のモデル（既定のサービス DeepSeek の高速・低価格モデル）。</summary>
        public const string DefaultId = "deepseek-flash";

        /// <summary>全サービスのモデル（料金の検索用）。</summary>
        public static IEnumerable<ModelInfo> All => Providers.All.SelectMany(p => p.Models);

        /// <summary>モデル名から情報を探す。一覧にない名前（自分で入力したもの）は null。</summary>
        public static ModelInfo? Find(string? id) =>
            All.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>画面に出す名前（一覧にない場合はモデル名のまま）。</summary>
        public static string DisplayName(string? id) => Find(id)?.Display ?? (id ?? string.Empty);

        /// <summary>使ったトークンから費用（USD）を概算する。料金が不明なモデルは 0。InputTokens はキャッシュに当たらなかった分。</summary>
        public static decimal Cost(string? modelId, long inputTokens, long outputTokens, long cacheReadTokens = 0)
        {
            var m = Find(modelId);
            if (m is null)
            {
                return 0m;
            }

            return (inputTokens * m.InputPerMillion + cacheReadTokens * m.CacheReadPerMillion + outputTokens * m.OutputPerMillion) / 1_000_000m;
        }

        /// <summary>費用の表示用（不明なモデルは「?」）。</summary>
        public static string CostText(string? modelId, decimal cost) => Find(modelId)?.PriceKnown == true ? cost.ToString("0.####") : "?";
    }
}
