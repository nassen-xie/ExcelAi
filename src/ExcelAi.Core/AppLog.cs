using System;
using System.IO;
using System.Linq;
using System.Text;

namespace ExcelAi
{
    /// <summary>ログ（%APPDATA%\ExcelAi\logs\yyyyMMdd.log）。API キーとセルの内容は書かない。</summary>
    public static class AppLog
    {
        private static readonly object Gate = new object();
        private static bool cleaned;

        public static void Info(string message) => Write("INFO", message);

        public static void Warn(string message) => Write("WARN", message);

        public static void Error(string message, Exception? exception = null) =>
            Write("ERROR", exception is null ? message : message + " | " + exception.GetType().Name + ": " + exception.Message);

        private static void Write(string level, string message)
        {
            try
            {
                lock (Gate)
                {
                    string dir = AppPaths.LogDirectory;
                    if (!cleaned)
                    {
                        cleaned = true;
                        foreach (string old in Directory.EnumerateFiles(dir, "*.log").Where(f => File.GetLastWriteTimeUtc(f) < DateTime.UtcNow.AddDays(-14)))
                        {
                            File.Delete(old);
                        }
                    }

                    string file = Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd") + ".log");
                    File.AppendAllText(file, DateTime.Now.ToString("HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine, new UTF8Encoding(false));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // ログが書けなくても処理は続ける
            }
        }
    }
}
