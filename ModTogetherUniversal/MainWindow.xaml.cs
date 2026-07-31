using System;
using System.Linq;
using System.Text;
using System.Windows;
using ModTogetherUniversal.Models;
using Wpf.Ui.Controls;

namespace ModTogetherUniversal
{
    public partial class MainWindow : FluentWindow
    {
        private static readonly Encoding Windows1252;
        private static readonly UTF8Encoding StrictUtf8 = new(false, true);

        static MainWindow()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Windows1252 = Encoding.GetEncoding(1252);
        }
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
                // Disable ScrollViewer inside NavigationView to fix overflow issues
                var sv = FindVisualChild<System.Windows.Controls.ScrollViewer>(RootNavigation);
                if (sv != null)
                {
                    sv.VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Disabled;
                }

                // Load plugins
                Services.PluginManager.Instance.OnLog += Log;
                ReloadAllPlugins();

                // Apply theme after Window handle is created for reliable system theme detection
                App.ApplyTheme(App.Settings.Current.Theme);
                if (App.Settings.Current.Theme == "System")
                {
                    Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);
                }

                Services.DiscordRpcService.Instance.Initialize();
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
            if (App.Client != null)
            {
                App.Client.OnLog += msg => Dispatcher.Invoke(() => Log(msg));
                App.Client.OnUsersUpdate += users => 
                {
                    Dispatcher.Invoke(() => 
                    {
                        if (UserList != null)
                        {
                            UserList.Items.Clear();
                            var viewModels = users.Select(u => new UserSyncViewModel
                            {
                                Username = u.Username,
                                IsSynced = u.IsSynced,
                                SyncProgress = u.SyncProgress,
                                CurrentActivity = !string.IsNullOrEmpty(u.CurrentActivity) 
                                    ? u.CurrentActivity 
                                    : (u.IsSynced ? "🟢 Ready" : $"⚡ Syncing {u.SyncProgress}%"),
                                PingMs = u.PingMs
                            }).ToList();
                            
                            foreach (var u in viewModels) UserList.Items.Add(u);
                            int syncedCount = viewModels.Count(u => u.IsSynced);
                            LblUsers.Text = $"Party Readiness: {syncedCount}/{viewModels.Count} Ready";
                            UserList.Visibility = Visibility.Visible;
                        }
                    });
                };
                App.Client.OnKicked += () => 
                {
                    Dispatcher.Invoke(() => 
                    {
                        App.Client.StopBackgroundTasks();
                        Log("🚫 You have been disconnected from the session.");
                        if (BtnDisconnect != null) BtnDisconnect.IsEnabled = false;
                        if (UserList != null) UserList.Visibility = Visibility.Collapsed;
                        if (LblUsers != null) LblUsers.Text = "Connected Users: -";
                    });
                };
            }
            
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
            
            if (App.Client != null)
            {
                App.Client.OnDownloadProgress += pct => UpdateDownloadProgress(pct);
                App.Client.OnUploadProgress += pct => UpdateUploadProgress(pct);
                
                App.Client.OnModDownloaded += (modFilename) => 
                {
                    // Just log that it was downloaded successfully
                    Log($"ðŸ“¥ Downloaded Mod: {modFilename}");
                };
            }
            
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

        private void DumpVisualTree(DependencyObject obj, int depth, System.IO.StreamWriter writer)
        {
            if (obj == null) return;
            string indent = new string(' ', depth * 2);
            string info = $"{indent}{obj.GetType().Name}";
            if (obj is FrameworkElement fe)
            {
                info += $" [ActualHeight={fe.ActualHeight}, DesiredSize={fe.DesiredSize.Height}, Margin={fe.Margin}]";
            }
            if (obj is System.Windows.Controls.ScrollViewer sv)
            {
                info += $" (ViewportHeight={sv.ViewportHeight}, ExtentHeight={sv.ExtentHeight}, VertScrollBarVisibility={sv.VerticalScrollBarVisibility})";
            }
            writer.WriteLine(info);
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DumpVisualTree(System.Windows.Media.VisualTreeHelper.GetChild(obj, i), depth + 1, writer);
            }
        }

        private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child != null && child is T t)
                    return t;
                else
                {
                    T? childOfChild = FindVisualChild<T>(child!);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
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

        private async void BtnSendChat_Click(object sender, RoutedEventArgs e)
        {
            await SendChat();
        }

        private async void TxtChatInput_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                await SendChat();
            }
        }

        private async System.Threading.Tasks.Task SendChat()
        {
            string msg = TxtChatInput.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            string username = Environment.UserName;
            
            if (App.Server != null && App.Server.IsRunning)
            {
                username = App.Server.HostUsername;
                App.Server.BroadcastChat(username, msg);
            }
            else if (App.Client != null && App.Client.IsConnected)
            {
                await App.Client.SendChatAsync(msg);
            }
            else
            {
                Log("⚠️ You must be in a session to send a message.");
                return;
            }

            TxtChatInput.Text = "";
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
            if (message.StartsWith("[DEBUG]", StringComparison.OrdinalIgnoreCase) && App.Settings?.Current?.EnableDebugLog == false)
            {
                return;
            }

            Dispatcher.Invoke(() =>
            {
                LogBox.AppendText($"{RepairMojibake(message)}{Environment.NewLine}");
                LogBox.ScrollToEnd();
            });
        }

        // Older builds wrote some UTF-8 symbols through a legacy code page.
        // Repair them at the display boundary so existing service messages remain readable.
        private static string RepairMojibake(string value)
        {
            for (var attempt = 0; attempt < 2 && LooksLikeMojibake(value); attempt++)
            {
                try
                {
                    var repaired = StrictUtf8.GetString(Windows1252.GetBytes(value));
                    if (repaired == value) break;
                    value = repaired;
                }
                catch (DecoderFallbackException)
                {
                    break;
                }
            }

            return value;
        }

        private static bool LooksLikeMojibake(string value) =>
            value.Contains('Ã') || value.Contains('Â') || value.Contains('â') || value.Contains('ð') || value.Contains('Å');
        
        public void UpdateUploadProgress(int value)
        {
            Dispatcher.Invoke(() => PbUpload.Value = value);
        }

        public void UpdateDownloadProgress(int value)
        {
            Dispatcher.Invoke(() => PbDownload.Value = value);
        }

        public void UpdateInstallProgress(int value)
        {
            Dispatcher.Invoke(() => PbInstall.Value = value);
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
                
                // Remove existing dynamic plugin nav items and header
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
                        FontSize = 10.5,
                        Opacity = 0.55,
                        Margin = new System.Windows.Thickness(12, 10, 0, 4),
                        Tag = "DynamicPluginHeader"
                    };
                    RootNavigation.MenuItems.Add(header);
                }

                foreach (var ext in Services.PluginManager.Instance.LoadedPlugins)
                {
                    bool isMatch = Services.PluginManager.Instance.IsPluginForGame(ext, gameDir);

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

                    if (isMatch)
                    {
                        // Bug P4 Fix: Only Initialize() when the plugin actually matches the current game
                        ext.Initialize(gameDir);
                        navItem.IsEnabled = true;
                        RootNavigation.MenuItems.Add(navItem);
                    }
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

                if (NavPlugins != null)
                {
                    NavPlugins.IsEnabled = isValid;
                    NavPlugins.Opacity = isValid ? 1.0 : 0.4;
                }

                if (RootNavigation?.MenuItems != null)
                {
                    foreach (var item in RootNavigation.MenuItems)
                    {
                        if (item is Wpf.Ui.Controls.NavigationViewItem navItem && navItem.Tag?.ToString() == "DynamicPlugin")
                        {
                            navItem.IsEnabled = isValid;
                            navItem.Opacity = isValid ? 1.0 : 0.4;
                        }
                    }
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

        public void RemovePluginTabByTitle(string title)
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

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                bool hasValidMod = files != null && files.Any(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                                                                  f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                                                                  f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase));
                if (hasValidMod)
                {
                    e.Effects = DragDropEffects.Copy;
                    e.Handled = true;
                    return;
                }
            }
            e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private async void Window_Drop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files == null || files.Length == 0) return;

            string gameDir = App.Settings.Current.GameDirectory;
            string targetDir = string.IsNullOrWhiteSpace(gameDir) ? System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameMods") : System.IO.Path.Combine(gameDir, "GameMods");
            System.IO.Directory.CreateDirectory(targetDir);

            int count = 0;
            foreach (var file in files)
            {
                if (file.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                    file.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                {
                    string fileName = System.IO.Path.GetFileName(file);
                    string destPath = System.IO.Path.Combine(targetDir, fileName);

                    try
                    {
                        System.IO.File.Copy(file, destPath, true);
                        Log($"📦 Imported mod via Drag & Drop: {fileName}");
                        count++;

                        // Auto-sync with connected room
                        if (App.Server != null && App.Server.IsRunning)
                        {
                            App.Server.DeletedMods.TryRemove(fileName, out _);
                            App.Server.TriggerCacheRefresh();
                        }
                        else if (App.Client != null && App.Client.IsConnected)
                        {
                            await App.Client.UploadModAsync(destPath, fileName);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"❌ Failed to import {fileName}: {ex.Message}");
                    }
                }
            }

            if (count > 0)
            {
                Log($"✅ Imported & Synced {count} mod(s) with party room!");
            }
        }

        #region Global Mouse Wheel Scroll System

        protected override void OnPreviewMouseWheel(System.Windows.Input.MouseWheelEventArgs e)
        {
            base.OnPreviewMouseWheel(e);

            if (e.Handled) return;

            var originalSource = e.OriginalSource as DependencyObject;
            var scrollViewer = FindAncestor<System.Windows.Controls.ScrollViewer>(originalSource) 
                               ?? FindScrollableChild(RootNavigation);

            if (scrollViewer != null)
            {
                double delta = e.Delta / 2.0;
                double targetOffset = scrollViewer.VerticalOffset - delta;
                scrollViewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(scrollViewer.ScrollableHeight, targetOffset)));
                e.Handled = true;
            }
        }

        private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T t) return t;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static System.Windows.Controls.ScrollViewer? FindScrollableChild(DependencyObject? obj)
        {
            if (obj == null) return null;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child is System.Windows.Controls.ScrollViewer sv && sv.ScrollableHeight > 0)
                {
                    return sv;
                }
                var childOfChild = FindScrollableChild(child);
                if (childOfChild != null) return childOfChild;
            }
            return null;
        }

        #endregion
    }
}
