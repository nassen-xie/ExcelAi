using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ExcelAi.Addin
{
    /// <summary>選んだセルを AI で 1 件ずつ処理して、結果をシートに書き込む画面。</summary>
    internal sealed class ProcessForm : Form
    {
        private readonly SelectionInfo selection;
        private readonly ComboBox template = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill };
        private readonly TextBox prompt = UiKit.Multiline(110);
        private readonly TextBox system = UiKit.Multiline(46);
        private readonly RadioButton unitCell = new RadioButton { AutoSize = true, Checked = true };
        private readonly RadioButton unitRow = new RadioButton { AutoSize = true };
        private readonly RadioButton outRight = new RadioButton { AutoSize = true, Checked = true };
        private readonly RadioButton outColumn = new RadioButton { AutoSize = true };
        private readonly TextBox outColumnLetter = new TextBox { Width = 50, Text = "Z" };
        private readonly RadioButton outReplace = new RadioButton { AutoSize = true };
        private readonly CheckBox skipEmpty = new CheckBox { AutoSize = true, Checked = true };
        private readonly CheckBox skipDone = new CheckBox { AutoSize = true, Checked = true };
        private readonly NumericUpDown concurrency;
        private readonly Label estimate = new Label { AutoSize = true, MaximumSize = new Size(640, 0) };
        private readonly ProgressBar progress = new ProgressBar { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
        private readonly Label status = new Label { AutoSize = true, MaximumSize = new Size(640, 0) };
        private readonly ListBox errors = new ListBox { Dock = DockStyle.Fill, HorizontalScrollbar = true, IntegralHeight = false };
        private readonly Button run;
        private readonly Button stop;
        private readonly Button close;
        private readonly System.Windows.Forms.Timer drain = new System.Windows.Forms.Timer { Interval = 150 };
        private readonly ConcurrentQueue<(BatchResult Result, BatchProgress Progress)> queue = new ConcurrentQueue<(BatchResult, BatchProgress)>();

        private CancellationTokenSource? cancel;
        private List<(int Row, int Col)> targets = new List<(int, int)>();
        private bool running;
        private int written;
        private int failed;
        private BatchProgress last = new BatchProgress();
        private UsageTotals usageBefore = new UsageTotals();

        public ProcessForm(SelectionInfo selection)
        {
            this.selection = selection;
            var settings = SettingsStore.Current;
            concurrency = UiKit.Number(1, 16, settings.MaxConcurrency);
            Text = Loc.T("proc.title");
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(700, 700);
            MinimumSize = new Size(640, 620);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Icon = UiKit.AppIcon;

            var table = UiKit.Grid();
            table.Dock = DockStyle.Top;
            table.Row(Loc.T("proc.template"), template);
            table.Row(Loc.T("proc.prompt"), prompt, Loc.T("proc.placeholders"));
            table.Row(Loc.T("proc.system"), system);

            unitCell.Text = Loc.T("proc.unit.cell");
            unitRow.Text = Loc.T("proc.unit.row");
            var unit = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0) };
            unit.Controls.Add(unitCell);
            unit.Controls.Add(unitRow);
            table.Row(Loc.T("proc.unit"), unit);

            outRight.Text = Loc.T("proc.out.right");
            outColumn.Text = Loc.T("proc.out.column");
            outReplace.Text = Loc.T("proc.out.replace");
            var output = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Margin = new Padding(0) };
            output.Controls.Add(outRight);
            output.Controls.Add(outColumn);
            output.Controls.Add(outColumnLetter);
            output.Controls.Add(outReplace);
            table.Row(Loc.T("proc.output"), output);

            skipEmpty.Text = Loc.T("proc.skipempty");
            skipDone.Text = Loc.T("proc.skipdone");
            var skips = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Margin = new Padding(0) };
            skips.Controls.Add(skipEmpty);
            skips.Controls.Add(skipDone);
            table.Row(string.Empty, skips);
            table.Row(Loc.T("proc.concurrency"), concurrency);
            table.Row(Loc.T("proc.range"), new Label { Text = selection.Rows == 0 ? "-" : selection.Address, AutoSize = true, Margin = new Padding(3, 6, 3, 3) });

            run = UiKit.Button(Loc.T("proc.start"), async (_, _) => await RunAsync());
            stop = UiKit.Button(Loc.T("proc.stop"), (_, _) => cancel?.Cancel());
            stop.Enabled = false;
            close = UiKit.Button(Loc.T("close"), (_, _) => Close());
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 46, Padding = new Padding(10, 8, 10, 8) };
            buttons.Controls.Add(close);
            buttons.Controls.Add(stop);
            buttons.Controls.Add(run);

            var bottom = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(10, 0, 10, 0) };
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            bottom.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            bottom.Controls.Add(estimate, 0, 0);
            bottom.Controls.Add(progress, 0, 1);
            bottom.Controls.Add(status, 0, 2);
            bottom.Controls.Add(errors, 0, 3);

            Controls.Add(bottom);
            Controls.Add(table);
            Controls.Add(buttons);
            bottom.BringToFront();
            AcceptButton = null;

            foreach (var t in TemplateStore.All())
            {
                template.Items.Add(t);
            }

            template.Items.Add(new PromptTemplate { Id = string.Empty, NameKey = "tpl.custom" });
            template.SelectedIndexChanged += (_, _) => ApplyTemplate();
            template.SelectedIndex = 0;

            foreach (Control c in new Control[] { prompt, system, outColumnLetter })
            {
                c.TextChanged += (_, _) => UpdateEstimate();
            }

            foreach (var radio in new[] { unitCell, unitRow, outRight, outColumn, outReplace })
            {
                radio.CheckedChanged += (_, _) => OnModeChanged();
            }

            skipEmpty.CheckedChanged += (_, _) => UpdateEstimate();
            skipDone.CheckedChanged += (_, _) => UpdateEstimate();
            drain.Tick += (_, _) => Drain();
            FormClosing += (_, e) =>
            {
                if (running)
                {
                    e.Cancel = true;
                    cancel?.Cancel();
                }
            };
            OnModeChanged();
        }

        private void ApplyTemplate()
        {
            if (template.SelectedItem is PromptTemplate t && t.Id.Length > 0)
            {
                prompt.Text = t.Prompt.Replace("\n", "\r\n");
                system.Text = t.System;
            }
        }

        private void OnModeChanged()
        {
            // 行ごとの処理では「元のセルを置き換える」は使えない（複数のセルが 1 つの結果になるため）
            outReplace.Enabled = unitCell.Checked;
            if (!unitCell.Checked && outReplace.Checked)
            {
                outRight.Checked = true;
            }

            outColumnLetter.Enabled = outColumn.Checked;
            UpdateEstimate();
        }

        private void UpdateEstimate()
        {
            if (running)
            {
                return;
            }

            try
            {
                var plan = Plan(forEstimate: true);
                if (plan.Error is not null)
                {
                    estimate.Text = plan.Error;
                    return;
                }

                var settings = SettingsStore.Current;
                decimal cost = BatchRunner.EstimateCost(plan.Items, system.Text.Trim().Length > 0 ? system.Text : settings.SystemPrompt, settings);
                estimate.Text = Loc.F("proc.estimate", plan.Items.Count, Models.CostText(settings.Model, cost), Models.DisplayName(settings.Model)) + "  " + Loc.T("proc.estimate.note");
            }
            catch (Exception e)
            {
                AppLog.Warn("estimate: " + e.Message);
                estimate.Text = string.Empty;
            }
        }

        private ProcessOptions Options() => new ProcessOptions
        {
            Template = prompt.Text,
            System = system.Text,
            RowMode = unitRow.Checked,
            Output = outReplace.Checked ? OutputMode.Replace : outColumn.Checked ? OutputMode.Column : OutputMode.Right,
            ColumnLetter = outColumnLetter.Text,
            SkipEmpty = skipEmpty.Checked,
            SkipDone = skipDone.Checked,
        };

        private ProcessPlan Plan(bool forEstimate) => ProcessEngine.Build(selection, Options(), SettingsStore.Current, forEstimate);

        private async System.Threading.Tasks.Task RunAsync()
        {
            if (running)
            {
                return;
            }

            if (prompt.Text.Trim().Length == 0)
            {
                UiKit.Warn(this, Loc.T("proc.prompt.empty"));
                return;
            }

            if (!ConsentDialog.EnsureReady(this))
            {
                return;
            }

            var plan = Plan(forEstimate: false);
            if (plan.Error is not null)
            {
                UiKit.Warn(this, plan.Error);
                return;
            }

            if (plan.Items.Count == 0)
            {
                UiKit.Info(this, Loc.F("proc.estimate", 0, "0", Models.DisplayName(SettingsStore.Current.Model)));
                return;
            }

            var settings = SettingsStore.Current;
            decimal cost = BatchRunner.EstimateCost(plan.Items, system.Text.Trim().Length > 0 ? system.Text : settings.SystemPrompt, settings);
            if ((settings.ConfirmBatch || plan.Items.Count >= 50) && !UiKit.Ask(this, Loc.F("proc.confirm", plan.Items.Count, cost.ToString("0.####"))))
            {
                return;
            }

            if (outReplace.Checked && !UiKit.Ask(this, Loc.T("proc.replace.warn")))
            {
                return;
            }

            // 実行
            targets = plan.Targets;
            running = true;
            written = 0;
            failed = 0;
            last = new BatchProgress { Total = plan.Items.Count };
            errors.Items.Clear();
            progress.Value = 0;
            SetBusy(true);
            usageBefore = AiGateway.Default.Usage.ThisMonth();
            cancel = new CancellationTokenSource();
            bool quiet = plan.Items.Count > 200;
            dynamic app = ExcelHost.App;
            bool screenUpdating = true;
            try
            {
                if (quiet)
                {
                    screenUpdating = (bool)app.ScreenUpdating;
                    app.ScreenUpdating = false;
                }
            }
            catch (System.Runtime.InteropServices.COMException)
            {
            }

            drain.Start();
            try
            {
                int concurrencyValue = (int)concurrency.Value;
                var gateway = AiGateway.Default;
                if (concurrencyValue != settings.MaxConcurrency)
                {
                    var changed = settings.Clone();
                    changed.MaxConcurrency = concurrencyValue;
                    SettingsStore.Save(changed);
                }

                await BatchRunner.RunAsync(plan.Items, gateway, (result, p) => queue.Enqueue((result, p)), cancel.Token);
            }
            catch (Exception e)
            {
                AppLog.Error("batch", e);
                errors.Items.Add(e.Message);
            }
            finally
            {
                drain.Stop();
                Drain();
                try
                {
                    if (quiet)
                    {
                        app.ScreenUpdating = screenUpdating;
                    }
                }
                catch (System.Runtime.InteropServices.COMException)
                {
                }

                AiGateway.Default.Usage.Flush();
                running = false;
                SetBusy(false);
                var after = AiGateway.Default.Usage.ThisMonth();
                status.Text = Loc.F("proc.done", written - failed, failed, (after.Cost - usageBefore.Cost).ToString("0.####"));
                cancel.Dispose();
                cancel = null;
            }
        }

        // 結果を Excel に書く（Excel のメイン スレッドで、少しずつ）
        private void Drain()
        {
            int guard = 0;
            while (queue.TryDequeue(out var item) && guard++ < 400)
            {
                var (result, p) = item;
                last = p;
                string? text = ProcessEngine.ResultText(result);
                if (text is null)
                {
                    continue;
                }

                var (row, col) = targets[result.Index];
                if (!ExcelHost.WriteCell(selection.Sheet, row, col, text))
                {
                    errors.Items.Add($"{CellText.ColumnLetters(col)}{row}: {Loc.T("proc.writefail")}");
                    failed++;
                }
                else if (!result.Ok)
                {
                    errors.Items.Add($"{CellText.ColumnLetters(col)}{row}: {result.Error!.UserMessage}");
                    failed++;
                }

                written++;
            }

            progress.Maximum = Math.Max(1, last.Total);
            progress.Value = Math.Min(progress.Maximum, last.Done);
            status.Text = Loc.F("proc.progress", last.Done, last.Total, failed);
        }

        private void SetBusy(bool busy)
        {
            run.Enabled = !busy;
            stop.Enabled = busy;
            close.Enabled = !busy;
            foreach (Control c in new Control[] { template, prompt, system, unitCell, unitRow, outRight, outColumn, outColumnLetter, outReplace, skipEmpty, skipDone, concurrency })
            {
                c.Enabled = !busy;
            }

            if (!busy)
            {
                OnModeChanged();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                drain.Dispose();
                cancel?.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>選んでいる範囲を読んで、画面を開く。</summary>
        public static void ShowFor(IWin32Window owner)
        {
            var info = ExcelHost.ReadSelection(out string? error);
            if (info is null || info.Rows == 0)
            {
                UiKit.Warn(owner, Loc.T(error ?? "proc.noselection"));
                return;
            }

            if (!ConsentDialog.EnsureReady(owner))
            {
                return;
            }

            using (var form = new ProcessForm(info))
            {
                form.ShowDialog(owner);
            }
        }
    }
}
