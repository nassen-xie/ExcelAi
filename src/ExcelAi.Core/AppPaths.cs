using System;
using System.IO;

namespace ExcelAi
{
    /// <summary>設定・キャッシュ・ログの置き場所（%APPDATA%\ExcelAi。環境変数 EXCELAI_HOME で変えられる）。</summary>
    public static class AppPaths
    {
        public static string Home
        {
            get
            {
                string? custom = Environment.GetEnvironmentVariable("EXCELAI_HOME");
                string home = !string.IsNullOrWhiteSpace(custom)
                    ? custom!
                    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ExcelAi");
                Directory.CreateDirectory(home);
                return home;
            }
        }

        public static string SettingsFile => Path.Combine(Home, "settings.json");

        public static string TemplatesFile => Path.Combine(Home, "templates.json");

        public static string CacheFile => Path.Combine(Home, "cache.jsonl");

        public static string UsageFile => Path.Combine(Home, "usage.json");

        public static string LogDirectory
        {
            get
            {
                string dir = Path.Combine(Home, "logs");
                Directory.CreateDirectory(dir);
                return dir;
            }
        }
    }
}
