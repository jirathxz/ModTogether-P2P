using System.Windows;
using ModTogetherMHW.Services;

namespace ModTogetherMHW
{
    public partial class App : Application
    {
        static App()
        {
            AppDomain.CurrentDomain.UnhandledException += (s, args) => 
            {
                string logPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "error.log");
                System.IO.File.WriteAllText(logPath, args.ExceptionObject.ToString());
            };
        }
        
        public static SettingsManager Settings { get; } = new SettingsManager();
        public static NetworkDiscovery Network { get; } = new NetworkDiscovery();
        public static UpdaterService Updater { get; } = new UpdaterService();
        public static ModServer Server { get; } = new ModServer();
        public static ModClient Client { get; } = new ModClient();
        public static ModInstaller? Installer { get; set; }
        public static ModFileWatcher Watcher { get; } = new ModFileWatcher(Client);

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            AppDomain.CurrentDomain.UnhandledException += (s, args) => 
            {
                var ex = args.ExceptionObject as Exception;
                string logPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "error.log");
                System.IO.File.WriteAllText(logPath, ex?.ToString());
                System.Windows.MessageBox.Show("Fatal Crash: " + ex?.Message + "\n\n" + ex?.StackTrace);
            };
            
            DispatcherUnhandledException += (s, args) => 
            {
                string logPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "error.log");
                System.IO.File.WriteAllText(logPath, args.Exception.ToString());
                System.Windows.MessageBox.Show("UI Crash: " + args.Exception.Message + "\n\n" + args.Exception.StackTrace);
                args.Handled = true;
            };
        }

        public static void ApplyTheme(string theme)
        {
            if (theme == "Light")
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Light);
            }
            else if (theme == "Dark")
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.Apply(Wpf.Ui.Appearance.ApplicationTheme.Dark);
            }
            else
            {
                Wpf.Ui.Appearance.ApplicationThemeManager.ApplySystemTheme();
            }
        }
    }
}
