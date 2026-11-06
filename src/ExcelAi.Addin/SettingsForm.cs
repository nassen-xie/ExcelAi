using System;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ExcelAi.Addin
{
    /// <summary>設定の画面（API キー・モデル・動作）。API キーは各自で入力する。</summary>
    internal sealed class SettingsForm : Form
    {
        private readonly AiSettings working = SettingsStore.Current.Clone();
        private readonly TextBox apiKey = new TextBox { UseSystemPasswordChar = true, Width = 330 };
        private readonly CheckBox show = new CheckBox { AutoSize = true };
        private readonly ComboBox provider = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
        private readonly ComboBox model = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Width = 330 };
        private readonly Label modelNote = new Label { AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 4) };
        // サービスを切り替えても、入力途中のキーを失わないように、サービスごとに持つ
        private readonly System.Collections.Generic.Dictionary<string, string> keyEdits = new System.Collections.Generic.Dictionary<string, string>();
        private string shownProvider = string.Empty;
        private Label? apiKeyHint;
        private Label? baseUrlHint;
        private Label? effortHint;
        private readonly ComboBox effort = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
        private readonly NumericUpDown maxTokens = UiKit.Number(256, 128000, 4096);
        private readonly NumericUpDown concurrency = UiKit.Number(1, 16, 4);
        private readonly NumericUpDown timeout = UiKit.Number(10, 900, 120);
        private readonly CheckBox cache = new CheckBox { AutoSize = true };
        private readonly NumericUpDown cacheDays = UiKit.Number(1, 365, 30);
        private readonly NumericUpDown cellChars = UiKit.Number(100, 200000, 8000);
        private readonly ComboBox language = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly TextBox baseUrl = new TextBox { Width = 330 };
        private readonly TextBox system = UiKit.Multiline(70);
        private readonly CheckBox confirmBatch = new CheckBox { AutoSize = true };
        private readonly CheckBox consent = new CheckBox { AutoSize = true };
        private readonly Label testResult = new Label { AutoSize = true, MaximumSize = new Size(400, 0), Margin = new Padding(3, 6, 3, 3) };
        private readonly Button testButton;
        private CancellationTokenSource? testCancel;

        public SettingsForm()
        {
            Text = Loc.T("set.title");
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(640, 780);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Icon = UiKit.AppIcon;

            var table = UiKit.Grid();
            show.Text = Loc.T("set.show");
            show.CheckedChanged += (_, _) => apiKey.UseSystemPasswordChar = !show.Checked;
            var keyPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            keyPanel.Controls.Add(apiKey);
            keyPanel.Controls.Add(show);
            foreach (var p in Providers.All)
            {
                provider.Items.Add(p);
            }

            provider.SelectedIndexChanged += (_, _) => OnProviderChanged();
            table.Row(Loc.T("set.provider"), provider, Loc.T("set.provider.hint"));
            apiKeyHint = table.Row(Loc.T("set.apikey"), keyPanel, " ");

            testButton = UiKit.Button(Loc.T("set.test"), async (_, _) => await TestAsync(), 120);
            var testPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0) };
            testPanel.Controls.Add(testButton);
            testPanel.Controls.Add(testResult);
            table.Row(string.Empty, testPanel);

            model.TextChanged += (_, _) => ShowModelNote();
            var modelCell = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            modelCell.Controls.Add(model);
            modelCell.Controls.Add(modelNote);
            table.Row(Loc.T("set.model"), modelCell, Loc.T("set.model.hint"));
            effort.Items.AddRange(new object[] { "low", "medium", "high", "max" });
            effortHint = table.Row(Loc.T("set.effort"), effort, Loc.T("set.effort.hint"));
            table.Row(Loc.T("set.maxtokens"), maxTokens);
            table.Row(Loc.T("set.concurrency"), concurrency);
            table.Row(Loc.T("set.timeout"), timeout);

            cache.Text = Loc.T("set.cache");
            table.Row(string.Empty, cache);
            var cachePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false, Margin = new Padding(0) };
            cachePanel.Controls.Add(cacheDays);
            cachePanel.Controls.Add(UiKit.Button(Loc.T("set.clearcache"), (_, _) =>
            {
                AiGateway.Default.Cache.Clear();
                UiKit.Info(this, Loc.T("set.cache.cleared"));
            }, 130));
            table.Row(Loc.T("set.cachedays"), cachePanel);
            table.Row(Loc.T("set.cellchars"), cellChars);

            language.Items.Add(new Choice("auto", Loc.T("lang.auto")));
            language.Items.Add(new Choice("ja", "日本語"));
            language.Items.Add(new Choice("zh", "中文"));
            language.Items.Add(new Choice("en", "English"));
            table.Row(Loc.T("set.language"), language);
            baseUrlHint = table.Row(Loc.T("set.baseurl"), baseUrl, " ");
            table.Row(Loc.T("set.system"), system);
            confirmBatch.Text = Loc.T("set.confirmbatch");
            table.Row(string.Empty, confirmBatch);
            consent.Text = Loc.T("set.consent");
            table.Row(string.Empty, consent);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 46, Padding = new Padding(10, 8, 10, 8) };
            var cancel = UiKit.Button(Loc.T("cancel"), (_, _) => Close());
            var ok = UiKit.Button(Loc.T("ok"), (_, _) => Save());
            buttons.Controls.Add(cancel);
            buttons.Controls.Add(ok);
            AcceptButton = ok;
            CancelButton = cancel;

            var scroll = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(table);
            Controls.Add(scroll);
            Controls.Add(buttons);
            Load += (_, _) => Fill();
            FormClosed += (_, _) => testCancel?.Cancel();
        }

        private void Fill()
        {
            shownProvider = working.Provider;
            provider.SelectedItem = Providers.Find(working.Provider);
            apiKey.Text = working.ApiKey;
            FillModels(working.ProviderInfo);
            model.Text = working.Model;
            baseUrl.Text = working.BaseUrl;
            ShowProviderHints(working.ProviderInfo);
            effort.SelectedItem = working.Effort;
            maxTokens.Value = working.MaxTokens;
            concurrency.Value = working.MaxConcurrency;
            timeout.Value = working.TimeoutSeconds;
            cache.Checked = working.UseCache;
            cacheDays.Value = working.CacheDays;
            cellChars.Value = working.CellCharLimit;
            language.SelectedIndex = Math.Max(0, language.Items.Cast<Choice>().ToList().FindIndex(c => c.Id == working.Language));
            system.Text = working.SystemPrompt;
            confirmBatch.Checked = working.ConfirmBatch;
            consent.Checked = working.ConsentAccepted;
        }

        private AiSettings Collect()
        {
            var s = working.Clone();
            keyEdits[shownProvider] = apiKey.Text;
            foreach (var edit in keyEdits)
            {
                s.SetKey(edit.Key, edit.Value);
            }

            s.Provider = ((ProviderInfo)provider.SelectedItem!).Id;
            s.Model = model.Text.Trim();
            s.Effort = (string)effort.SelectedItem! ;
            s.MaxTokens = (int)maxTokens.Value;
            s.MaxConcurrency = (int)concurrency.Value;
            s.TimeoutSeconds = (int)timeout.Value;
            s.UseCache = cache.Checked;
            s.CacheDays = (int)cacheDays.Value;
            s.CellCharLimit = (int)cellChars.Value;
            s.Language = ((Choice)language.SelectedItem!).Id;
            s.BaseUrl = baseUrl.Text.Trim();
            s.SystemPrompt = system.Text;
            s.ConfirmBatch = confirmBatch.Checked;
            if (consent.Checked && !s.ConsentAccepted)
            {
                s.ConsentAccepted = true;
                s.ConsentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            }
            else if (!consent.Checked)
            {
                s.ConsentAccepted = false;
            }

            return s;
        }

        private void Save()
        {
            var s = Collect();
            if (s.EffectiveApiKey.Length > 0 && !s.ConsentAccepted)
            {
                // キーを入れたのに同意がない場合は、その場で確認する
                if (!ConsentDialog.Ask(this))
                {
                    return;
                }

                s.ConsentAccepted = true;
                s.ConsentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            }

            SettingsStore.Save(s);
            UiKit.Info(this, Loc.T("set.saved"));
            DialogResult = DialogResult.OK;
            Close();
        }

        private async System.Threading.Tasks.Task TestAsync()
        {
            var s = Collect();
            testButton.Enabled = false;
            testResult.ForeColor = SystemColors.ControlText;
            testResult.Text = Loc.T("set.testing");
            testCancel = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            try
            {
                var llm = new RoutingLlm(() => s);
                var request = new LlmRequest { Model = s.Model, System = "Reply with exactly: OK", Messages = new[] { new ChatMessage(true, "ping") }, MaxTokens = 512, Effort = "low" };
                var result = await llm.CompleteAsync(request, testCancel.Token);
                testResult.ForeColor = Color.DarkGreen;
                testResult.Text = Loc.F("set.test.ok", Models.DisplayName(s.Model) + ", " + result.InputTokens + "+" + result.OutputTokens + " tokens");
            }
            catch (AiException e)
            {
                testResult.ForeColor = Color.Firebrick;
                testResult.Text = e.UserMessage;
            }
            catch (Exception e)
            {
                testResult.ForeColor = Color.Firebrick;
                testResult.Text = e.Message;
            }
            finally
            {
                testButton.Enabled = true;
            }
        }

        // サービスを変えたとき：キー・モデル・接続先の欄と案内を、そのサービスのものにする
        private void OnProviderChanged()
        {
            if (!(provider.SelectedItem is ProviderInfo next) || shownProvider.Length == 0 || next.Id == shownProvider)
            {
                return;
            }

            keyEdits[shownProvider] = apiKey.Text;
            shownProvider = next.Id;
            apiKey.Text = keyEdits.TryGetValue(next.Id, out var typed) ? typed : working.GetKey(next.Id);
            FillModels(next);
            model.Text = next.DefaultModel;
            baseUrl.Text = string.Empty;
            ShowProviderHints(next);
        }

        private void FillModels(ProviderInfo info)
        {
            model.Items.Clear();
            foreach (var m in info.Models)
            {
                model.Items.Add(m.Id);
            }
        }

        private void ShowProviderHints(ProviderInfo info)
        {
            if (apiKeyHint != null)
            {
                apiKeyHint.Text = Loc.F("set.apikey.hint", info.Display, info.KeyUrl.Length > 0 ? info.KeyUrl : "—");
            }

            if (baseUrlHint != null)
            {
                baseUrlHint.Text = Loc.F("set.baseurl.hint", info.BaseUrl.Length > 0 ? info.BaseUrl : (info.NeedsBaseUrl ? "—" : "SDK"));
            }

            effort.Enabled = info.SupportsEffort;
            if (effortHint != null)
            {
                effortHint.Text = Loc.T("set.effort.hint") + (info.SupportsEffort ? string.Empty : "  (Claude only)");
            }

            ShowModelNote();
        }

        private void ShowModelNote()
        {
            var info = Models.Find(model.Text.Trim());
            modelNote.Text = info is null
                ? string.Empty
                : $"{info.Display}  —  {Loc.T(info.NoteKey)}" + (info.PriceKnown ? $"  (${info.InputPerMillion:0.###} / ${info.OutputPerMillion:0.###} per 1M)" : string.Empty);
        }

        private sealed class Choice
        {
            public Choice(string id, string text)
            {
                Id = id;
                Text = text;
            }

            public string Id { get; }

            public string Text { get; }

            public override string ToString() => Text;
        }

        /// <summary>この画面を開く。</summary>
        public static void ShowDialogFor(IWin32Window owner)
        {
            AppLog.Info("settings: creating");
            using (var form = new SettingsForm())
            {
                AppLog.Info("settings: showing");
                form.ShowDialog(owner);
                AppLog.Info("settings: closed");
            }
        }
    }

    internal static class ConsentDialog
    {
        /// <summary>データ送信の同意を確認する。同意した場合は設定に記録して true。</summary>
        public static bool Ask(IWin32Window? owner)
        {
            using (var form = UiKit.NewForm(Loc.T("consent.title"), 520, 330))
            {
                var body = UiKit.Multiline(200, readOnly: true);
                body.Text = Loc.T("consent.body");
                body.Dock = DockStyle.Fill;
                var accept = UiKit.Button(Loc.T("consent.accept"), (_, _) => { form.DialogResult = DialogResult.OK; form.Close(); }, 160);
                var decline = UiKit.Button(Loc.T("cancel"), (_, _) => { form.DialogResult = DialogResult.Cancel; form.Close(); });
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 46, Padding = new Padding(10, 8, 10, 8) };
                buttons.Controls.Add(decline);
                buttons.Controls.Add(accept);
                form.Controls.Add(body);
                form.Controls.Add(buttons);
                form.AcceptButton = accept;
                form.CancelButton = decline;
                if (form.ShowDialog(owner) != DialogResult.OK)
                {
                    return false;
                }
            }

            var settings = SettingsStore.Current.Clone();
            settings.ConsentAccepted = true;
            settings.ConsentDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            SettingsStore.Save(settings);
            return true;
        }

        /// <summary>キーと同意がそろっているか。そろっていない場合は、画面で案内してそろえる。</summary>
        public static bool EnsureReady(IWin32Window owner)
        {
            var s = SettingsStore.Current;
            if (s.EffectiveApiKey.Length == 0)
            {
                UiKit.Warn(owner, Loc.T("err.nokey"));
                SettingsForm.ShowDialogFor(owner);
                s = SettingsStore.Current;
                if (s.EffectiveApiKey.Length == 0)
                {
                    return false;
                }
            }

            return s.ConsentAccepted || Ask(owner);
        }
    }
}
