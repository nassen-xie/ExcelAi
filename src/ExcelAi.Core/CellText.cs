using System;
using System.Globalization;
using System.Text;

namespace ExcelAi
{
    /// <summary>セルの値と文字の変換（列の文字 A・B…と番号、範囲を文字にする、長すぎる文字を切る）。</summary>
    public static class CellText
    {
        public static string ToText(object? value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case string s:
                    return s;
                case double d:
                    return d.ToString("R", CultureInfo.InvariantCulture);
                case bool b:
                    return b ? "TRUE" : "FALSE";
                case DateTime dt:
                    return dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                default:
                    // Excel のエラー値（ExcelError）などは空として扱う
                    return value.GetType().Name == "ExcelError" || value.GetType().Name == "ExcelEmpty" || value.GetType().Name == "ExcelMissing" ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            }
        }

        /// <summary>範囲（2 次元配列）をタブ区切りの文字にする。1 つのセルは値だけ。</summary>
        public static string RangeToText(object? value)
        {
            if (value is object[,] grid)
            {
                int rows = grid.GetLength(0), cols = grid.GetLength(1);
                int r0 = grid.GetLowerBound(0), c0 = grid.GetLowerBound(1);
                var sb = new StringBuilder();
                for (int r = 0; r < rows; r++)
                {
                    if (r > 0)
                    {
                        sb.Append('\n');
                    }

                    for (int c = 0; c < cols; c++)
                    {
                        if (c > 0)
                        {
                            sb.Append('\t');
                        }

                        sb.Append(ToText(grid[r0 + r, c0 + c]));
                    }
                }

                return sb.ToString();
            }

            return ToText(value);
        }

        public static string Limit(string text, int maxChars)
        {
            if (maxChars <= 0 || text.Length <= maxChars)
            {
                return text;
            }

            return text.Substring(0, maxChars);
        }

        /// <summary>1 から始まる列番号 → A, B, …, Z, AA…</summary>
        public static string ColumnLetters(int index)
        {
            var sb = new StringBuilder();
            while (index > 0)
            {
                int rem = (index - 1) % 26;
                sb.Insert(0, (char)('A' + rem));
                index = (index - 1) / 26;
            }

            return sb.ToString();
        }

        /// <summary>A, B, …, AA → 1 から始まる列番号。正しくない文字は 0。</summary>
        public static int ColumnIndex(string letters)
        {
            letters = (letters ?? string.Empty).Trim().ToUpperInvariant();
            if (letters.Length == 0 || letters.Length > 3)
            {
                return 0;
            }

            int result = 0;
            foreach (char ch in letters)
            {
                if (ch < 'A' || ch > 'Z')
                {
                    return 0;
                }

                result = result * 26 + (ch - 'A' + 1);
            }

            return result > 16384 ? 0 : result;
        }
    }
}
