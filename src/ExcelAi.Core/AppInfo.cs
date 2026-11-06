namespace ExcelAi
{
    /// <summary>作者・連絡先・ライセンス（「バージョン情報」・ファイルのプロパティ・文書に出す。固有名詞なので翻訳しない）。</summary>
    public static class AppInfo
    {
        public const string Company = "株式会社ナッセンシステム";

        public const string Email = "contact@nassen.co.jp";

        public const string Website = "https://nassen.co.jp";

        public const int CopyrightYear = 2026;

        public const string License = "MIT";

        public static string Copyright => "Copyright © " + CopyrightYear + " " + Company;
    }
}
