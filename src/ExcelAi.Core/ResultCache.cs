using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ExcelAi
{
    /// <summary>
    /// 同じ依頼の結果を覚えておく（数式の AI() は再計算のたびに呼ばれるため、費用を抑える大事な仕組み）。
    /// メモリと、%APPDATA%\ExcelAi\cache.jsonl（Excel を閉じても残る）に保存する。
    /// </summary>
    public sealed class ResultCache
    {
        private sealed class Entry
        {
            public string K { get; set; } = string.Empty;

            public long T { get; set; }

            public string Text { get; set; } = string.Empty;

            public long In { get; set; }

            public long Out { get; set; }
        }

        private readonly object gate = new object();
        private readonly Dictionary<string, Entry> items = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private readonly string? file;
        private bool loaded;
        private const int MaxEntries = 20000;

        /// <param name="file">保存先（null はメモリだけ）。</param>
        public ResultCache(string? file)
        {
            this.file = file;
        }

        public int Count
        {
            get
            {
                lock (gate)
                {
                    EnsureLoaded(30);
                    return items.Count;
                }
            }
        }

        /// <summary>依頼の内容から決まるキー。</summary>
        public static string KeyOf(LlmRequest r)
        {
            var sb = new StringBuilder();
            sb.Append(r.Model).Append('\u0001').Append(r.Effort).Append('\u0001').Append(r.MaxTokens).Append('\u0001').Append(r.System);
            foreach (var m in r.Messages)
            {
                sb.Append('\u0001').Append(m.FromUser ? 'U' : 'A').Append(m.Text);
            }

            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()))).Replace("-", string.Empty);
            }
        }

        public bool TryGet(string key, int days, out LlmResult result)
        {
            lock (gate)
            {
                EnsureLoaded(days);
                if (items.TryGetValue(key, out var entry) && !Expired(entry, days))
                {
                    result = new LlmResult { Text = entry.Text, InputTokens = entry.In, OutputTokens = entry.Out, StopReason = "end_turn", FromCache = true };
                    return true;
                }
            }

            result = new LlmResult();
            return false;
        }

        public void Put(string key, LlmResult result, int days)
        {
            var entry = new Entry { K = key, T = DateTime.UtcNow.Ticks, Text = result.Text, In = result.InputTokens, Out = result.OutputTokens };
            lock (gate)
            {
                EnsureLoaded(days);
                items[key] = entry;
                if (file is not null)
                {
                    try
                    {
                        File.AppendAllText(file, JsonSerializer.Serialize(entry) + "\n", new UTF8Encoding(false));
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        AppLog.Warn("cache write: " + e.Message);
                    }
                }
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                items.Clear();
                loaded = true;
                if (file is not null)
                {
                    try
                    {
                        File.Delete(file);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                    {
                        AppLog.Warn("cache clear: " + e.Message);
                    }
                }
            }
        }

        private static bool Expired(Entry e, int days) => new DateTime(e.T, DateTimeKind.Utc) < DateTime.UtcNow.AddDays(-days);

        // 最初に使うときにファイルを読む。期限切れ・古いものを捨て、多すぎる場合は新しい方だけ残して書き直す
        private void EnsureLoaded(int days)
        {
            if (loaded)
            {
                return;
            }

            loaded = true;
            if (file is null || !File.Exists(file))
            {
                return;
            }

            int lines = 0;
            try
            {
                foreach (string line in File.ReadLines(file, Encoding.UTF8))
                {
                    lines++;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<Entry>(line);
                        if (entry is not null && entry.K.Length > 0 && !Expired(entry, days))
                        {
                            items[entry.K] = entry;
                        }
                    }
                    catch (JsonException)
                    {
                        // 壊れた行は読み飛ばす
                    }
                }

                if (items.Count > MaxEntries)
                {
                    foreach (var old in items.Values.OrderBy(v => v.T).Take(items.Count - MaxEntries).ToList())
                    {
                        items.Remove(old.K);
                    }
                }

                if (lines > items.Count * 1.3 + 50)
                {
                    File.WriteAllLines(file, items.Values.OrderBy(v => v.T).Select(v => JsonSerializer.Serialize(v)), new UTF8Encoding(false));
                }
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Warn("cache load: " + e.Message);
            }
        }
    }
}
