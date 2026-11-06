using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace UiPreview
{
    internal static class Program
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static int Main(string[] args)
        {
            string outDir = args.Length > 0 ? args[0] : Path.Combine(Path.GetTempPath(), "excelai-ui");
            Directory.CreateDirectory(outDir);
            string home = Path.Combine(Path.GetTempPath(), "excelai-ui-home");
            Directory.CreateDirectory(home);
            Environment.SetEnvironmentVariable("EXCELAI_HOME", home);
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            var addin = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "ExcelAi.Addin.dll"));
            var core = Assembly.LoadFrom(Path.Combine(AppContext.BaseDirectory, "ExcelAi.Core.dll"));
            var loc = core.GetType("ExcelAi.Loc")!;
            var overrideProperty = loc.GetProperty("Override")!;
            foreach (string lang in new[] { "ja", "zh", "en" })
            {
                overrideProperty.SetValue(null, lang);
                Snap(addin.GetType("ExcelAi.Addin.SettingsForm")!, Array.Empty<object>(), Path.Combine(outDir, "settings-" + lang + ".png"));
                Snap(addin.GetType("ExcelAi.Addin.AboutForm")!, Array.Empty<object>(), Path.Combine(outDir, "about-" + lang + ".png"));
                Snap(addin.GetType("ExcelAi.Addin.ProcessForm")!, new object[] { Sample(addin) }, Path.Combine(outDir, "process-" + lang + ".png"));
                SnapControl(addin.GetType("ExcelAi.Addin.ChatControl")!, Path.Combine(outDir, "chat-" + lang + ".png"));
            }

            return 0;
        }

        // 画面に見せるための選択範囲（5 行 x 2 列）
        private static object Sample(Assembly addin)
        {
            var type = addin.GetType("ExcelAi.Addin.SelectionInfo")!;
            var info = Activator.CreateInstance(type)!;
            var values = new object?[6, 3];
            string[] names = { "田中 太郎", "Suzuki Hanako", "山田 次郎", "李 明", "Smith John" };
            for (int r = 1; r <= 5; r++)
            {
                values[r, 1] = names[r - 1];
                values[r, 2] = "東京都千代田区 " + r;
            }

            type.GetProperty("Row")!.SetValue(info, 2);
            type.GetProperty("Col")!.SetValue(info, 1);
            type.GetProperty("Rows")!.SetValue(info, 5);
            type.GetProperty("Cols")!.SetValue(info, 2);
            type.GetProperty("Values")!.SetValue(info, values);
            type.GetProperty("Address")!.SetValue(info, "A2:B6");
            return info;
        }

        private static void Snap(Type formType, object[] ctorArgs, string path)
        {
            using (var form = (Form)Activator.CreateInstance(formType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, ctorArgs, null)!)
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(20, 20);
                form.ShowInTaskbar = true;
                form.TopMost = true;
                form.Show();
                form.Activate();
                Application.DoEvents();
                System.Threading.Thread.Sleep(300);
                Application.DoEvents();
                Capture(form.Bounds, path);
                form.Close();
            }
        }

        private static void SnapControl(Type controlType, string path)
        {
            using (var host = new Form { Text = "task pane", Width = 300, Height = 760, StartPosition = FormStartPosition.Manual, Location = new Point(20, 20), TopMost = true })
            {
                var control = (Control)Activator.CreateInstance(controlType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, null, null)!;
                control.Dock = DockStyle.Fill;
                host.Controls.Add(control);
                host.Show();
                Application.DoEvents();
                System.Threading.Thread.Sleep(300);
                Application.DoEvents();
                Capture(host.Bounds, path);
                host.Close();
            }
        }

        private static void Capture(Rectangle bounds, string path)
        {
            using (var bitmap = new Bitmap(bounds.Width, bounds.Height))
            using (var g = Graphics.FromImage(bitmap))
            {
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
                bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }
    }
}
