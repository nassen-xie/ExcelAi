using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace ExcelAi
{
    public sealed class UsageTotals
    {
        public long Calls { get; set; }

        public long CacheHits { get; set; }

        public long InputTokens { get; set; }

        public long OutputTokens { get; set; }

        public long CacheReadTokens { get; set; }

        public decimal Cost { get; set; }
    }

    /// <summary>月ごとの使用量と費用の概算（%APPDATA%\ExcelAi\usage.json）。</summary>
    public sealed class UsageMeter
    {
        private readonly object gate = new object();
        private readonly string? file;
        private Dictionary<string, UsageTotals>? months;
        private DateTime lastSave = DateTime.MinValue;

        public UsageMeter(string? file)
        {
            this.file = file;
        }

        public static string MonthKey(DateTime now) => now.ToString("yyyy-MM");

        public void Record(string model, LlmResult result)
        {
            lock (gate)
            {
                var t = Month();
                if (result.FromCache)
                {
                    t.CacheHits++;
                }
                else
                {
                    t.Calls++;
                    t.InputTokens += result.InputTokens;
                    t.OutputTokens += result.OutputTokens;
                    t.CacheReadTokens += result.CacheReadTokens;
                    t.Cost += Models.Cost(model, result.InputTokens, result.OutputTokens, result.CacheReadTokens);
                }

                if (DateTime.UtcNow - lastSave > TimeSpan.FromSeconds(3))
                {
                    Save();
                }
            }
        }

        public UsageTotals ThisMonth()
        {
            lock (gate)
            {
                var t = Month();
                return new UsageTotals
                {
                    Calls = t.Calls, CacheHits = t.CacheHits, InputTokens = t.InputTokens, OutputTokens = t.OutputTokens, CacheReadTokens = t.CacheReadTokens, Cost = t.Cost,
                };
            }
        }

        public void Flush()
        {
            lock (gate)
            {
                Save();
            }
        }

        private UsageTotals Month()
        {
            months ??= Load();
            string key = MonthKey(DateTime.Now);
            if (!months.TryGetValue(key, out var totals))
            {
                totals = new UsageTotals();
                months[key] = totals;
            }

            return totals;
        }

        private Dictionary<string, UsageTotals> Load()
        {
            try
            {
                if (file is not null && File.Exists(file))
                {
                    return JsonSerializer.Deserialize<Dictionary<string, UsageTotals>>(File.ReadAllText(file, Encoding.UTF8)) ?? new Dictionary<string, UsageTotals>();
                }
            }
            catch (Exception e) when (e is IOException || e is JsonException || e is UnauthorizedAccessException)
            {
                AppLog.Warn("usage load: " + e.Message);
            }

            return new Dictionary<string, UsageTotals>();
        }

        private void Save()
        {
            lastSave = DateTime.UtcNow;
            if (file is null || months is null)
            {
                return;
            }

            try
            {
                File.WriteAllText(file, JsonSerializer.Serialize(months), new UTF8Encoding(false));
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                AppLog.Warn("usage save: " + e.Message);
            }
        }
    }
}
