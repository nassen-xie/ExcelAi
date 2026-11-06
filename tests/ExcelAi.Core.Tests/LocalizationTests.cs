using System.Text.RegularExpressions;

namespace ExcelAi.Tests;

[Collection("env")]
public sealed class LocalizationTests
{
    [Theory]
    [InlineData("ja", "en", "ja")]      // Excel の表示言語が最優先
    [InlineData("zh", "ja", "zh")]
    [InlineData("en", "ja", "en")]
    [InlineData("ko", "ja", "en")]      // 対応していない言語は English（Windows の言語には戻らない）
    [InlineData("fr", "zh", "en")]
    [InlineData(null, "zh", "zh")]      // Excel の言語が取れなければ Windows の言語
    [InlineData("", "ja", "ja")]
    [InlineData(null, "de", "en")]
    [InlineData("ZH", "en", "zh")]
    public void Auto_Language_Prefers_Excel_Then_Windows_Then_English(string? host, string windows, string expected)
    {
        Assert.Equal(expected, Loc.Resolve(host, windows));
    }

    [Fact]
    public void Language_Setting_Overrides_Auto_And_Auto_Uses_The_Host_Language()
    {
        using var home = new TempHome();
        Loc.Override = null;
        try
        {
            Loc.HostLanguage = () => "zh";
            Assert.Equal("zh", Loc.Language);
            Assert.Equal("关于", Loc.T("rb.about"));

            var s = new AiSettings { Language = "en" };
            SettingsStore.Save(s);
            Assert.Equal("en", Loc.Language);
            Assert.Equal("About", Loc.T("rb.about"));

            s.Language = "ja";
            SettingsStore.Save(s);
            Assert.Equal("バージョン情報", Loc.T("rb.about"));
        }
        finally
        {
            Loc.HostLanguage = null;
        }
    }

    [Fact]
    public void Author_Info_Is_Set()
    {
        Assert.Equal("株式会社ナッセンシステム", AppInfo.Company);
        Assert.Equal("contact@nassen.co.jp", AppInfo.Email);
        Assert.Equal("https://nassen.co.jp", AppInfo.Website);
        Assert.Equal("Copyright © 2026 株式会社ナッセンシステム", AppInfo.Copyright);
        Assert.Equal("MIT", AppInfo.License);
    }

    // 画面の文字は Strings.cs（3 言語）に集める。コードに日本語・中国語を直接書かない（固有名詞と言語名は例外）
    [Fact]
    public void No_Hard_Coded_Cjk_Text_Outside_The_Language_Table()
    {
        string root = AppContext.BaseDirectory;
        while (root != null && !File.Exists(Path.Combine(root, "ExcelAi.slnx")))
        {
            root = Path.GetDirectoryName(root)!;
        }

        Assert.NotNull(root);
        var allowed = new[] { "Strings.cs", "AppInfo.cs" };
        var literal = new Regex("\"[^\"\\n]*[\\u3040-\\u30ff\\u3400-\\u9fff][^\"\\n]*\"");
        var offenders = new List<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar) || file.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar) || allowed.Contains(Path.GetFileName(file)))
            {
                continue;
            }

            int number = 0;
            foreach (string line in File.ReadLines(file))
            {
                number++;
                string code = line.TrimStart();
                if (code.StartsWith("//") || code.StartsWith("*") || code.StartsWith("/*"))
                {
                    continue;
                }

                // 行末のコメントは除く
                int comment = line.IndexOf("//", StringComparison.Ordinal);
                string text = comment >= 0 && !line.Substring(0, comment).Contains("\"") ? line.Substring(0, comment) : line;
                foreach (Match m in literal.Matches(text))
                {
                    // 言語の選択肢に出す「その言語自身の名前」は、翻訳しない
                    if (m.Value == "\"日本語\"" || m.Value == "\"中文\"")
                    {
                        continue;
                    }

                    offenders.Add(Path.GetRelativePath(root, file) + ":" + number + " " + m.Value);
                }
            }
        }

        Assert.True(offenders.Count == 0, "画面の文字は Strings.cs に入れてください:\n" + string.Join("\n", offenders));
    }
}
