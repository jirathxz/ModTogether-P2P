using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace ModTogetherUniversal
{
    public partial class MainWindow : FluentWindow
    {
        public static MainWindow? Instance { get; private set; }
        private string _updateUrlStandalone = "";
        private string _updateAssetNameStandalone = "";
        private string _updateUrlLightweight = "";
        private string _updateAssetNameLightweight = "";

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;
            
            Loaded += (s, e) => 
            {
                // Load extensions
                Services.PluginManager.Instance.OnLog += Log;
                ReloadAllPlugins();

                // Apply theme after Window handle is created for reliable system theme detection
                App.ApplyTheme(App.Settings.Current.Theme);
                if (App.Settings.Current.Theme == "System")
                {
                    Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
                }

                ApplyTranslations();
                ValidateGamePath();
            };
            App.Settings.OnSettingsChanged += () => 
            {
                ApplyTranslations();
                ValidateGamePath();
            };
            
            Services.BandwidthTracker.OnSpeedUpdated += (up, down) =>
            {
                Dispatcher.Invoke(() => 
                {
                    TxtUploadSpeed.Text = FormatBytes(up) + "/s";
                    TxtDownloadSpeed.Text = FormatBytes(down) + "/s";
                });
            };
            Services.BandwidthTracker.Start();
            
            App.Updater.OnLog += msg => Dispatcher.Invoke(() => Log(msg));
            App.Updater.OnUpdateAvailable += (version, assets) => 
            {
                Dispatcher.Invoke(() => 
                {
                    bool foundAny = false;
                    
                    foreach (var asset in assets)
                    {
                        if (asset.Name.Contains("Standalone", StringComparison.OrdinalIgnoreCase))
                        {
                            _updateUrlStandalone = asset.Url;
                            _updateAssetNameStandalone = asset.Name;
                            BtnUpdateStandalone.Visibility = Visibility.Visible;
                            foundAny = true;
                        }
                        else if (asset.Name.Contains("Lightweight", StringComparison.OrdinalIgnoreCase))
                        {
                            _updateUrlLightweight = asset.Url;
                            _updateAssetNameLightweight = asset.Name;
                            BtnUpdateLightweight.Visibility = Visibility.Visible;
                            foundAny = true;
                        }
                        // Default to standalone button if the naming doesn't contain these words
                        else if (string.IsNullOrEmpty(_updateUrlStandalone))
                        {
                            _updateUrlStandalone = asset.Url;
                            _updateAssetNameStandalone = asset.Name;
                            BtnUpdateStandalone.Visibility = Visibility.Visible;
                            BtnUpdateStandalone.Content = "Update";
                            foundAny = true;
                        }
                    }

                    if (foundAny)
                    {
                        LblUpdateAlert.Text = $"Update Available: {version}";
                        UpdateAlertBar.Visibility = Visibility.Visible;
                    }
                });
            };
            
            App.Client.OnDownloadProgress += pct => UpdateDownloadProgress(pct);
            App.Client.OnUploadProgress += pct => UpdateUploadProgress(pct);
            
            App.Client.OnModDownloaded += (modFilename) => 
            {
                // Just log that it was downloaded successfully
                Log($"ðŸ“¥ Downloaded Mod: {modFilename}");
            };
            
            System.Threading.Tasks.Task.Run(async () => 
            {
                await System.Threading.Tasks.Task.Delay(2000); // Wait a bit before checking
                await App.Updater.CheckForUpdatesAsync();

                // Validate path on startup
                Dispatcher.Invoke(() => 
                {
                    ValidateGamePath();
                });
            });
        }

        public void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            Title = Models.I18N.GetString("title", lang);
            
            if (NavRoom != null) NavRoom.Content = Models.I18N.GetString("tab_room", lang);
            if (NavExplorer != null) NavExplorer.Content = Models.I18N.GetString("tab_explorer", lang);
            if (NavRecovery != null) NavRecovery.Content = Models.I18N.GetString("tab_recovery", lang);
            if (NavSettings != null) NavSettings.Content = Models.I18N.GetString("tab_settings", lang);
        }

        private Dictionary<Wpf.Ui.Controls.NavigationViewItem, System.Windows.Controls.Page> _pluginPages = new();

        private void RootNavigation_SelectionChanged(Wpf.Ui.Controls.NavigationView sender, System.Windows.RoutedEventArgs args)
        {
            if (sender.SelectedItem is Wpf.Ui.Controls.NavigationViewItem navItem
                && navItem.Tag?.ToString() == "DynamicPlugin"
                && _pluginPages.TryGetValue(navItem, out var pluginPage))
            {
                // Set the plugin page for the DynamicPluginPage wrapper
                DynamicPluginPage.CurrentPluginPage = pluginPage;
                bool navResult = RootNavigation.Navigate(typeof(DynamicPluginPage));
                Log($"[DEBUG] Navigation to DynamicPluginPage result: {navResult}");
            }
        }

        private void BtnToggleBottomPanel_Click(object sender, RoutedEventArgs e)
        {
            if (BottomPanelContainer.Visibility == Visibility.Visible)
            {
                BottomPanelContainer.Visibility = Visibility.Collapsed;
                TxtToggleBottomPanel.Text = "Show Console & Status";
                IconToggleBottomPanel.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronUp24;
            }
            else
            {
                BottomPanelContainer.Visibility = Visibility.Visible;
                TxtToggleBottomPanel.Text = "Hide Console & Status";
                IconToggleBottomPanel.Symbol = Wpf.Ui.Controls.SymbolRegular.ChevronDown24;
            }
        }

        private void BtnClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogBox.Clear();
        }

        private async void BtnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Log("Disconnecting...");
            BtnDisconnect.IsEnabled = false;
            
            App.Client?.StopBackgroundTasks();
            
            if (App.Server != null && App.Server.IsRunning)
            {
                await App.Server.StopAsync();
                App.Watcher?.Stop();
                App.Network?.StopBroadcasting();
            }
            
            Log("ðŸ›‘ Disconnected.");
            
            UserList.Visibility = Visibility.Collapsed;
            LblUsers.Text = Models.I18N.GetString("lbl_users", App.Settings.Current.Language);
        }

        private async void BtnUpdateStandalone_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdateStandalone.IsEnabled = false;
            BtnUpdateLightweight.IsEnabled = false;
            BtnUpdateStandalone.Content = "Downloading...";
            Log("Downloading Standalone update... Please wait.");
            
            await App.Updater.DownloadAndInstallUpdateAsync(_updateUrlStandalone, _updateAssetNameStandalone, progress => 
            {
                Dispatcher.Invoke(() => 
                {
                    BtnUpdateStandalone.Content = $"Downloading {progress}%";
                });
            });
        }
        
        private async void BtnUpdateLightweight_Click(object sender, RoutedEventArgs e)
        {
            BtnUpdateStandalone.IsEnabled = false;
            BtnUpdateLightweight.IsEnabled = false;
            BtnUpdateLightweight.Content = "Downloading...";
            Log("Downloading Lightweight update... Please wait.");
            
            await App.Updater.DownloadAndInstallUpdateAsync(_updateUrlLightweight, _updateAssetNameLightweight, progress => 
            {
                Dispatcher.Invoke(() => 
                {
                    BtnUpdateLightweight.Content = $"Downloading {progress}%";
                });
            });
        }

        private void BtnUpdateManual_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var psi = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/jirathxz/ModTogether-P2P/releases/latest",
                    UseShellExecute = true
                };
                System.Diagnostics.Process.Start(psi);
            }
            catch (Exception ex)
            {
                Log($"âš ï¸ Failed to open browser: {ex.Message}");
            }
        }

        public void Log(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText($"{message}{Environment.NewLine}");
                LogBox.ScrollToEnd();
            });
        }
        
        public void UpdateUploadProgress(int value)
        {
            Dispatcher.Invoke(() => PbUpload.Value = value);
        }

        public void UpdateDownloadProgress(int value)
        {
            Dispatcher.Invoke(() => PbDownload.Value = value);
        }
        
        private string FormatBytes(long bytes)
        {
            string[] suffix = { "B", "KB", "MB", "GB" };
            int i = 0;
            double dblSByte = bytes;
            while (bytes / 1024 > 0)
            {
                dblSByte = bytes / 1024.0;
                bytes /= 1024;
                i++;
            }
            return $"{dblSByte:0.##} {suffix[i]}";
        }
        
        public void ReloadAllPlugins()
        {
            Dispatcher.Invoke(() =>
            {
                string gameDir = App.Settings.Current.GameDirectory;
                Services.PluginManager.Instance.LoadPlugins();
                
                // Remove existing dynamic extension nav items and header
                var itemsToRemove = new System.Collections.Generic.List<object>();
                foreach (var item in RootNavigation.MenuItems)
                {
                    if (item is Wpf.Ui.Controls.NavigationViewItem navItem 
                        && navItem.Tag?.ToString() == "DynamicPlugin")
                    {
                        itemsToRemove.Add(navItem);
                    }
                    else if (item is Wpf.Ui.Controls.NavigationViewItemHeader headerItem 
                             && headerItem.Tag?.ToString() == "DynamicPluginHeader")
                    {
                        itemsToRemove.Add(headerItem);
                    }
                }
                foreach (var item in itemsToRemove)
                    RootNavigation.MenuItems.Remove(item);

                _pluginPages.Clear();

                if (Services.PluginManager.Instance.LoadedPlugins.Count > 0)
                {
                    var header = new Wpf.Ui.Controls.NavigationViewItemHeader
                    {
                        Text = "PLUGINS",
                        Tag = "DynamicPluginHeader"
                    };
                    RootNavigation.MenuItems.Add(header);
                }

                // Add a nav item for every loaded extension (no game-path filter)
                foreach (var ext in Services.PluginManager.Instance.LoadedPlugins)
                {
                    // Initialize with current game dir (empty string if not set)
                    ext.Initialize(gameDir);

                    System.Windows.Controls.Page? pageInstance = null;
                    try { pageInstance = ext.CreatePage(); } catch (Exception ex) { Log($"Failed to create page for {ext.Name}: {ex.Message}\n{ex.StackTrace}"); }
                    if (pageInstance == null) continue;

                    // Parse icon symbol
                    Wpf.Ui.Controls.SymbolRegular symbol = Wpf.Ui.Controls.SymbolRegular.Box24;
                    if (Enum.TryParse<Wpf.Ui.Controls.SymbolRegular>(ext.NavigationIcon, out var parsedSymbol))
                        symbol = parsedSymbol;

                    var navItem = new Wpf.Ui.Controls.NavigationViewItem
                    {
                        Content = ext.Name,
                        Icon = new Wpf.Ui.Controls.SymbolIcon { Symbol = symbol },
                        Tag = "DynamicPlugin",
                        TargetPageType = typeof(DynamicPluginPage)
                    };

                    // Store the page instance in the dictionary so we can retrieve it on click
                    _pluginPages[navItem] = pageInstance;

                    navItem.IsEnabled = true;
                    RootNavigation.MenuItems.Add(navItem);
                }
                Log($"âœ… Loaded {Services.PluginManager.Instance.LoadedPlugins.Count} plugin(s).");
            });
        }

        public bool ValidateGamePath()
        {
            string gameDir = App.Settings.Current.GameDirectory;
            bool isValid = !string.IsNullOrEmpty(gameDir) 
                        && System.IO.Directory.Exists(gameDir);
            
            if (!isValid && !string.IsNullOrEmpty(gameDir))
            {
                // Path was non-empty but invalid -> Reset path to force selecting a new path
                App.Settings.Current.GameDirectory = string.Empty;
                App.Settings.Save();
                
                Dispatcher.Invoke(() => 
                {
                    Log("âš ï¸ [Game Path Error] Game directory is invalid. Game path has been reset.");
                    System.Windows.MessageBox.Show(
                        Models.I18N.GetString("err_invalid_dir_reset", App.Settings.Current.Language),
                        Models.I18N.GetString("title_path_error", App.Settings.Current.Language),
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                });
            }

            Dispatcher.Invoke(() => 
            {
                if (NavRoom != null) 
                {
                    NavRoom.IsEnabled = isValid;
                    NavRoom.Opacity = isValid ? 1.0 : 0.4;
                }

                if (NavRecovery != null)
                {
                    NavRecovery.IsEnabled = isValid;
                    NavRecovery.Opacity = isValid ? 1.0 : 0.4;
                }
                
                if (!isValid)
                {
                    RootNavigation?.Navigate(typeof(SettingsPage));
                }
                else
                {
                    if (RootNavigation != null && (RootNavigation.SelectedItem == null || RootNavigation.SelectedItem == NavSettings))
                    {
                        RootNavigation.Navigate(typeof(RoomPage));
                    }
                }
            });

            return isValid;
        }

        public void RemoveExtensionTabByTitle(string title)
        {
            Dispatcher.Invoke(() =>
            {
                if (RootNavigation?.MenuItems == null) return;
                for (int i = RootNavigation.MenuItems.Count - 1; i >= 0; i--)
                {
                    if (RootNavigation.MenuItems[i] is Wpf.Ui.Controls.NavigationViewItem item && (string)item.Content == title)
                    {
                        RootNavigation.MenuItems.RemoveAt(i);
                    }
                }
            });
        }
    }
}

