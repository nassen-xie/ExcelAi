using System;
using System.Drawing;
using System.Windows.Forms;

namespace ExcelAi.Addin
{
    /// <summary>画面（WinForms）を作る小さな道具。</summary>
    internal static class UiKit
    {
        private static Icon? appIcon;

        /// <summary>画面のアイコン（assets/ai-*.png）。</summary>
        public static Icon AppIcon => appIcon ??= Icon.FromHandle(RibbonController.Icon(32).GetHicon());

        public static Form NewForm(string title, int width, int height, bool resizable = false)
        {
            return new Form
            {
                Text = title,
                Font = SystemFonts.MessageBoxFont,
                AutoScaleMode = AutoScaleMode.Font,
                ClientSize = new Size(width, height),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = resizable ? FormBorderStyle.Sizable : FormBorderStyle.FixedDialog,
                MaximizeBox = resizable,
                MinimizeBox = false,
                ShowInTaskbar = false,
                Icon = AppIcon,
            };
        }

        public static Label Label(string text, bool wrap = false) =>
            new Label { Text = text, AutoSize = !wrap, Dock = wrap ? DockStyle.Top : DockStyle.None, Anchor = AnchorStyles.Left, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(3, 6, 3, 3) };

        public static Button Button(string text, EventHandler onClick, int width = 100)
        {
            // 文字が切れないよう、幅は文字に合わせて広げる（指定した幅は最小の幅。画面の拡大率・言語が違っても収まる）
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                MinimumSize = new Size(width, 28),
                Padding = new Padding(6, 0, 6, 0),
                UseVisualStyleBackColor = true,
            };
            button.Click += onClick;
            return button;
        }

        public static NumericUpDown Number(int min, int max, int value) =>
            new NumericUpDown { Minimum = min, Maximum = max, Value = Math.Max(min, Math.Min(max, value)), Width = 90 };

        public static TextBox Multiline(int height, bool readOnly = false) =>
            new TextBox { Multiline = true, ScrollBars = ScrollBars.Vertical, Height = height, Dock = DockStyle.Fill, ReadOnly = readOnly, AcceptsReturn = true };

        /// <summary>ラベルと入力欄を並べる 2 列の表。</summary>
        public static TableLayoutPanel Grid()
        {
            var table = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 8, 10, 4) };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return table;
        }

        /// <summary>1 行を足す。ヒントがある場合は、そのラベルを返す（後から文を変えられる）。</summary>
        public static Label? Row(this TableLayoutPanel table, string label, Control control, string? hint = null)
        {
            int row = table.RowCount++;
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            table.Controls.Add(Label(label), 0, row);
            control.Margin = new Padding(3, 3, 3, 3);
            if (hint is null)
            {
                table.Controls.Add(control, 1, row);
                return null;
            }

            var cell = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0) };
            cell.Controls.Add(control);
            var note = new Label { Text = hint, AutoSize = true, MaximumSize = new Size(380, 0), ForeColor = SystemColors.GrayText, Margin = new Padding(3, 0, 3, 4) };
            cell.Controls.Add(note);
            table.Controls.Add(cell, 1, row);
            return note;
        }

        public static void Info(IWin32Window? owner, string text, string? title = null) =>
            MessageBox.Show(owner, text, title ?? Loc.T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Information);

        public static void Warn(IWin32Window? owner, string text, string? title = null) =>
            MessageBox.Show(owner, text, title ?? Loc.T("app.name"), MessageBoxButtons.OK, MessageBoxIcon.Warning);

        public static bool Ask(IWin32Window? owner, string text, string? title = null) =>
            MessageBox.Show(owner, text, title ?? Loc.T("app.name"), MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK;
    }
}
