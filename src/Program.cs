using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace JarStart
{
    internal static class Program
    {
        private static Mutex _mutex;
        private static readonly string MutexName = "Global" + (char)92 + "JarStart_SingleInstance_v1";

        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Native.SetDpiAware(); } catch { }

            var cfg = AppConfig.Load();
            Ui.Init();
            Theme.Apply(cfg);

            bool created;
            _mutex = new Mutex(true, MutexName, out created);
            if (!created)
            {
                MessageBox.Show("JarStart 已经在运行了。", "JarStart",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e) { ReportCrash(e.Exception); };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                ReportCrash(e.ExceptionObject as Exception);
            };

            try
            {
                Application.Run(new MainForm(cfg));
            }
            finally
            {
                try { _mutex.ReleaseMutex(); } catch { }
            }
        }

        private static void ReportCrash(Exception ex)
        {
            if (ex == null) return;
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JarStart");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "crash.log"),
                    DateTime.Now + "  " + ex + Environment.NewLine + Environment.NewLine,
                    System.Text.Encoding.Default);
            }
            catch { }
            try
            {
                MessageBox.Show("JarStart 遇到意外错误：\n\n" + ex.Message +
                    "\n\n详情已写入 %APPDATA%" + (char)92 + "JarStart" + (char)92 + "crash.log",
                    "JarStart", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch { }
        }
    }
}
