using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace SetlistManager
{
    static class Program
    {
        private const string APP_NAME = "Setlist Manager";
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            bool result;
            var mutex = new System.Threading.Mutex(true, "UniqueAppId", out result);
            if (!result)
            {
                MessageBox.Show("There's already another instance of " + APP_NAME + " running\nYou should only have one instance running at any given time to avoid file conflicts and crashes",  APP_NAME, MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }

            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            var argument = args.Aggregate("", (current, arg) => current + " " + arg).ToLowerInvariant().Trim();
            if (string.IsNullOrWhiteSpace(argument))
            {
                Application.Run(new SetlistManager());
            }
            else if (argument.EndsWith(".setlist", StringComparison.Ordinal))
            {
                var file = argument.Trim();
                string setlist;

                if (File.Exists(file))
                {
                    setlist = file;
                }
                else if (File.Exists(Application.StartupPath + "\\setlist\\" + file))
                {
                    setlist = Application.StartupPath + "\\setlist\\" + file;
                }
                else
                {
                    setlist = "";
                }
                Application.Run(new SetlistManager(setlist));
            }
            GC.KeepAlive(mutex);
        }

        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = (Exception)e.ExceptionObject;
                var vers = Assembly.GetExecutingAssembly().GetName().Version;
                var version = " v" + String.Format("{0}.{1}.{2}", vers.Major, vers.Minor, vers.Build);
                var error = APP_NAME + " crashed on me! Please see the error log below:" + Environment.NewLine + Environment.NewLine + 
                    Environment.NewLine + APP_NAME + " version" + version + Environment.NewLine + "Error Message:" + Environment.NewLine + 
                        ex.Message + Environment.NewLine + Environment.NewLine + "Stack Trace:" + Environment.NewLine + ex.StackTrace;
                MessageBox.Show(error, "Fatal Error", MessageBoxButtons.OKCancel, MessageBoxIcon.Stop);
            }
            finally
            {
                Application.Exit();
            }
        }
    }
}
