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
            if (BtnCheckUpdates != null) BtnCheckUpdates.Content = Models.I18N.GetString("plugins_btn_check_update", lang);
            if (ExpOnlineStore != null) ExpOnlineStore.Header = Models.I18N.GetString("plugins_online_title", lang);
            if (TxtInstalledTitle != null) TxtInstalledTitle.Text = Models.I18N.GetString("plugins_installed_title", lang);
            if (TxtNoPluginsNotice != null) TxtNoPluginsNotice.Text = Models.I18N.GetString("plugins_no_dll_notice", lang);
        }

        private void PluginsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTranslations();
            RefreshPluginList();
        }

        private async void RefreshPluginList()
        {
            ListInstalledPlugins.ItemsSource = null;
            ListInstalledPlugins.ItemsSource = PluginManager.Instance.LoadedPlugins;

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
            var lang = App.Settings.Current.Language;
            if (sender is Button btn && btn.Tag is PluginStoreItem item)
            {
                btn.IsEnabled = false;
                btn.Content = Models.I18N.GetString("plugins_downloading", lang);
                var (success, message) = await OnlinePluginStoreService.Instance.DownloadAndInstallPluginAsync(item);
                btn.IsEnabled = true;
                btn.Content = success 
                    ? Models.I18N.GetString("plugins_installed_badge", lang) 
                    : Models.I18N.GetString("plugins_btn_install", lang);

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

        private void BtnDeleteStorePlugin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is PluginStoreItem item)
            {
                var pluginsDir = PluginManager.Instance.GetPluginsPath();
                string targetFile = Path.Combine(pluginsDir, item.DllFileName);

                if (File.Exists(targetFile))
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete plugin '{item.Name}'?\n\nFile: {item.DllFileName}",
                        "Delete Plugin", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(targetFile);
                            MessageBox.Show($"🗑️ Plugin '{item.Name}' deleted successfully.", "Plugin Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                            BtnReloadPlugins_Click(sender, e);
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"❌ Failed to delete plugin: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
            }
        }

        private void BtnDeleteInstalledPlugin_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is ModTogether.API.IModPlugin plugin)
            {
                Type pType = plugin.GetType();
                if (plugin is PluginProxy proxy)
                {
                    pType = proxy.OriginalType;
                }
                
                string asmName = pType.Assembly.GetName().Name ?? "";
                if (string.IsNullOrEmpty(asmName)) return;

                string dllFileName = asmName + ".dll";
                var pluginsDir = PluginManager.Instance.GetPluginsPath();
                string targetFile = Path.Combine(pluginsDir, dllFileName);

                if (File.Exists(targetFile))
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete installed plugin '{plugin.Name}'?\n\nFile: {dllFileName}\n\nNote: This will delete the DLL file. Some plugin tabs may remain until you restart the app.",
                        "Delete Plugin", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(targetFile);
                            MessageBox.Show($"🗑️ Plugin '{plugin.Name}' deleted successfully.", "Plugin Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            // Remove from loaded list immediately to update UI
                            PluginManager.Instance.LoadedPlugins.Remove(plugin);
                            if (Application.Current.MainWindow is MainWindow mainWindow)
                            {
                                mainWindow.RemovePluginTabByTitle(plugin.Name);
                            }
                            
                            RefreshPluginList();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"❌ Failed to delete plugin: {ex.Message}\nMake sure it's not being actively used.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                }
                else
                {
                    MessageBox.Show($"❌ Could not find plugin DLL file: {dllFileName}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                btn.Content = Models.I18N.GetString("plugins_btn_check_update", App.Settings.Current.Language);

                MessageBox.Show(
                    updated > 0 ? $"✅ Updated {updated} plugin(s) to the latest SHA release!" : "✅ All installed plugins match the latest GitHub Release SHA!",
                    "Auto Plugin Update", MessageBoxButton.OK, MessageBoxImage.Information);

                RefreshPluginList();
            }
        }
    }
}
