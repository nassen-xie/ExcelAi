using System;
using System.Collections.Generic;
using System.Globalization;

namespace ExcelAi
{
    /// <summary>画面の文字（日本語・中文・English）。言語は設定の Language（auto は Windows の表示言語）。</summary>
    public static partial class Loc
    {
        private static readonly Dictionary<string, string[]> Table = new Dictionary<string, string[]>(StringComparer.Ordinal);

        static Loc()
        {
            Register();
        }

        private static void K(string key, string ja, string zh, string en) => Table[key] = new[] { ja, zh, en };

        /// <summary>テストなどで言語を固定する（null で設定に従う）。</summary>
        public static string? Override { get; set; }

        /// <summary>
        /// 「自動」のときに使う、ホスト（Excel）の表示言語（2 文字の言語コード。取れなければ null）。
        /// 設定が「自動」なら、Excel の表示言語 → Windows の表示言語 → English の順で決める。
        /// </summary>
        public static Func<string?>? HostLanguage { get; set; }

        /// <summary>「自動」の決め方：ホストの言語があればそれ（日本語・中国語以外は English）、なければ Windows の言語。</summary>
        public static string Resolve(string? host, string windows)
        {
            string code = string.IsNullOrEmpty(host) ? windows : host!;
            code = code.ToLowerInvariant();
            return code == "ja" || code == "zh" ? code : "en";
        }

        public static string Language
        {
            get
            {
                string setting = Override ?? SettingsStore.Current.Language;
                if (string.Equals(setting, "ja", StringComparison.OrdinalIgnoreCase) || string.Equals(setting, "zh", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(setting, "en", StringComparison.OrdinalIgnoreCase))
                {
                    return setting.ToLowerInvariant();
                }

                return Resolve(HostLanguage?.Invoke(), CultureInfo.CurrentUICulture.TwoLetterISOLanguageName);
            }
        }

        public static string T(string key)
        {
            if (!Table.TryGetValue(key, out var values))
            {
                return key;
            }

            switch (Language)
            {
                case "ja":
                    return values[0];
                case "zh":
                    return values[1];
                default:
                    return values[2];
            }
        }

        public static string F(string key, params object[] args) => string.Format(CultureInfo.CurrentCulture, T(key), args);

        /// <summary>すべての言語がそろっているかを調べるための一覧。</summary>
        public static IReadOnlyDictionary<string, string[]> All => Table;
    }
}
