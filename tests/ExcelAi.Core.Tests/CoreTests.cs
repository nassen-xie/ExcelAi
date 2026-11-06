using System.Text.Json;
using System.Text.RegularExpressions;

namespace ExcelAi.Tests;

[Collection("env")]
public sealed class SettingsTests
{
    [Fact]
    public void ApiKey_Is_Encrypted_At_Rest_And_Round_Trips()
    {
        using var home = new TempHome();
        var settings = new AiSettings { ApiKey = "sk-ant-test-1234567890" };
        SettingsStore.Save(settings);

        string json = File.ReadAllText(AppPaths.SettingsFile);
        Assert.DoesNotContain("sk-ant-test", json);
        Assert.Contains("ApiKeyProtected", json);
        Assert.Equal("sk-ant-test-1234567890", SettingsStore.Current.ApiKey);
    }

    [Fact]
    public void Normalize_Clamps_Out_Of_Range_Values()
    {
        var s = new AiSettings { MaxTokens = 5, MaxConcurrency = 99, TimeoutSeconds = 1, CacheDays = 0, CellCharLimit = 1, Effort = "extreme", Model = " " };
        s.Normalize();
        Assert.Equal(256, s.MaxTokens);
        Assert.Equal(16, s.MaxConcurrency);
        Assert.Equal(10, s.TimeoutSeconds);
        Assert.Equal(1, s.CacheDays);
        Assert.Equal(100, s.CellCharLimit);
        Assert.Equal("low", s.Effort);
        Assert.Equal(Models.DefaultId, s.Model);
    }

    [Fact]
    public void Environment_Key_Is_Used_When_No_Stored_Key()
    {
        using var home = new TempHome();
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", "sk-from-env");
        Assert.Equal("sk-from-env", new AiSettings().EffectiveApiKey);
        Assert.Equal("sk-own", new AiSettings { ApiKey = "sk-own" }.EffectiveApiKey);
        // 別のサービスには、そのサービスの環境変数だけを使う
        Assert.Equal(string.Empty, new AiSettings { Provider = "openai" }.EffectiveApiKey);
        Environment.SetEnvironmentVariable("DEEPSEEK_API_KEY", null);
    }

    [Fact]
    public void Corrupt_Settings_File_Falls_Back_To_Defaults()
    {
        using var home = new TempHome();
        File.WriteAllText(AppPaths.SettingsFile, "{ not json");
        Assert.Equal(Models.DefaultId, SettingsStore.Current.Model);
    }
}

public sealed class LocTests
{
    [Fact]
    public void Every_Key_Has_Three_Languages_And_Matching_Format_Placeholders()
    {
        foreach (var (key, values) in Loc.All)
        {
            Assert.Equal(3, values.Length);
            Assert.All(values, v => Assert.False(string.IsNullOrWhiteSpace(v), key));
            var shape = values.Select(v => string.Join(",", Regex.Matches(v, @"\{(\d+)\}").Select(m => m.Groups[1].Value).OrderBy(x => x))).Distinct().ToList();
            Assert.True(shape.Count == 1, $"{key}: format placeholders differ between languages");
        }
    }

    [Fact]
    public void All_Keys_Used_In_The_Sources_Exist()
    {
        string root = FindRoot();
        var used = new HashSet<string>();
        foreach (string file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories).Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\")))
        {
            foreach (Match m in Regex.Matches(File.ReadAllText(file), @"Loc\.[TF]\(""([a-z0-9_.]+)""[,)]"))
            {
                used.Add(m.Groups[1].Value);
            }
        }

        Assert.NotEmpty(used);
        var missing = used.Where(k => !Loc.All.ContainsKey(k)).ToList();
        Assert.True(missing.Count == 0, "missing keys: " + string.Join(", ", missing));
    }

    [Fact]
    public void Error_Kinds_All_Have_Messages()
    {
        foreach (AiErrorKind kind in Enum.GetValues(typeof(AiErrorKind)))
        {
            Assert.True(Loc.All.ContainsKey("err." + kind.ToString().ToLowerInvariant()), kind.ToString());
        }
    }

    private static string FindRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "ExcelAi.slnx")))
        {
            dir = Path.GetDirectoryName(dir)!;
        }

        return dir!;
    }
}

public sealed class CellTextTests
{
    [Theory]
    [InlineData(1, "A")]
    [InlineData(26, "Z")]
    [InlineData(27, "AA")]
    [InlineData(702, "ZZ")]
    [InlineData(703, "AAA")]
    [InlineData(16384, "XFD")]
    public void Column_Letters_Round_Trip(int index, string letters)
    {
        Assert.Equal(letters, CellText.ColumnLetters(index));
        Assert.Equal(index, CellText.ColumnIndex(letters));
        Assert.Equal(index, CellText.ColumnIndex(letters.ToLowerInvariant()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("A1")]
    [InlineData("XFE")]
    [InlineData("ABCD")]
    public void Invalid_Columns_Are_Zero(string letters) => Assert.Equal(0, CellText.ColumnIndex(letters));

    [Fact]
    public void Values_Convert_To_Text()
    {
        Assert.Equal("", CellText.ToText(null));
        Assert.Equal("1.5", CellText.ToText(1.5));
        Assert.Equal("100", CellText.ToText(100d));
        Assert.Equal("TRUE", CellText.ToText(true));
        Assert.Equal("a", CellText.ToText("a"));
    }

    [Fact]
    public void Ranges_Become_Tab_Separated_Text_And_Are_Limited()
    {
        var grid = new object[2, 2] { { "a", 1d }, { null!, "d" } };
        Assert.Equal("a\t1\n\td", CellText.RangeToText(grid));
        Assert.Equal("abc", CellText.Limit("abcdef", 3));
        Assert.Equal("abc", CellText.Limit("abc", 10));
    }
}

public sealed class TemplateTests
{
    [Fact]
    public void Placeholders_Are_Replaced_And_Unknown_Are_Kept()
    {
        string result = TemplateRenderer.Render("T:{{value}} / {{ B }} / {{row}} / {{Z}}", name => name.ToLowerInvariant() switch
        {
            "value" => "v",
            "b" => "bee",
            "row" => "7",
            _ => null,
        });
        Assert.Equal("T:v / bee / 7 / {{Z}}", result);
        Assert.Equal(new[] { "value", "B", "row" }, TemplateRenderer.Placeholders("{{value}} {{B}} {{value}} {{row}}"));
    }

    [Fact]
    public void Builtins_Have_Names_In_All_Languages_And_Use_Value()
    {
        foreach (var t in TemplateStore.Builtins)
        {
            Assert.True(Loc.All.ContainsKey(t.NameKey), t.NameKey);
            Assert.Contains("{{value}}", t.Prompt);
        }
    }

    [Fact]
    public void User_Templates_Override_Builtins_By_Id_And_Bad_Files_Are_Ignored()
    {
        using var home = new TempHome();
        string file = Path.Combine(home.Path, "t.json");
        File.WriteAllText(file, JsonSerializer.Serialize(new[]
        {
            new PromptTemplate { Id = "summarize", Name = "mine", Prompt = "custom {{value}}" },
            new PromptTemplate { Id = "new", Name = "new one", Prompt = "x {{value}}" },
            new PromptTemplate { Id = "empty", Name = "no prompt", Prompt = "" },
        }));
        var all = TemplateStore.All(file);
        Assert.Equal("custom {{value}}", all.Single(t => t.Id == "summarize").Prompt);
        Assert.Equal("mine", all.Single(t => t.Id == "summarize").DisplayName);
        Assert.Contains(all, t => t.Id == "new");
        Assert.DoesNotContain(all, t => t.Id == "empty");

        File.WriteAllText(file, "not json");
        Assert.Equal(TemplateStore.Builtins.Count, TemplateStore.All(file).Count);
    }

    [Fact]
    public void Sample_File_Is_Created_Once()
    {
        using var home = new TempHome();
        string file = Path.Combine(home.Path, "u.json");
        TemplateStore.EnsureUserFile(file);
        string first = File.ReadAllText(file);
        File.WriteAllText(file, first + " ");
        TemplateStore.EnsureUserFile(file);
        Assert.Equal(first + " ", File.ReadAllText(file));
        Assert.Single(TemplateStore.LoadUser(file));
    }
}

public sealed class CacheAndUsageTests
{
    private static LlmRequest Request(string text, string model = "claude-opus-5-5") =>
        new() { Model = model, System = "s", Messages = new[] { new ChatMessage(true, text) }, MaxTokens = 100, Effort = "low" };

    [Fact]
    public void Same_Request_Has_Same_Key_And_Any_Difference_Changes_It()
    {
        string key = ResultCache.KeyOf(Request("a"));
        Assert.Equal(key, ResultCache.KeyOf(Request("a")));
        Assert.NotEqual(key, ResultCache.KeyOf(Request("b")));
        Assert.NotEqual(key, ResultCache.KeyOf(Request("a", "claude-haiku-5-5")));
        var other = Request("a");
        other.Effort = "high";
        Assert.NotEqual(key, ResultCache.KeyOf(other));
    }

    [Fact]
    public void Cache_Persists_Expires_And_Survives_Corrupt_Lines()
    {
        using var home = new TempHome();
        string file = Path.Combine(home.Path, "c.jsonl");
        var cache = new ResultCache(file);
        cache.Put("k1", new LlmResult { Text = "one", InputTokens = 3, OutputTokens = 4 }, 30);
        File.AppendAllText(file, "garbage line\n");

        var reloaded = new ResultCache(file);
        Assert.True(reloaded.TryGet("k1", 30, out var hit));
        Assert.Equal("one", hit.Text);
        Assert.True(hit.FromCache);
        Assert.False(reloaded.TryGet("missing", 30, out _));
        Assert.Equal(1, reloaded.Count);

        reloaded.Clear();
        Assert.False(File.Exists(file));
        Assert.False(reloaded.TryGet("k1", 30, out _));
    }

    [Fact]
    public void Old_Entries_Are_Dropped()
    {
        using var home = new TempHome();
        string file = Path.Combine(home.Path, "c.jsonl");
        long old = DateTime.UtcNow.AddDays(-40).Ticks;
        File.WriteAllText(file, "{\"K\":\"old\",\"T\":" + old + ",\"Text\":\"x\",\"In\":1,\"Out\":1}\n");
        Assert.False(new ResultCache(file).TryGet("old", 30, out _));
    }

    [Fact]
    public void Usage_Accumulates_Per_Month_And_Persists()
    {
        using var home = new TempHome();
        string file = Path.Combine(home.Path, "u.json");
        var meter = new UsageMeter(file);
        meter.Record("claude-opus-5-5", new LlmResult { InputTokens = 1_000_000, OutputTokens = 100_000 });
        meter.Record("claude-opus-5-5", new LlmResult { FromCache = true });
        meter.Flush();

        var totals = new UsageMeter(file).ThisMonth();
        Assert.Equal(1, totals.Calls);
        Assert.Equal(1, totals.CacheHits);
        Assert.Equal(1_000_000, totals.InputTokens);
        Assert.Equal(4m + 2m, totals.Cost);
    }

    [Fact]
    public void Cost_Uses_The_Model_Price_Table()
    {
        Assert.Equal(0.10m + 0.50m, Models.Cost("claude-haiku-5-5", 1_000_000, 1_000_000));
        // DeepSeek: キャッシュに当たった入力は安い
        Assert.Equal(0.30m + 1.20m + 0.006m, Models.Cost("deepseek-flash", 1_000_000, 1_000_000, 1_000_000));
        // 一覧にないモデル（自分で入力した名前）は料金不明 = 0、表示は「?」
        Assert.Null(Models.Find("nope"));
        Assert.Equal(0m, Models.Cost("nope", 1_000_000, 1_000_000));
        Assert.Equal("nope", Models.DisplayName("nope"));
        Assert.Equal("?", Models.CostText("nope", 0m));
        Assert.Equal("1.5", Models.CostText("deepseek-flash", 1.5m));
    }
}
