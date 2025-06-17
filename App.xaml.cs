using System.Configuration;
using System.Data;
using System.Runtime.InteropServices;
using System.Windows;

namespace StarforgeLauncher
{
    public partial class App : Application
    {
        private const string MutexName = "StarforgeLauncher";
        private Mutex? mutex;
        [DllImport("shell32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern int SetCurrentProcessExplicitAppUserModelID(string AppID);

        protected override void OnStartup(StartupEventArgs e)
        {
            SetCurrentProcessExplicitAppUserModelID(MutexName);

            bool createdNew;
            mutex = new Mutex(true, MutexName, out createdNew);

            if (!createdNew)
            {
                System.Windows.MessageBox.Show("Another instance of the application is already running.");
                Environment.Exit(0);
            }

            base.OnStartup(e);
        }
        protected override void OnExit(ExitEventArgs e)
        {
            if (mutex != null)
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
            }
            base.OnExit(e);
        }
    }

}
