using System;
using System.Collections.Generic;
using System.Linq;

namespace ExcelAi.Addin
{
    public enum OutputMode
    {
        /// <summary>選択範囲の右隣。</summary>
        Right,

        /// <summary>指定した列。</summary>
        Column,

        /// <summary>元のセルを置き換える。</summary>
        Replace,
    }

    /// <summary>「選択範囲を処理」の指定（画面とは別にして、画面なしでも動かせるようにする）。</summary>
    public sealed class ProcessOptions
    {
        public string Template { get; set; } = "{{value}}";

        public string? System { get; set; }

        /// <summary>true：行ごと（{{A}} などで列を使う）。false：セル 1 つずつ。</summary>
        public bool RowMode { get; set; }

        public OutputMode Output { get; set; } = OutputMode.Right;

        /// <summary>Output が Column のときの列（A, B, …）。</summary>
        public string ColumnLetter { get; set; } = "Z";

        public bool SkipEmpty { get; set; } = true;

        public bool SkipDone { get; set; } = true;
    }

    internal sealed class ProcessPlan
    {
        public List<BatchItem> Items { get; } = new List<BatchItem>();

        /// <summary>Items と同じ順の、書き込み先（シートの行・列）。</summary>
        public List<(int Row, int Col)> Targets { get; } = new List<(int Row, int Col)>();

        public string? Error { get; set; }
    }

    /// <summary>処理する項目と書き込み先を決める・結果をセルに書く（画面に依存しない）。</summary>
    internal static class ProcessEngine
    {
        public const int MaxItems = 20000;
        public const string ErrorMark = "#AI!";

        /// <summary>
        /// 選択範囲から、AI に送る項目と書き込み先を作る。<paramref name="forEstimate"/> が true の場合は費用の見積もり用（Excel のセルを読まず、速い）。
        /// </summary>
        public static ProcessPlan Build(SelectionInfo selection, ProcessOptions options, AiSettings settings, bool forEstimate)
        {
            var plan = new ProcessPlan();
            if (selection.Rows == 0)
            {
                plan.Error = Loc.T("proc.noselection");
                return plan;
            }

            int columnStart = 0;
            if (options.Output == OutputMode.Column)
            {
                columnStart = CellText.ColumnIndex(options.ColumnLetter);
                if (columnStart == 0)
                {
                    plan.Error = Loc.T("proc.column.invalid");
                    return plan;
                }
            }

            if (options.RowMode && options.Output == OutputMode.Replace)
            {
                plan.Error = Loc.T("proc.out.replace");
                return plan;
            }

            string template = options.Template.Replace("\r\n", "\n");
            string? systemText = string.IsNullOrWhiteSpace(options.System) ? null : options.System;
            var sheet = selection.Sheet;
            int index = 0;
            for (int r = 1; r <= selection.Rows; r++)
            {
                int sheetRow = selection.Row + r - 1;
                int cols = options.RowMode ? 1 : selection.Cols;
                for (int c = 1; c <= cols; c++)
                {
                    string value;
                    int sheetCol;
                    if (options.RowMode)
                    {
                        value = string.Join("\t", Enumerable.Range(1, selection.Cols).Select(cc => selection.Text(r, cc)));
                        sheetCol = selection.Col;
                    }
                    else
                    {
                        value = selection.Text(r, c);
                        sheetCol = selection.Col + c - 1;
                    }

                    bool blank = options.RowMode ? value.Replace("\t", string.Empty).Trim().Length == 0 : value.Trim().Length == 0;
                    if (options.SkipEmpty && blank)
                    {
                        continue;
                    }

                    int outCol = options.Output == OutputMode.Replace ? sheetCol
                        : options.Output == OutputMode.Column ? columnStart + (options.RowMode ? 0 : c - 1)
                        : selection.Col + selection.Cols + (options.RowMode ? 0 : c - 1);
                    if (options.SkipDone && !forEstimate && options.Output != OutputMode.Replace)
                    {
                        // すでに結果がある（エラーの印のものは除く）セルは処理しない = 途中から再開できる
                        string existing = ExcelHost.ReadCell(sheet, sheetRow, outCol);
                        if (existing.Length > 0 && !existing.StartsWith(ErrorMark, StringComparison.Ordinal))
                        {
                            continue;
                        }
                    }

                    string limited = CellText.Limit(value, settings.CellCharLimit);
                    string prompt;
                    if (forEstimate)
                    {
                        prompt = template.Replace("{{value}}", limited);
                    }
                    else
                    {
                        int capturedRow = sheetRow;
                        prompt = TemplateRenderer.Render(template, name =>
                        {
                            if (string.Equals(name, "value", StringComparison.OrdinalIgnoreCase))
                            {
                                return limited;
                            }

                            if (string.Equals(name, "row", StringComparison.OrdinalIgnoreCase))
                            {
                                return capturedRow.ToString();
                            }

                            int col = CellText.ColumnIndex(name);
                            return col == 0 ? null : CellText.Limit(ExcelHost.ReadCell(sheet, capturedRow, col), settings.CellCharLimit);
                        });
                    }

                    plan.Items.Add(new BatchItem(index++, prompt, systemText));
                    plan.Targets.Add((sheetRow, outCol));
                    if (plan.Items.Count > MaxItems)
                    {
                        plan.Error = Loc.F("proc.toomany", plan.Items.Count, MaxItems);
                        return plan;
                    }
                }
            }

            return plan;
        }

        /// <summary>書き込む文字（成功は結果、失敗は「#AI! 理由」）。取り消したものは null。</summary>
        public static string? ResultText(BatchResult result)
        {
            if (result.Error?.Kind == AiErrorKind.Cancelled)
            {
                return null;
            }

            if (!result.Ok)
            {
                return ErrorMark + " " + result.Error!.UserMessage;
            }

            string text = result.Text.Trim();
            return result.Truncated ? text + " " + Loc.T("warn.truncated") : text;
        }
    }
}
