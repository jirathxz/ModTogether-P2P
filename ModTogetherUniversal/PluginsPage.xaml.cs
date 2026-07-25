using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ModTogetherUniversal.Services;

namespace ModTogetherUniversal
{
    public partial class PluginsPage : Page
    {
        public PluginsPage()
        {
            InitializeComponent();
            this.Loaded += PluginsPage_Loaded;
            App.Settings.OnSettingsChanged += ApplyTranslations;
        }

        private void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            if (TxtTitle != null) TxtTitle.Text = Models.I18N.GetString("plugins_title", lang);
            if (TxtDesc != null) TxtDesc.Text = Models.I18N.GetString("plugins_desc", lang);
            if (BtnOpenFolder != null) BtnOpenFolder.Content = Models.I18N.GetString("plugins_btn_open", lang);
            if (BtnReloadPlugins != null) BtnReloadPlugins.Content = Models.I18N.GetString("plugins_btn_reload", lang);
        }

        private void PluginsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTranslations();
            RefreshPluginList();
        }

        private async void RefreshPluginList()
        {
            ListExtensions.ItemsSource = null;
            ListExtensions.ItemsSource = PluginManager.Instance.LoadedPlugins;

            var catalog = await OnlinePluginStoreService.Instance.FetchCatalogFromGitHubAsync();
            ItemsOnlineStore.ItemsSource = null;

            if (catalog == null || catalog.Count == 0)
            {
                if (CardNoPluginsNotice != null) CardNoPluginsNotice.Visibility = Visibility.Visible;
            }
            else
            {
                if (CardNoPluginsNotice != null) CardNoPluginsNotice.Visibility = Visibility.Collapsed;
                ItemsOnlineStore.ItemsSource = catalog;
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string pluginsPath = PluginManager.Instance.GetPluginsPath();
            Directory.CreateDirectory(pluginsPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() 
            { 
                FileName = pluginsPath, 
                UseShellExecute = true, 
                Verb = "open" 
            });
        }

        private void BtnReloadPlugins_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow mainWindow)
            {
                mainWindow.ReloadAllPlugins();
            }
            else
            {
                PluginManager.Instance.LoadPlugins();
            }

            RefreshPluginList();
        }

        private async void BtnInstallStorePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PluginStoreItem item)
            {
                btn.IsEnabled = false;
                btn.Content = "Downloading Real DLL...";
                var (success, message) = await OnlinePluginStoreService.Instance.DownloadAndInstallPluginAsync(item);
                btn.IsEnabled = true;
                btn.Content = success ? "Installed ✅" : "Install Plugin";

                if (success)
                {
                    MessageBox.Show($"✅ {message}", "Plugin Installed", MessageBoxButton.OK, MessageBoxImage.Information);
                    BtnReloadPlugins_Click(sender, e);
                }
                else
                {
                    MessageBox.Show($"❌ {message}", "Plugin Download Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn)
            {
                btn.IsEnabled = false;
                btn.Content = "Checking SHA...";
                int updated = await OnlinePluginStoreService.Instance.UpdateAllPluginsAsync();
                btn.IsEnabled = true;
                btn.Content = "Check & Update All";

                MessageBox.Show(
                    updated > 0 ? $"✅ Updated {updated} plugin(s) to the latest SHA release!" : "✅ All installed plugins match the latest GitHub Release SHA!",
                    "Auto Plugin Update", MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshPluginList();
            }
        }
    }
}
