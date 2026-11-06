using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace ExcelAi.Addin
{
    /// <summary>作業ウィンドウのチャット。選択範囲を添付して相談でき、回答をセルに入れられる。</summary>
    [System.Runtime.InteropServices.ComVisible(true)]
    [System.Runtime.InteropServices.ProgId("ExcelAi.ChatControl")]
    public class ChatControl : UserControl
    {
        private const int AttachCellLimit = 2000;

        private readonly RichTextBox transcript = new RichTextBox { Dock = DockStyle.Fill, ReadOnly = true, BackColor = SystemColors.Window, DetectUrls = true, ScrollBars = RichTextBoxScrollBars.Vertical };
        private readonly TextBox input = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
        private readonly ComboBox model = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Fill };
        private readonly CheckBox attach = new CheckBox { AutoSize = true, Checked = true };
        private readonly Button send;
        private readonly Button stop;
        private readonly Button insert;
        private readonly Button copy;
        private readonly Button clear;
        private readonly List<ChatMessage> history = new List<ChatMessage>();
        private CancellationTokenSource? cancel;
        private string lastAnswer = string.Empty;

        public ChatControl()
        {
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            Dock = DockStyle.Fill;

            FillModels();
            SettingsStore.Changed += OnSettingsChanged;
            attach.Text = Loc.T("chat.attach");
            var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 70, ColumnCount = 1, RowCount = 2, Padding = new Padding(6, 6, 6, 0) };
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            top.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            top.Controls.Add(model, 0, 0);
            top.Controls.Add(attach, 0, 1);

            send = UiKit.Button(Loc.T("chat.send"), async (_, _) => await SendAsync(), 120);
            stop = UiKit.Button(Loc.T("chat.stop"), (_, _) => cancel?.Cancel(), 70);
            stop.Enabled = false;
            insert = UiKit.Button(Loc.T("chat.insert"), (_, _) => InsertLastAnswer(), 110);
            copy = UiKit.Button(Loc.T("chat.copy"), (_, _) => CopyLastAnswer(), 70);
            clear = UiKit.Button(Loc.T("chat.clear"), (_, _) => Reset(), 70);
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, FlowDirection = FlowDirection.LeftToRight, WrapContents = true, Padding = new Padding(4, 4, 4, 2) };
            foreach (var b in new[] { send, stop, insert, copy, clear })
            {
                buttons.Controls.Add(b);
            }

            var bottom = new Panel { Dock = DockStyle.Bottom, Height = 190 };
            input.Dock = DockStyle.Fill;
            bottom.Controls.Add(input);
            bottom.Controls.Add(buttons);

            Controls.Add(transcript);
            Controls.Add(bottom);
            Controls.Add(top);
            input.KeyDown += (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    send.PerformClick();
                }
            };
            Append(Loc.T("chat.hint") + "\n", SystemColors.GrayText, italic: true);
        }

        private void Append(string text, Color color, bool bold = false, bool italic = false)
        {
            transcript.SelectionStart = transcript.TextLength;
            transcript.SelectionLength = 0;
            transcript.SelectionColor = color;
            transcript.SelectionFont = new Font(transcript.Font, (bold ? FontStyle.Bold : FontStyle.Regular) | (italic ? FontStyle.Italic : FontStyle.Regular));
            transcript.AppendText(text);
            transcript.SelectionStart = transcript.TextLength;
            transcript.ScrollToCaret();
        }

        // 今のサービスのモデルを並べ、設定のモデルを選ぶ（一覧にない名前も入力できる）
        private void FillModels()
        {
            var settings = SettingsStore.Current;
            model.Items.Clear();
            foreach (var m in settings.ProviderInfo.Models)
            {
                model.Items.Add(m.Id);
            }

            model.Text = settings.Model;
        }

        private void OnSettingsChanged(object? sender, EventArgs e)
        {
            if (IsDisposed || cancel != null)
            {
                return;
            }

            if (InvokeRequired)
            {
                BeginInvoke(new Action(FillModels));
            }
            else
            {
                FillModels();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SettingsStore.Changed -= OnSettingsChanged;
            }

            base.Dispose(disposing);
        }

        private void Reset()
        {
            cancel?.Cancel();
            history.Clear();
            lastAnswer = string.Empty;
            transcript.Clear();
        }

        private async System.Threading.Tasks.Task SendAsync()
        {
            string text = input.Text.Trim();
            if (text.Length == 0 || cancel is not null)
            {
                return;
            }

            if (!ConsentDialog.EnsureReady(this.ParentForm ?? (IWin32Window)ExcelHost.Owner))
            {
                return;
            }

            string body = text;
            string note = string.Empty;
            if (attach.Checked && ExcelHost.ReadSelection(out _) is { Rows: > 0 } selection)
            {
                string tsv = SelectionText(selection);
                if (tsv.Length > 0)
                {
                    body = text + "\n\n[" + selection.Address + "]\n" + CellText.Limit(tsv, SettingsStore.Current.CellCharLimit * 3);
                    note = " " + Loc.F("chat.attached", selection.Address);
                }
            }

            history.Add(new ChatMessage(true, body));
            input.Clear();
            Append("\n" + Loc.T("chat.you") + ": ", Color.RoyalBlue, bold: true);
            Append(text + note + "\n", SystemColors.WindowText);
            Append(Loc.T("chat.ai") + ": ", Color.DarkGreen, bold: true);
            SetBusy(true);
            cancel = new CancellationTokenSource();
            var answer = new StringBuilder();
            try
            {
                var gateway = AiGateway.Default;
                var request = gateway.NewRequest(Loc.T("chat.system"), history.ToList(), model.Text.Trim());
                request.MaxTokens = Math.Max(request.MaxTokens, 8192);
                var result = await gateway.StreamAsync(request, delta =>
                {
                    answer.Append(delta);
                    BeginInvoke(new Action(() => Append(delta, SystemColors.WindowText)));
                }, cancel.Token);
                lastAnswer = result.Text.Trim();
                history.Add(new ChatMessage(false, result.Text));
                if (result.Truncated)
                {
                    Append(" " + Loc.T("warn.truncated"), Color.DarkOrange);
                }

                Append("\n", SystemColors.WindowText);
            }
            catch (AiException e)
            {
                // 失敗した依頼は履歴から外す（次の送信で同じ質問が二重にならないように）
                history.RemoveAt(history.Count - 1);
                Append("\n" + e.UserMessage + "\n", e.Kind == AiErrorKind.Cancelled ? Color.DarkOrange : Color.Firebrick);
            }
            finally
            {
                cancel.Dispose();
                cancel = null;
                SetBusy(false);
            }
        }

        private static string SelectionText(SelectionInfo selection)
        {
            if (selection.Rows * selection.Cols > AttachCellLimit)
            {
                selection.Rows = Math.Max(1, AttachCellLimit / Math.Max(1, selection.Cols));
            }

            var sb = new StringBuilder();
            for (int r = 1; r <= selection.Rows; r++)
            {
                if (r > 1)
                {
                    sb.Append('\n');
                }

                sb.Append(string.Join("\t", Enumerable.Range(1, selection.Cols).Select(c => selection.Text(r, c))));
            }

            return sb.ToString();
        }

        private void SetBusy(bool busy)
        {
            send.Enabled = !busy;
            stop.Enabled = busy;
            model.Enabled = !busy;
        }

        private void CopyLastAnswer()
        {
            if (lastAnswer.Length > 0)
            {
                Clipboard.SetText(lastAnswer);
            }
        }

        // 回答の行を、今のセルから下に 1 行ずつ入れる
        private void InsertLastAnswer()
        {
            if (lastAnswer.Length == 0)
            {
                UiKit.Info(ExcelHost.Owner, Loc.T("chat.nothingtoinsert"));
                return;
            }

            dynamic? cell = ExcelHost.ActiveCell;
            if (cell is null)
            {
                return;
            }

            var lines = lastAnswer.Replace("\r\n", "\n").Split('\n').Take(1000).ToList();
            int row = (int)cell.Row, col = (int)cell.Column;
            dynamic sheet = cell.Worksheet;
            for (int i = 0; i < lines.Count; i++)
            {
                if (!ExcelHost.WriteCell(sheet, row + i, col, lines[i]))
                {
                    UiKit.Warn(ExcelHost.Owner, Loc.T("proc.writefail"));
                    return;
                }
            }
        }
    }
}
