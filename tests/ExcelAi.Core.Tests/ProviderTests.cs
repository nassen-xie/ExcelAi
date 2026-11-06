using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExcelAi.Tests;

[Collection("env")]
public sealed class ProviderTests
{
    [Fact]
    public void New_Settings_Default_To_DeepSeek()
    {
        var s = new AiSettings();
        Assert.Equal("deepseek", s.Provider);
        Assert.Equal("deepseek-flash", s.Model);
        Assert.Equal("https://api.deepseek.com", s.ResolvedBaseUrl);
        Assert.Equal(ApiProtocol.OpenAi, s.ProviderInfo.Protocol);
    }

    [Fact]
    public void Keys_Are_Kept_Per_Provider_And_Round_Trip_Through_The_File()
    {
        using var home = new TempHome();
        var s = new AiSettings();
        s.SetKey("deepseek", "sk-ds");
        s.SetKey("openai", "sk-oa");
        s.Provider = "openai";
        Assert.Equal("sk-oa", s.ApiKey);
        Assert.Equal("sk-ds", s.GetKey("deepseek"));
        Assert.Equal(string.Empty, s.GetKey("anthropic"));
        SettingsStore.Save(s);

        string json = File.ReadAllText(AppPaths.SettingsFile);
        Assert.DoesNotContain("sk-ds", json);
        Assert.DoesNotContain("sk-oa", json);

        var loaded = SettingsStore.Current;
        Assert.Equal("openai", loaded.Provider);
        Assert.Equal("sk-oa", loaded.ApiKey);
        Assert.Equal("sk-ds", loaded.GetKey("deepseek"));

        loaded.SetKey("openai", "  ");
        Assert.Equal(string.Empty, loaded.GetKey("openai"));
    }

    [Fact]
    public void Settings_From_The_Claude_Only_Version_Keep_Working()
    {
        using var home = new TempHome();
        string legacyKey = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes("sk-ant-old"), Encoding.UTF8.GetBytes("ExcelAi.v1"), DataProtectionScope.CurrentUser));
        File.WriteAllText(AppPaths.SettingsFile, JsonSerializer.Serialize(new { Model = "claude-haiku-5-5", ApiKeyProtected = legacyKey, ConsentAccepted = true }));

        var s = SettingsStore.Current;
        Assert.Equal("anthropic", s.Provider);
        Assert.Equal("claude-haiku-5-5", s.Model);
        Assert.Equal("sk-ant-old", s.ApiKey);
        Assert.True(s.ProviderInfo.SupportsEffort);

        // 新しい保存形式にしても、キーは残る
        SettingsStore.Save(s);
        Assert.Equal("sk-ant-old", SettingsStore.Current.GetKey("anthropic"));
    }

    [Fact]
    public void Unknown_Provider_Falls_Back_And_Custom_Model_Names_Are_Kept()
    {
        var s = new AiSettings { Provider = "nope", Model = " my-private-model " };
        s.Normalize();
        Assert.Equal("deepseek", s.Provider);
        Assert.Equal("my-private-model", s.Model);
    }

    [Fact]
    public void Base_Url_Override_Wins_Over_The_Provider_Default()
    {
        var s = new AiSettings { Provider = "openai", BaseUrl = "https://gateway.example/v1" };
        Assert.Equal("https://gateway.example/v1", s.ResolvedBaseUrl);
        Assert.Equal("https://api.openai.com/v1", new AiSettings { Provider = "openai" }.ResolvedBaseUrl);
    }

    [Fact]
    public void Provider_Catalogue_Is_Consistent()
    {
        Assert.Equal("deepseek", Providers.All[0].Id);
        Assert.Equal(Providers.All.Count, Providers.All.Select(p => p.Id).Distinct().Count());
        foreach (var p in Providers.All.Where(p => p.Id != "custom"))
        {
            Assert.Contains(p.DefaultModel, p.Models.Select(m => m.Id));
        }

        foreach (var m in Models.All)
        {
            Assert.False(string.IsNullOrEmpty(Loc.T(m.NoteKey)), m.NoteKey);
        }
    }
}
