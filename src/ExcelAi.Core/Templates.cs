using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExcelAi
{
    /// <summary>プロンプトのテンプレート。Prompt の {{value}}・{{A}}・{{row}} を、処理するセルの値に差し替える。</summary>
    public sealed class PromptTemplate
    {
        public string Id { get; set; } = string.Empty;

        /// <summary>利用者が付けた名前（組み込みは NameKey から）。</summary>
        public string Name { get; set; } = string.Empty;

        public string NameKey { get; set; } = string.Empty;

        public string System { get; set; } = string.Empty;

        public string Prompt { get; set; } = string.Empty;

        public bool Builtin { get; set; }

        public string DisplayName => NameKey.Length > 0 ? Loc.T(NameKey) : Name;

        public override string ToString() => DisplayName;
    }

    public static class TemplateRenderer
    {
        private static readonly Regex Placeholder = new Regex(@"\{\{\s*([A-Za-z]{1,10})\s*\}\}", RegexOptions.Compiled);

        /// <summary>{{名前}} を <paramref name="resolve"/> の結果に差し替える（null を返した名前はそのまま残す）。</summary>
        public static string Render(string template, Func<string, string?> resolve) =>
            Placeholder.Replace(template, m => resolve(m.Groups[1].Value) ?? m.Value);

        /// <summary>テンプレートが使っている差し込みの名前（重複なし）。</summary>
        public static IReadOnlyList<string> Placeholders(string template) =>
            Placeholder.Matches(template).Cast<Match>().Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    public static class TemplateStore
    {
        public static IReadOnlyList<PromptTemplate> Builtins { get; } = new[]
        {
            B("translate_en", "Translate the following text into English. Output only the translation.\n\n{{value}}"),
            B("translate_ja", "Translate the following text into Japanese. Output only the translation.\n\n{{value}}"),
            B("translate_zh", "Translate the following text into Simplified Chinese. Output only the translation.\n\n{{value}}"),
            B("summarize", "Summarize the following text in at most two sentences, in the same language as the text.\n\n{{value}}"),
            B("proofread", "Correct typos and grammar mistakes in the following text. Keep its meaning and language. Output only the corrected text.\n\n{{value}}"),
            B("classify", "Classify the following text into exactly one of these categories: Category1, Category2, Other. Output only the category name.\n\n{{value}}"),
            B("extract", "From the following text, extract the phone number. Output only the extracted value, or leave the answer empty if there is none.\n\n{{value}}"),
            B("sentiment", "Classify the sentiment of the following text as exactly one of: Positive, Neutral, Negative. Output only that word.\n\n{{value}}"),
            B("normalize_address", "Normalize the following Japanese address into the form: prefecture, city/ward, street number, building name (separated by single spaces). Output only the address.\n\n{{value}}"),
        };

        private static PromptTemplate B(string id, string prompt) =>
            new PromptTemplate { Id = id, NameKey = "tpl." + id, Prompt = prompt, Builtin = true };

        /// <summary>組み込み + 利用者のテンプレート（templates.json）。同じ Id は利用者のものが優先。</summary>
        public static IReadOnlyList<PromptTemplate> All(string? file = null)
        {
            var list = new List<PromptTemplate>(Builtins);
            foreach (var user in LoadUser(file ?? AppPaths.TemplatesFile))
            {
                int existing = list.FindIndex(t => string.Equals(t.Id, user.Id, StringComparison.OrdinalIgnoreCase));
                if (existing >= 0)
                {
                    list[existing] = user;
                }
                else
                {
                    list.Add(user);
                }
            }

            return list;
        }

        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static IReadOnlyList<PromptTemplate> LoadUser(string file)
        {
            try
            {
                if (File.Exists(file))
                {
                    var items = JsonSerializer.Deserialize<List<PromptTemplate>>(File.ReadAllText(file, Encoding.UTF8), Json) ?? new List<PromptTemplate>();
                    foreach (var t in items)
                    {
                        t.Builtin = false;
                        t.NameKey = string.Empty;
                        if (t.Id.Length == 0)
                        {
                            t.Id = "user-" + Math.Abs(t.Name.GetHashCode());
                        }
                    }

                    return items.Where(t => t.Prompt.Length > 0).ToList();
                }
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                AppLog.Warn("templates: " + e.Message);
            }

            return new List<PromptTemplate>();
        }

        /// <summary>利用者のテンプレートのファイルがなければ見本を書いて、パスを返す（メモ帳などで編集する）。</summary>
        public static string EnsureUserFile(string? file = null)
        {
            file ??= AppPaths.TemplatesFile;
            if (!File.Exists(file))
            {
                var sample = new List<PromptTemplate>
                {
                    new PromptTemplate
                    {
                        Id = "my-sample",
                        Name = Loc.T("tpl.sample.name"),
                        System = string.Empty,
                        Prompt = Loc.T("tpl.sample.prompt") + "\n\n{{value}}",
                    },
                };
                File.WriteAllText(file, JsonSerializer.Serialize(sample, Json), new UTF8Encoding(false));
            }

            return file;
        }
    }
}
