namespace ExcelAi.Tests;

/// <summary>EXCELAI_HOME を一時フォルダーにして、設定・キャッシュを本物と分ける。</summary>
public sealed class TempHome : IDisposable
{
    private readonly string? previous = Environment.GetEnvironmentVariable("EXCELAI_HOME");
    private readonly Dictionary<string, string?> previousKeys = Providers.All.Where(p => p.EnvVar.Length > 0).ToDictionary(p => p.EnvVar, p => Environment.GetEnvironmentVariable(p.EnvVar));

    public TempHome()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "excelai-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Environment.SetEnvironmentVariable("EXCELAI_HOME", Path);
        foreach (var name in previousKeys.Keys)
        {
            Environment.SetEnvironmentVariable(name, null);
        }

        Loc.Override = "ja";
    }

    public string Path { get; }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("EXCELAI_HOME", previous);
        foreach (var key in previousKeys)
        {
            Environment.SetEnvironmentVariable(key.Key, key.Value);
        }

        Loc.Override = null;
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

[CollectionDefinition("env", DisableParallelization = true)]
public sealed class EnvCollection
{
}

