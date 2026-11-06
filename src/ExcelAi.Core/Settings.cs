using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ExcelAi
{
    /// <summary>利用者ごとの設定。API キーは Windows の DPAPI（現在のユーザーだけが読める）で暗号化して保存する。</summary>
    public sealed class AiSettings
    {
        private string? provider;
        private string? model;

        /// <summary>使う AI サービス（<see cref="Providers"/> の Id）。未設定で、以前の版の Claude のキーがある場合は anthropic、それ以外は deepseek。</summary>
        public string Provider
        {
            get => provider ?? (string.IsNullOrEmpty(ApiKeyProtected) ? Providers.DefaultId : "anthropic");
            set => provider = value;
        }

        /// <summary>モデル名（一覧にない名前も入力できる）。未設定ならサービスの既定。</summary>
        public string Model
        {
            get => model ?? (Providers.Find(Provider).DefaultModel is { Length: > 0 } d ? d : Models.DefaultId);
            set => model = value;
        }

        [JsonIgnore]
        public ProviderInfo ProviderInfo => Providers.Find(Provider);

        /// <summary>実際の接続先。設定の BaseUrl があればそれ、なければサービスの既定（空は SDK の既定）。</summary>
        [JsonIgnore]
        public string ResolvedBaseUrl => BaseUrl.Length > 0 ? BaseUrl : ProviderInfo.BaseUrl;

        /// <summary>考える深さ（low / medium / high / max）。セルの処理は low で十分なことが多い。</summary>
        public string Effort { get; set; } = "low";

        /// <summary>1 回の応答の最大トークン数（考える分も含む）。</summary>
        public int MaxTokens { get; set; } = 4096;

        /// <summary>同時に API を呼ぶ数。</summary>
        public int MaxConcurrency { get; set; } = 4;

        public int TimeoutSeconds { get; set; } = 120;

        public bool UseCache { get; set; } = true;

        public int CacheDays { get; set; } = 30;

        /// <summary>1 つのセル（または 1 つの引数）から送る最大の文字数。</summary>
        public int CellCharLimit { get; set; } = 8000;

        /// <summary>auto / ja / zh / en。</summary>
        public string Language { get; set; } = "auto";

        /// <summary>空はサービスの既定の接続先。社内のゲートウェイや Custom の場合に指定する。</summary>
        public string BaseUrl { get; set; } = string.Empty;

        public string SystemPrompt { get; set; } = DefaultSystemPrompt;

        /// <summary>一括処理の前に、件数と費用の概算を見せて確認する。</summary>
        public bool ConfirmBatch { get; set; } = true;

        public bool ConsentAccepted { get; set; }

        public string ConsentDate { get; set; } = string.Empty;

        /// <summary>以前の版の API キー（Claude 用。DPAPI で暗号化・Base64）。今は <see cref="ApiKeysProtected"/> を使い、読むときだけ参照する。</summary>
        public string? ApiKeyProtected { get; set; }

        /// <summary>サービスごとの API キー（DPAPI で暗号化した Base64）。</summary>
        public Dictionary<string, string> ApiKeysProtected { get; set; } = new Dictionary<string, string>();

        public const string DefaultSystemPrompt =
            "You are an assistant embedded in Microsoft Excel. Answer with only the requested content, concisely, "
            + "in plain text (no Markdown, no code fences, no preamble) unless the user asks for another format. "
            + "Reply in the language of the user's request unless told otherwise.";

        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ExcelAi.v1");

        /// <summary>今のサービスの API キー。</summary>
        [JsonIgnore]
        public string ApiKey
        {
            get => GetKey(Provider);
            set => SetKey(Provider, value);
        }

        /// <summary>指定したサービスの保存済みの API キー（なければ空）。</summary>
        public string GetKey(string providerId)
        {
            string? protectedText = null;
            if (ApiKeysProtected is null || !ApiKeysProtected.TryGetValue(providerId, out protectedText))
            {
                // 以前の版は Claude のキーだけを ApiKeyProtected に持っていた
                protectedText = providerId == "anthropic" ? ApiKeyProtected : null;
            }

            if (string.IsNullOrEmpty(protectedText))
            {
                return string.Empty;
            }

            try
            {
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser));
            }
            catch (Exception e) when (e is CryptographicException || e is FormatException)
            {
                return string.Empty;
            }
        }

        public void SetKey(string providerId, string value)
        {
            ApiKeysProtected ??= new Dictionary<string, string>();
            if (providerId == "anthropic")
            {
                ApiKeyProtected = null;
            }

            if (string.IsNullOrWhiteSpace(value))
            {
                ApiKeysProtected.Remove(providerId);
                return;
            }

            ApiKeysProtected[providerId] = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value.Trim()), Entropy, DataProtectionScope.CurrentUser));
        }

        /// <summary>設定の API キー。ない場合はサービスの環境変数（DeepSeek なら DEEPSEEK_API_KEY など）。</summary>
        [JsonIgnore]
        public string EffectiveApiKey
        {
            get
            {
                string key = ApiKey;
                if (key.Length > 0)
                {
                    return key;
                }

                string env = ProviderInfo.EnvVar;
                return env.Length > 0 ? (Environment.GetEnvironmentVariable(env) ?? string.Empty).Trim() : string.Empty;
            }
        }

        public AiSettings Clone() => JsonSerializer.Deserialize<AiSettings>(JsonSerializer.Serialize(this)) ?? new AiSettings();

        /// <summary>範囲外の値を直す。</summary>
        public void Normalize()
        {
            Provider = Providers.Find(Provider).Id;
            if (string.IsNullOrWhiteSpace(Model))
            {
                Model = ProviderInfo.DefaultModel.Length > 0 ? ProviderInfo.DefaultModel : Models.DefaultId;
            }

            Model = Model.Trim();

            if (Array.IndexOf(new[] { "low", "medium", "high", "max" }, Effort) < 0)
            {
                Effort = "low";
            }

            MaxTokens = Math.Max(256, Math.Min(MaxTokens, 128000));
            MaxConcurrency = Math.Max(1, Math.Min(MaxConcurrency, 16));
            TimeoutSeconds = Math.Max(10, Math.Min(TimeoutSeconds, 900));
            CacheDays = Math.Max(1, Math.Min(CacheDays, 365));
            CellCharLimit = Math.Max(100, Math.Min(CellCharLimit, 200000));
            if (string.IsNullOrWhiteSpace(SystemPrompt))
            {
                SystemPrompt = DefaultSystemPrompt;
            }

            BaseUrl = (BaseUrl ?? string.Empty).Trim();
        }
    }

    public static class SettingsStore
    {
        private static readonly object Gate = new object();
        private static AiSettings? current;
        private static DateTime loadedStamp;

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>今の設定（ファイルが変わったら読み直す）。</summary>
        public static AiSettings Current
        {
            get
            {
                lock (Gate)
                {
                    string path = AppPaths.SettingsFile;
                    DateTime stamp = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue;
                    if (current is null || stamp != loadedStamp)
                    {
                        current = Read(path);
                        loadedStamp = stamp;
                    }

                    return current;
                }
            }
        }

        public static event EventHandler? Changed;

        public static void Save(AiSettings settings)
        {
            settings.Normalize();
            lock (Gate)
            {
                string path = AppPaths.SettingsFile;
                string temp = path + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(settings, Json), new UTF8Encoding(false));
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temp, path);
                current = settings;
                loadedStamp = File.GetLastWriteTimeUtc(path);
            }

            Changed?.Invoke(null, EventArgs.Empty);
        }

        private static AiSettings Read(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var settings = JsonSerializer.Deserialize<AiSettings>(File.ReadAllText(path, Encoding.UTF8), Json) ?? new AiSettings();
                    settings.Normalize();
                    return settings;
                }
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                AppLog.Warn("settings: " + e.Message);
            }

            return new AiSettings();
        }
    }
}
