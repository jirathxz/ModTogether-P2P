using System;
using System.Windows;
using Wpf.Ui.Controls;

namespace ModTogetherMHW
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
                if (App.Settings.Current.AutoEnableMods)
                {
                    Dispatcher.Invoke(() => 
                    {
                        if (App.Installer == null)
                        {
                            App.Installer = new Services.ModInstaller(App.Settings.Current.MhwDirectory);
                            App.Installer.OnLog += msg => Dispatcher.Invoke(() => Log(msg));
                            App.Installer.OnInstallProgress += pct => UpdateInstallProgress((int)pct);
                        }
                        
                        string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", modFilename);
                        if (System.IO.File.Exists(fullPath))
                        {
                            bool hasConflict = false;
                            var contents = Services.ArchiveExtractor.GetArchiveContents(fullPath);
                            string nativePc = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "nativePC");
                            
                            foreach (var entry in contents)
                            {
                                string lower = entry.ToLower();
                                int idx = lower.IndexOf("nativepc/");
                                if (idx >= 0)
                                {
                                    string relPath = entry.Substring(idx + "nativepc/".Length);
                                    string target = System.IO.Path.Combine(nativePc, relPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                                    if (System.IO.File.Exists(target))
                                    {
                                        hasConflict = true;
                                        break;
                                    }
                                }
                            }
                            
                            if (hasConflict)
                            {
                                Log($"⚠️ Auto-install skipped for {modFilename} (Conflict detected)");
                            }
                            else
                            {
                                Log($"⚡ Auto-installing downloaded mod: {modFilename}");
                                App.Installer.InstallMod(fullPath, modFilename);
                            }
                        }
                    });
                }
            };
            
            System.Threading.Tasks.Task.Run(async () => 
            {
                await System.Threading.Tasks.Task.Delay(2000); // Wait a bit before checking
                await App.Updater.CheckForUpdatesAsync();

                // Validate path on startup
                Dispatcher.Invoke(() => 
                {
                    if (ValidateGamePath())
                    {
                        string mhwDir = App.Settings.Current.MhwDirectory;
                        if (App.Installer == null)
                        {
                            App.Installer = new Services.ModInstaller(mhwDir);
                            App.Installer.OnLog += msg => Dispatcher.Invoke(() => Log(msg));
                            App.Installer.OnInstallProgress += pct => UpdateInstallProgress((int)pct);
                        }
                        string cacheDir = System.IO.Path.Combine(mhwDir, "GameMods");
                        if (System.IO.Directory.Exists(cacheDir))
                        {
                            Log("🔍 Scanning GameMods folder to auto-detect installed mods...");
                            bool changes = App.Installer.AutoDetectInstalledMods(cacheDir);
                            if (changes)
                            {
                                Log("✅ Auto-detected and registered manually installed mods.");
                            }
                        }
                    }
                });
            });
        }

        public void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            Title = Models.I18N.GetString("title", lang);
            
            if (NavRoom != null) NavRoom.Content = Models.I18N.GetString("tab_room", lang);
            if (NavManager != null) NavManager.Content = Models.I18N.GetString("tab_manager", lang);
            if (NavRecovery != null) NavRecovery.Content = Models.I18N.GetString("tab_recovery", lang);
            if (NavSettings != null) NavSettings.Content = Models.I18N.GetString("tab_settings", lang);
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
            
            Log("🛑 Disconnected.");
            
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
                Log($"⚠️ Failed to open browser: {ex.Message}");
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
        
        public void UpdateInstallProgress(int value)
        {
            Dispatcher.Invoke(() => PbInstall.Value = value);
        }

        public bool ValidateGamePath()
        {
            string mhwDir = App.Settings.Current.MhwDirectory;
            bool isValid = !string.IsNullOrEmpty(mhwDir) 
                        && System.IO.Directory.Exists(mhwDir) 
                        && System.IO.File.Exists(System.IO.Path.Combine(mhwDir, "MonsterHunterWorld.exe"));
            
            if (!isValid && !string.IsNullOrEmpty(mhwDir))
            {
                // Path was non-empty but invalid -> Reset path to force selecting a new path
                App.Settings.Current.MhwDirectory = string.Empty;
                App.Settings.Save();
                App.Installer = null;
                
                Dispatcher.Invoke(() => 
                {
                    Log("⚠️ [Game Path Error] Game directory is invalid or MonsterHunterWorld.exe is missing. Game path has been reset.");
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
                if (NavManager != null) 
                {
                    NavManager.IsEnabled = isValid;
                    NavManager.Opacity = isValid ? 1.0 : 0.4;
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
    }
}
