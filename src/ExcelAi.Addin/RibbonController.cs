using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ExcelDna.Integration;
using ExcelDna.Integration.CustomUI;

namespace ExcelAi.Addin
{
    /// <summary>[AI] タブ（リボン）。ボタンの文字は設定の言語に合わせる。</summary>
    [ComVisible(true)]
    public sealed class RibbonController : ExcelRibbon
    {
        private static CustomTaskPane? pane;

        public override string GetCustomUI(string ribbonId) => @"
<customUI xmlns='http://schemas.microsoft.com/office/2009/07/customui'>
  <ribbon>
    <tabs>
      <tab id='aiTab' label='AI' insertAfterMso='TabHome'>
        <group id='aiGroup' getLabel='GetGroupLabel'>
          <button id='aiProcess' getLabel='GetLabel' getScreentip='GetTip' getImage='GetImage' size='large' onAction='OnProcess' />
          <button id='aiChat' getLabel='GetLabel' getScreentip='GetTip' getImage='GetImage' size='large' onAction='OnChat' />
          <separator id='aiSep' />
          <button id='aiTemplates' getLabel='GetLabel' getScreentip='GetTip' imageMso='FileOpen' size='normal' onAction='OnTemplates' />
          <button id='aiUsage' getLabel='GetLabel' getScreentip='GetTip' imageMso='ChartTypeColumnInsertGallery' size='normal' onAction='OnUsage' />
          <button id='aiSettings' getLabel='GetLabel' getScreentip='GetTip' imageMso='ControlProperties' size='normal' onAction='OnSettings' />
          <button id='aiHelp' getLabel='GetLabel' getScreentip='GetTip' imageMso='Help' size='normal' onAction='OnHelp' />
          <button id='aiAbout' getLabel='GetLabel' getScreentip='GetTip' getImage='GetImage' size='normal' onAction='OnAbout' />
        </group>
      </tab>
    </tabs>
  </ribbon>
  <contextMenus>
    <contextMenu idMso='ContextMenuCell'>
      <button id='aiCtxCell' getLabel='GetLabel' getScreentip='GetTip' getImage='GetImage' onAction='OnProcess' insertBeforeMso='Cut' />
      <menuSeparator id='aiCtxCellSep' insertBeforeMso='Cut' />
    </contextMenu>
    <contextMenu idMso='ContextMenuListRange'>
      <button id='aiCtxList' getLabel='GetLabel' getScreentip='GetTip' getImage='GetImage' onAction='OnProcess' insertBeforeMso='Cut' />
      <menuSeparator id='aiCtxListSep' insertBeforeMso='Cut' />
    </contextMenu>
  </contextMenus>
</customUI>";

        /// <summary>アプリのアイコン（assets/ai-*.png）（ボタンの大きさに合わせて 32 / 64 ピクセルを使う）。</summary>
        public Bitmap GetImage(IRibbonControl control) => Icon(control.Id == "aiProcess" || control.Id == "aiChat" ? 64 : 32);

        private static readonly System.Collections.Generic.Dictionary<int, Bitmap> Icons = new System.Collections.Generic.Dictionary<int, Bitmap>();

        internal static Bitmap Icon(int size)
        {
            lock (Icons)
            {
                if (!Icons.TryGetValue(size, out var bitmap))
                {
                    using (var stream = typeof(RibbonController).Assembly.GetManifestResourceStream("ExcelAi.ai-" + size + ".png")!)
                    {
                        bitmap = new Bitmap(stream);
                    }

                    Icons[size] = bitmap;
                }

                return bitmap;
            }
        }

        public string GetGroupLabel(IRibbonControl control) => Loc.T("rb.group");

        public string GetLabel(IRibbonControl control) => Loc.T(KeyOf(control.Id));

        public string GetTip(IRibbonControl control) => Loc.T(KeyOf(control.Id) + ".tip");

        private static string KeyOf(string id)
        {
            switch (id)
            {
                case "aiProcess":
                    return "rb.process";
                case "aiCtxCell":
                case "aiCtxList":
                    return "rb.ctx";
                case "aiChat":
                    return "rb.chat";
                case "aiTemplates":
                    return "rb.templates";
                case "aiUsage":
                    return "rb.usage";
                case "aiSettings":
                    return "rb.settings";
                case "aiAbout":
                    return "rb.about";
                default:
                    return "rb.help";
            }
        }

        public void OnSettings(IRibbonControl control) => Guard(() => SettingsForm.ShowDialogFor(ExcelHost.Owner));

        public void OnProcess(IRibbonControl control) => Guard(() => ProcessForm.ShowFor(ExcelHost.Owner));

        public void OnChat(IRibbonControl control) => Guard(() =>
        {
            if (pane is null)
            {
                pane = CustomTaskPaneFactory.CreateCustomTaskPane(typeof(ChatControl), Loc.T("chat.title"));
                pane.DockPosition = MsoCTPDockPosition.msoCTPDockPositionRight;
                pane.Width = 420;
            }

            pane.Visible = !pane.Visible;
        });

        public void OnTemplates(IRibbonControl control) => Guard(() =>
        {
            string file = TemplateStore.EnsureUserFile();
            Process.Start(new ProcessStartInfo("notepad.exe", "\"" + file + "\"") { UseShellExecute = true });
            UiKit.Info(ExcelHost.Owner, Loc.T("tpl.opened"));
        });

        public void OnUsage(IRibbonControl control) => Guard(() =>
        {
            var t = AiGateway.Default.Usage.ThisMonth();
            UiKit.Info(ExcelHost.Owner, Loc.F("usage.body", UsageMeter.MonthKey(DateTime.Now), t.Calls, t.InputTokens, t.OutputTokens, t.CacheHits, t.Cost.ToString("0.####")), Loc.T("usage.title"));
        });

        public void OnAbout(IRibbonControl control) => Guard(() => AboutForm.ShowFor(ExcelHost.Owner));

        public void OnHelp(IRibbonControl control) => Guard(() => UiKit.Info(ExcelHost.Owner, Loc.T("help.text"), Loc.T("app.name")));

        // ボタンの処理で出たエラーは、Excel を落とさず画面に出す
        private static void Guard(Action action)
        {
            try
            {
                AppLog.Info("ribbon action: " + action.Method.Name);
                action();
            }
            catch (Exception e)
            {
                AppLog.Error("ribbon", e);
                MessageBox.Show(ExcelHost.Owner, Loc.T("err.other") + "\r\n" + e.Message, Loc.T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
