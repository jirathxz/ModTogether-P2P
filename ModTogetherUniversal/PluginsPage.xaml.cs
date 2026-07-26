using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModTogetherUniversal.Services;

namespace ModTogetherUniversal
{
    public class PluginDisplayItem
    {
        public string Name { get; set; } = "";
        public string Version { get; set; } = "1.0.0";
        public string TargetGame { get; set; } = "Universal";
        public string Description { get; set; } = "";
        public string Author { get; set; } = "Community";
        public string TrustLevel { get; set; } = "Verified Trust";
        public string Permissions { get; set; } = "📁 Disk Access, 🌐 P2P Sync, ⚙️ Game API";
        public bool IsInstalled { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string DllFileName { get; set; } = "";

        public string CategoryHeader => IsInstalled ? "📦 Installed Plugins (Local)" : "🌐 GitHub Repository (Online Store)";

        public string StatusText => IsInstalled ? (IsUpdateAvailable ? "Update Available" : "Installed") : "Available";
        public Wpf.Ui.Controls.ControlAppearance BadgeAppearance => IsInstalled ? (IsUpdateAvailable ? Wpf.Ui.Controls.ControlAppearance.Caution : Wpf.Ui.Controls.ControlAppearance.Success) : Wpf.Ui.Controls.ControlAppearance.Secondary;

        public PluginStoreItem? StoreItem { get; set; }
        public ModTogether.API.IModPlugin? InstalledPlugin { get; set; }
    }

    public partial class PluginsPage : Page
    {
        private List<PluginDisplayItem> _allDisplayItems = new();

        public PluginsPage()
        {
            InitializeComponent();
            this.Loaded += (s, e) =>
            {
                // Disable the ancestor ScrollViewer (from NavigationView) to fix infinite height overflow
                var parent = System.Windows.Media.VisualTreeHelper.GetParent(this);
                while (parent != null)
                {
                    if (parent is ScrollViewer sv)
                    {
                        sv.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
                        break;
                    }
                    parent = System.Windows.Media.VisualTreeHelper.GetParent(parent);
                }
            };
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
            if (TxtNoPluginsNotice != null) TxtNoPluginsNotice.Text = Models.I18N.GetString("plugins_no_dll_notice", lang);
        }

        private void PluginsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTranslations();
            RefreshPluginList();
        }

        private async void RefreshPluginList()
        {
            _allDisplayItems.Clear();

            var loadedPlugins = PluginManager.Instance.LoadedPlugins.ToList();
            var catalog = await OnlinePluginStoreService.Instance.FetchCatalogFromGitHubAsync();

            if (catalog == null || catalog.Count == 0)
            {
                if (CardNoPluginsNotice != null) CardNoPluginsNotice.Visibility = Visibility.Visible;
            }
            else
            {
                if (CardNoPluginsNotice != null) CardNoPluginsNotice.Visibility = Visibility.Collapsed;
            }

            // Map Store Catalog Items
            if (catalog != null)
            {
                foreach (var storeItem in catalog)
                {
                    var installedMatch = loadedPlugins.FirstOrDefault(p => 
                        p.Name.Equals(storeItem.Name, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(storeItem.TargetGame) && p.TargetGame.Equals(storeItem.TargetGame, StringComparison.OrdinalIgnoreCase)));

                    var displayItem = new PluginDisplayItem
                    {
                        Name = storeItem.Name,
                        Version = storeItem.Version,
                        TargetGame = string.IsNullOrEmpty(storeItem.TargetGame) ? "Universal" : storeItem.TargetGame,
                        Description = storeItem.Description,
                        Author = string.IsNullOrEmpty(storeItem.Author) ? "jirathxz" : storeItem.Author,
                        TrustLevel = string.IsNullOrEmpty(storeItem.TrustLevel) ? "Verified Trust" : storeItem.TrustLevel,
                        Permissions = string.IsNullOrEmpty(storeItem.Permissions) ? "📁 Disk Access, 🌐 P2P Sync" : storeItem.Permissions,
                        IsInstalled = storeItem.IsInstalled || installedMatch != null,
                        IsUpdateAvailable = storeItem.IsUpdateAvailable,
                        DllFileName = storeItem.DllFileName,
                        StoreItem = storeItem,
                        InstalledPlugin = installedMatch
                    };

                    _allDisplayItems.Add(displayItem);
                }
            }

            // Add any loaded plugins that were not in the online catalog
            foreach (var plugin in loadedPlugins)
            {
                if (!_allDisplayItems.Any(item => item.InstalledPlugin == plugin || item.Name.Equals(plugin.Name, StringComparison.OrdinalIgnoreCase)))
                {
                    _allDisplayItems.Add(new PluginDisplayItem
                    {
                        Name = plugin.Name,
                        Version = plugin.Version,
                        TargetGame = string.IsNullOrEmpty(plugin.TargetGame) ? "Universal" : plugin.TargetGame,
                        Description = plugin.Description,
                        Author = string.IsNullOrEmpty(plugin.Author) ? "Community" : plugin.Author,
                        TrustLevel = "Verified Trust",
                        Permissions = "📁 Disk Access, 🌐 P2P Sync, ⚙️ Game API",
                        IsInstalled = true,
                        IsUpdateAvailable = false,
                        InstalledPlugin = plugin
                    });
                }
            }

            ApplyFilter();
        }

        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (ListPlugins == null) return;

            int selectedFilter = CmbFilter?.SelectedIndex ?? 0;
            IEnumerable<PluginDisplayItem> filtered = _allDisplayItems;

            if (selectedFilter == 1) // Installed Only
            {
                filtered = filtered.Where(x => x.IsInstalled);
            }
            else if (selectedFilter == 2) // Online Store
            {
                filtered = filtered.Where(x => !x.IsInstalled || x.StoreItem != null);
            }

            var previousSelectedName = (ListPlugins.SelectedItem as PluginDisplayItem)?.Name;

            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(filtered.ToList());
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new System.Windows.Data.PropertyGroupDescription("CategoryHeader"));

            ListPlugins.ItemsSource = view;

            // Re-select previously selected item if possible
            if (!string.IsNullOrEmpty(previousSelectedName))
            {
                var match = filtered.FirstOrDefault(x => x.Name == previousSelectedName);
                if (match != null) ListPlugins.SelectedItem = match;
            }

            UpdateInspectorState();
        }

        private void ListPlugins_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateInspectorState();
        }

        private void UpdateInspectorState()
        {
            if (PanelEmptyInspector == null || PanelPluginInspector == null) return;

            if (ListPlugins.SelectedItem is PluginDisplayItem item)
            {
                PanelEmptyInspector.Visibility = Visibility.Collapsed;
                PanelPluginInspector.Visibility = Visibility.Visible;

                if (InspectorTitle != null) InspectorTitle.Text = item.Name;
                if (InspectorAuthor != null) InspectorAuthor.Text = $"Author: {item.Author}";
                if (InspectorTargetGame != null) InspectorTargetGame.Text = $"Target: {item.TargetGame}";
                if (InspectorDescription != null) InspectorDescription.Text = string.IsNullOrWhiteSpace(item.Description) ? "No description provided." : item.Description;
                if (InspectorPermissions != null) InspectorPermissions.Text = item.Permissions;
                if (InspectorTrustBadge != null) InspectorTrustBadge.Content = item.TrustLevel;

                if (BtnInspectorInstall != null)
                {
                    if (item.StoreItem != null)
                    {
                        BtnInspectorInstall.Visibility = Visibility.Visible;
                        BtnInspectorInstall.Content = item.IsUpdateAvailable 
                            ? "Update Plugin" 
                            : (item.IsInstalled ? "Re-install Plugin" : "Install Plugin");
                        BtnInspectorInstall.Appearance = item.IsUpdateAvailable ? Wpf.Ui.Controls.ControlAppearance.Caution : Wpf.Ui.Controls.ControlAppearance.Success;
                    }
                    else
                    {
                        BtnInspectorInstall.Visibility = Visibility.Collapsed;
                    }
                }

                if (BtnInspectorDelete != null)
                {
                    BtnInspectorDelete.Visibility = item.IsInstalled ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            else
            {
                PanelEmptyInspector.Visibility = Visibility.Visible;
                PanelPluginInspector.Visibility = Visibility.Collapsed;
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

        private async void BtnInspectorInstall_Click(object sender, RoutedEventArgs e)
        {
            if (ListPlugins.SelectedItem is PluginDisplayItem item && item.StoreItem != null)
            {
                var lang = App.Settings.Current.Language;
                if (sender is Button btn)
                {
                    btn.IsEnabled = false;
                    btn.Content = Models.I18N.GetString("plugins_downloading", lang);

                    var (success, message) = await OnlinePluginStoreService.Instance.DownloadAndInstallPluginAsync(item.StoreItem);
                    
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
        }

        private void BtnInspectorDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListPlugins.SelectedItem is PluginDisplayItem item)
            {
                string dllFileName = item.DllFileName;

                if (string.IsNullOrEmpty(dllFileName) && item.InstalledPlugin != null)
                {
                    Type pType = item.InstalledPlugin.GetType();
                    if (item.InstalledPlugin is PluginProxy proxy)
                    {
                        pType = proxy.OriginalType;
                    }
                    string asmName = pType.Assembly.GetName().Name ?? "";
                    if (!string.IsNullOrEmpty(asmName))
                    {
                        dllFileName = asmName + ".dll";
                    }
                }

                if (string.IsNullOrEmpty(dllFileName)) return;

                var pluginsDir = PluginManager.Instance.GetPluginsPath();
                string targetFile = Path.Combine(pluginsDir, dllFileName);

                if (File.Exists(targetFile))
                {
                    var result = MessageBox.Show(
                        $"Are you sure you want to delete plugin '{item.Name}'?\n\nFile: {dllFileName}\n\nNote: This will delete the DLL file. Some plugin tabs may remain until you restart the app.",
                        "Delete Plugin", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                    if (result == MessageBoxResult.Yes)
                    {
                        try
                        {
                            File.Delete(targetFile);
                            MessageBox.Show($"🗑️ Plugin '{item.Name}' deleted successfully.", "Plugin Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                            
                            if (item.InstalledPlugin != null)
                            {
                                PluginManager.Instance.LoadedPlugins.Remove(item.InstalledPlugin);
                                if (Application.Current.MainWindow is MainWindow mainWindow)
                                {
                                    mainWindow.RemovePluginTabByTitle(item.InstalledPlugin.Name);
                                }
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
