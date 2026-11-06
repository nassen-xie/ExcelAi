using System;
using ExcelDna.Integration;

namespace ExcelAi.Addin
{
    /// <summary>アドインの開始・終了。</summary>
    public sealed class AddInEntry : IExcelAddIn
    {
        public static string VersionText => typeof(AddInEntry).Assembly.GetName().Version.ToString(3);

        public void AutoOpen()
        {
            Tls.Enable();
            // 「自動」の言語は、Excel の表示言語に合わせる（Excel のメイン スレッドの今、調べて覚える）
            string? host = ExcelHost.UiLanguage();
            Loc.HostLanguage = () => host;
            AppLog.Info("AutoOpen v" + VersionText + " tls=" + System.Net.ServicePointManager.SecurityProtocol + " ui=" + (host ?? "?") + "->" + Loc.Language);
            ExcelIntegration.RegisterUnhandledExceptionHandler(e =>
            {
                AppLog.Error("udf unhandled", e as Exception);
                return Loc.T("udf.prefix") + " " + (e as Exception)?.Message;
            });
        }

        public void AutoClose()
        {
            try
            {
                AiGateway.Default.Usage.Flush();
            }
            catch (Exception e)
            {
                AppLog.Error("AutoClose", e);
            }
        }
    }
}
