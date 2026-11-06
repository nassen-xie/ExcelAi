using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExcelDna.Integration;

namespace ExcelAi.Addin
{
    /// <summary>セルの数式で使える関数（=AI(…) など）。結果が返るまでは「処理中」と出し、Excel は止まらない。</summary>
    public static class Functions
    {
        private const string Category = "AI";

        [ExcelFunction(Name = "AI", Category = Category, IsThreadSafe = true,
            Description = "Sends an instruction to the AI service chosen in Settings (DeepSeek by default) and returns the result. Identical inputs are cached.")]
        public static object Ai(
            [ExcelArgument(Name = "prompt", Description = "Instruction for the AI")] string prompt,
            [ExcelArgument(Name = "data", Description = "(Optional) Cell or range to work on")] object data,
            [ExcelArgument(Name = "system", Description = "(Optional) System prompt")] object system,
            [ExcelArgument(Name = "model", Description = "(Optional) Model name, e.g. deepseek-flash")] object model)
        {
            string user = Compose(prompt, data);
            string? systemText = Optional(system);
            string? modelText = Optional(model);
            return Run("AI", new object[] { user, systemText ?? string.Empty, modelText ?? string.Empty }, user, systemText, modelText);
        }

        [ExcelFunction(Name = "AI_TRANSLATE", Category = Category, IsThreadSafe = true, Description = "Translates text into the given language.")]
        public static object Translate(
            [ExcelArgument(Name = "text", Description = "Text (cell) to translate")] object text,
            [ExcelArgument(Name = "language", Description = "(Optional) Target language. Default: English")] object language)
        {
            string target = Optional(language) ?? "English";
            string user = Compose("Translate the following text into " + target + ". Output only the translation.", text);
            return Run("AI_TRANSLATE", new object[] { user }, user, null, null);
        }

        [ExcelFunction(Name = "AI_SUMMARIZE", Category = Category, IsThreadSafe = true, Description = "Summarizes text.")]
        public static object Summarize(
            [ExcelArgument(Name = "text", Description = "Text (cell) to summarize")] object text,
            [ExcelArgument(Name = "maxChars", Description = "(Optional) Approximate maximum number of characters")] object maxChars)
        {
            string limit = Optional(maxChars) is { } n && double.TryParse(n, out double chars) && chars > 0 ? $" Use at most about {(int)chars} characters." : " Use at most two sentences.";
            string user = Compose("Summarize the following text in the same language as the text." + limit + " Output only the summary.", text);
            return Run("AI_SUMMARIZE", new object[] { user }, user, null, null);
        }

        [ExcelFunction(Name = "AI_CLASSIFY", Category = Category, IsThreadSafe = true, Description = "Classifies text into exactly one of the given categories.")]
        public static object Classify(
            [ExcelArgument(Name = "text", Description = "Text (cell) to classify")] object text,
            [ExcelArgument(Name = "categories", Description = "Categories (comma-separated text, or a range)")] object categories)
        {
            string list = string.Join(", ", CategoryNames(categories));
            string user = Compose("Classify the following text into exactly one of these categories: " + list + ". Output only the category name, exactly as written.", text);
            return Run("AI_CLASSIFY", new object[] { user }, user, null, null);
        }

        [ExcelFunction(Name = "AI_EXTRACT", Category = Category, IsThreadSafe = true, Description = "Extracts only the requested item from text.")]
        public static object Extract(
            [ExcelArgument(Name = "text", Description = "Text (cell) to read")] object text,
            [ExcelArgument(Name = "what", Description = "Item to extract, e.g. phone number")] string what)
        {
            string user = Compose("From the following text, extract: " + what + ". Output only the extracted value, or leave the answer empty if there is none.", text);
            return Run("AI_EXTRACT", new object[] { user }, user, null, null);
        }

        [ExcelFunction(Name = "AI_USAGE", Category = Category, Description = "AI usage this month (calls and estimated cost).")]
        public static string Usage()
        {
            var t = AiGateway.Default.Usage.ThisMonth();
            return $"{t.Calls} calls / {t.InputTokens + t.OutputTokens} tokens / ~${t.Cost:0.####} (cache hits: {t.CacheHits})";
        }

        [ExcelFunction(Name = "AI_VERSION", Category = Category, Description = "Version of Excel AI Assistant.")]
        public static string Version() => Loc.F("udf.version", AddInEntry.VersionText);

        // ---- 内部 ----

        private static object Run(string name, object[] key, string user, string? system, string? model)
        {
            if (user.Trim().Length == 0)
            {
                return string.Empty;
            }

            return ExcelAsyncUtil.Observe(name, key, () => new AiObservable(ct => Compute(user, system, model, ct)));
        }

        /// <summary>モデルを呼んで文字にする。エラーは「#AI! 説明」の文字にする（セルに理由を出す）。</summary>
        internal static async Task<string> Compute(string user, string? system, string? model, CancellationToken cancel)
        {
            try
            {
                var gateway = AiGateway.Default;
                var request = gateway.NewRequest(string.IsNullOrWhiteSpace(system) ? null : system, new[] { new ChatMessage(true, user) }, model);
                var result = await gateway.CompleteAsync(request, cancel).ConfigureAwait(false);
                string text = result.Text.Trim();
                return result.Truncated ? text + " " + Loc.T("warn.truncated") : text;
            }
            catch (AiException e) when (e.Kind != AiErrorKind.Cancelled)
            {
                return Loc.T("udf.prefix") + " " + e.UserMessage;
            }
            catch (AiException)
            {
                return string.Empty;
            }
            catch (Exception e)
            {
                AppLog.Error("udf", e);
                return Loc.T("udf.prefix") + " " + e.Message;
            }
        }

        /// <summary>指示 + 対象のデータ（セル・範囲）を 1 つの依頼の文にする。長すぎる分は切る。</summary>
        internal static string Compose(string instruction, object? data)
        {
            int limit = SettingsStore.Current.CellCharLimit;
            string text = IsMissing(data) ? string.Empty : CellText.Limit(CellText.RangeToText(data), limit);
            return text.Length == 0 ? instruction : instruction + "\n\n" + text;
        }

        private static bool IsMissing(object? value) => value is null || value is ExcelMissing || value is ExcelEmpty;

        private static string? Optional(object? value)
        {
            if (IsMissing(value))
            {
                return null;
            }

            string text = CellText.RangeToText(value).Trim();
            return text.Length == 0 ? null : text;
        }

        private static IEnumerable<string> CategoryNames(object? categories)
        {
            if (categories is object[,] grid)
            {
                return grid.Cast<object>().Select(CellText.ToText).Where(s => s.Length > 0);
            }

            return CellText.ToText(categories).Split(new[] { ',', '、', '，', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0);
        }
    }

    /// <summary>非同期の結果を Excel に返す（最初に「処理中」、終わったら結果）。セルの数式が変わる・消えると、処理を取り消す。</summary>
    internal sealed class AiObservable : IExcelObservable
    {
        private readonly Func<CancellationToken, Task<string>> work;

        public AiObservable(Func<CancellationToken, Task<string>> work)
        {
            this.work = work;
        }

        public IDisposable Subscribe(IExcelObserver observer)
        {
            var cts = new CancellationTokenSource();
            observer.OnNext(Loc.T("udf.working"));
            work(cts.Token).ContinueWith(task =>
            {
                if (cts.IsCancellationRequested)
                {
                    return;
                }

                if (task.IsFaulted)
                {
                    observer.OnNext(Loc.T("udf.prefix") + " " + task.Exception?.GetBaseException().Message);
                }
                else if (!task.IsCanceled)
                {
                    observer.OnNext(task.Result);
                }

                observer.OnCompleted();
            }, TaskScheduler.Default);
            return new CancelOnDispose(cts);
        }

        private sealed class CancelOnDispose : IDisposable
        {
            private readonly CancellationTokenSource cts;

            public CancelOnDispose(CancellationTokenSource cts)
            {
                this.cts = cts;
            }

            public void Dispose() => cts.Cancel();
        }
    }
}
