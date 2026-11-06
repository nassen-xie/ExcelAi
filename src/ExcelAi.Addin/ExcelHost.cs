using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using ExcelDna.Integration;

namespace ExcelAi.Addin
{
    internal sealed class Win32Window : IWin32Window
    {
        public Win32Window(IntPtr handle)
        {
            Handle = handle;
        }

        public IntPtr Handle { get; }
    }

    /// <summary>選んでいる範囲（使用済みの範囲との重なり）。値は 1 から始まる 2 次元配列。</summary>
    internal sealed class SelectionInfo
    {
        public dynamic Sheet { get; set; } = null!;

        public int Row { get; set; }

        public int Col { get; set; }

        public int Rows { get; set; }

        public int Cols { get; set; }

        public object?[,] Values { get; set; } = new object?[1, 1];

        public string Address { get; set; } = string.Empty;

        public string Text(int r, int c) => CellText.ToText(Values[r, c]);
    }

    /// <summary>Excel のオブジェクトを触る部分。Excel の COM は、Excel のメイン スレッドからだけ呼ぶ。</summary>
    internal static class ExcelHost
    {
        private static string? uiLanguage;

        /// <summary>Excel の表示言語（2 文字の言語コード）。Excel のメイン スレッドで 1 回だけ調べて覚える（取れなければ null）。</summary>
        public static string? UiLanguage()
        {
            if (uiLanguage != null)
            {
                return uiLanguage.Length > 0 ? uiLanguage : null;
            }

            try
            {
                dynamic app = ExcelDnaUtil.Application;
                int lcid = (int)app.LanguageSettings.LanguageID[2];   // msoLanguageIDUI
                uiLanguage = new System.Globalization.CultureInfo(lcid).TwoLetterISOLanguageName;
            }
            catch (Exception e)
            {
                AppLog.Warn("excel ui language: " + e.Message);
                uiLanguage = string.Empty;
            }

            return uiLanguage.Length > 0 ? uiLanguage : null;
        }

        public static dynamic App => ExcelDnaUtil.Application;

        public static IWin32Window Owner
        {
            get
            {
                try
                {
                    return new Win32Window(new IntPtr((int)App.Hwnd));
                }
                catch (Exception e) when (e is COMException || e is InvalidCastException || e is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                {
                    return new Win32Window(IntPtr.Zero);
                }
            }
        }

        /// <summary>
        /// 選んでいる範囲を読む。複数の範囲・セル以外（グラフなど）は error に理由のキーを返す。
        /// 空の範囲（使用済みの範囲の外）は Rows = 0 の結果。
        /// </summary>
        public static SelectionInfo? ReadSelection(out string? error)
        {
            error = null;
            try
            {
                dynamic selection = App.Selection;
                dynamic sheet;
                try
                {
                    sheet = selection.Worksheet;
                    if (selection.Areas.Count > 1)
                    {
                        error = "proc.singlearea";
                        return null;
                    }
                }
                catch (Exception e) when (e is COMException || e is Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
                {
                    error = "proc.noselection";
                    return null;
                }

                dynamic used = sheet.UsedRange;
                dynamic overlap = App.Intersect(selection, used);
                var info = new SelectionInfo { Sheet = sheet };
                if (overlap == null)
                {
                    info.Rows = 0;
                    info.Cols = 0;
                    return info;
                }

                info.Row = (int)overlap.Row;
                info.Col = (int)overlap.Column;
                info.Rows = (int)overlap.Rows.Count;
                info.Cols = (int)overlap.Columns.Count;
                info.Address = (string)overlap.Address(false, false);
                object raw = overlap.Value2;
                var values = new object?[info.Rows + 1, info.Cols + 1];
                if (raw is object[,] grid)
                {
                    int r0 = grid.GetLowerBound(0), c0 = grid.GetLowerBound(1);
                    for (int r = 1; r <= info.Rows; r++)
                    {
                        for (int c = 1; c <= info.Cols; c++)
                        {
                            values[r, c] = grid[r0 + r - 1, c0 + c - 1];
                        }
                    }
                }
                else
                {
                    values[1, 1] = raw;
                }

                info.Values = values;
                return info;
            }
            catch (COMException e)
            {
                AppLog.Warn("selection: " + e.Message);
                error = "proc.noselection";
                return null;
            }
        }

        /// <summary>1 つのセルの値を読む（行の中の列を {{A}} などで使うとき）。</summary>
        public static string ReadCell(dynamic sheet, int row, int col)
        {
            try
            {
                return CellText.ToText(sheet.Cells.Item(row, col).Value2);
            }
            catch (COMException)
            {
                return string.Empty;
            }
        }

        /// <summary>セルに文字を書く。= で始まる文字は数式にならないよう文字のまま入れる。Excel がセルの編集中などで断られたら、少し待って数回やり直す。</summary>
        public static bool WriteCell(dynamic sheet, int row, int col, string text)
        {
            string value = text.StartsWith("=", StringComparison.Ordinal) || text.StartsWith("@", StringComparison.Ordinal) ? "'" + text : text;
            for (int attempt = 0; attempt < 5; attempt++)
            {
                try
                {
                    sheet.Cells.Item(row, col).Value2 = value;
                    return true;
                }
                catch (COMException e)
                {
                    AppLog.Warn("write cell: " + e.Message);
                    Thread.Sleep(150);
                }
            }

            return false;
        }

        /// <summary>セルの文字（空・「#AI!」で始まるものは未処理として扱うため、そのまま返す）。</summary>
        public static string CellValueText(dynamic sheet, int row, int col) => ReadCell(sheet, row, col);

        /// <summary>今選んでいるセル（1 つ）。なければ null。</summary>
        public static dynamic? ActiveCell
        {
            get
            {
                try
                {
                    return App.ActiveCell;
                }
                catch (COMException)
                {
                    return null;
                }
            }
        }
    }
}
