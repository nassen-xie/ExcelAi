using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace ExcelAi.Addin
{
    /// <summary>バージョン情報（名前・版・作者・連絡先・ライセンス・使っている部品）。</summary>
    internal sealed class AboutForm : Form
    {
        public AboutForm()
        {
            Text = Loc.T("about.title");
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(560, 560);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Icon = UiKit.AppIcon;

            var head = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(14, 14, 14, 6) };
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            head.Controls.Add(new PictureBox { Image = RibbonController.Icon(64), SizeMode = PictureBoxSizeMode.Zoom, Size = new Size(64, 64), Margin = new Padding(0, 0, 14, 0) }, 0, 0);

            var info = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Margin = new Padding(0) };
            info.Controls.Add(new Label { Text = Loc.T("app.name"), AutoSize = true, Font = new Font(Font.FontFamily, Font.Size * 1.5f, FontStyle.Bold), Margin = new Padding(0, 0, 0, 2) });
            info.Controls.Add(new Label { Text = Loc.F("about.version", AddInEntry.VersionText), AutoSize = true, Margin = new Padding(0, 0, 0, 2) });
            info.Controls.Add(new Label { Text = AppInfo.Copyright, AutoSize = true, Margin = new Padding(0, 0, 0, 2) });
            info.Controls.Add(Link(Loc.T("about.website") + ": " + AppInfo.Website, AppInfo.Website));
            info.Controls.Add(Link(Loc.T("about.contact") + ": " + AppInfo.Email, "mailto:" + AppInfo.Email));
            info.Controls.Add(new Label { Text = Loc.F("about.license", AppInfo.License), AutoSize = true, Margin = new Padding(0, 4, 0, 2) });
            head.Controls.Add(info, 1, 0);

            var disclaimer = new Label
            {
                Text = Loc.T("about.disclaimer"),
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(532, 0),
                ForeColor = SystemColors.GrayText,
                Padding = new Padding(14, 0, 14, 8),
            };

            var notices = UiKit.Multiline(200, readOnly: true);
            notices.Dock = DockStyle.Fill;
            notices.Font = new Font(FontFamily.GenericMonospace, 8f);
            notices.Text = Resource("ExcelAi.LICENSE.txt") + "\r\n\r\n" + new string('-', 60) + "\r\n\r\n" + Resource("ExcelAi.THIRD-PARTY-NOTICES.txt");
            notices.Select(0, 0);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 8, 10, 8) };
            var close = UiKit.Button(Loc.T("close"), (_, _) => Close());
            buttons.Controls.Add(close);
            buttons.Controls.Add(UiKit.Button(Loc.T("about.logs"), (_, _) => Open(AppPaths.LogDirectory), 120));
            buttons.Controls.Add(UiKit.Button(Loc.T("about.datafolder"), (_, _) => Open(AppPaths.Home), 150));
            AcceptButton = close;
            CancelButton = close;

            var body = new Panel { Dock = DockStyle.Fill, Padding = new Padding(14, 0, 14, 0) };
            body.Controls.Add(notices);
            Controls.Add(body);
            Controls.Add(disclaimer);
            Controls.Add(head);
            Controls.Add(buttons);
        }

        private static LinkLabel Link(string text, string target)
        {
            var link = new LinkLabel { Text = text, AutoSize = true, Margin = new Padding(0, 0, 0, 2) };
            link.LinkClicked += (_, _) => Open(target);
            return link;
        }

        private static void Open(string target)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception e)
            {
                AppLog.Warn("about open: " + e.Message);
            }
        }

        private static string Resource(string name)
        {
            using (var stream = typeof(AboutForm).Assembly.GetManifestResourceStream(name))
            {
                if (stream is null)
                {
                    return string.Empty;
                }

                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    return reader.ReadToEnd().Replace("\r\n", "\n").Replace("\n", "\r\n");
                }
            }
        }

        public static void ShowFor(IWin32Window owner)
        {
            using (var form = new AboutForm())
            {
                form.ShowDialog(owner);
            }
        }
    }
}
