using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ModTogetherUniversal.Models;
using ModTogetherUniversal.Services;

namespace ModTogetherUniversal
{
    public partial class ModExplorerPage : Page, INotifyPropertyChanged
    {
        private ObservableCollection<ModItemData> _modItems = new ObservableCollection<ModItemData>();
        private string ModsDirectory 
        {
            get 
            {
                string gameDir = App.Settings.Current.GameDirectory;
                if (string.IsNullOrWhiteSpace(gameDir)) gameDir = AppDomain.CurrentDomain.BaseDirectory;
                return Path.Combine(gameDir, "GameMods");
            }
        }
        
        // Returns the target directory from Settings (GameDirectory)
        private string TargetDirectory => App.Settings.Current.GameDirectory;

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _colInstallText = "Install";
        public string ColInstallText { get => _colInstallText; set { _colInstallText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColInstallText))); } }
        
        private string _colFilenameText = "Filename";
        public string ColFilenameText { get => _colFilenameText; set { _colFilenameText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColFilenameText))); } }

        private string _colSizeText = "Size";
        public string ColSizeText { get => _colSizeText; set { _colSizeText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColSizeText))); } }

        private string _colModifiedText = "Modified";
        public string ColModifiedText { get => _colModifiedText; set { _colModifiedText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColModifiedText))); } }

        private string _menuDeleteText = "Delete Mod";
        public string MenuDeleteText { get => _menuDeleteText; set { _menuDeleteText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MenuDeleteText))); } }

        private bool _isInstallAllowed = true;
        public bool IsInstallAllowed
        {
            get => _isInstallAllowed;
            set
            {
                if (_isInstallAllowed != value)
                {
                    _isInstallAllowed = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstallAllowed)));
                }
            }
        }

        private string _installToolTip = "Check to install this mod to target directory.";
        public string InstallToolTip
        {
            get => _installToolTip;
            set
            {
                if (_installToolTip != value)
                {
                    _installToolTip = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(InstallToolTip)));
                }
            }
        }

        private System.IO.FileSystemWatcher? _watcher;

        private void SetupWatcher()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }

            if (!string.IsNullOrWhiteSpace(ModsDirectory) && Directory.Exists(ModsDirectory))
            {
                _watcher = new System.IO.FileSystemWatcher(ModsDirectory)
                {
                    NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.LastWrite,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };

                System.IO.FileSystemEventHandler handler = (s, e) =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() => RefreshModsList());
                };
                System.IO.RenamedEventHandler renamedHandler = (s, e) =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() => RefreshModsList());
                };

                _watcher.Created += handler;
                _watcher.Deleted += handler;
                _watcher.Changed += handler;
                _watcher.Renamed += renamedHandler;
            }
        }

        public ModExplorerPage()
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
            this.DataContext = this;
            ListMods.ItemsSource = _modItems;

            Loaded += (s, e) =>
            {
                ApplyTranslations();
                if (!string.IsNullOrWhiteSpace(ModsDirectory) && !Directory.Exists(ModsDirectory))
                    try { Directory.CreateDirectory(ModsDirectory); } catch { }
                
                SetupWatcher();
                
                CheckPluginStatus();
                RefreshModsList();
                LoadExplorerPresets();
            };
            App.Settings.OnSettingsChanged += () =>
            {
                ApplyTranslations();
                CheckPluginStatus();
                SetupWatcher();
                RefreshModsList();
            };
        }

        private void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            if (TxtTitle != null) TxtTitle.Text = I18N.GetString("explorer_title", lang);
            if (TxtDesc != null) TxtDesc.Text = I18N.GetString("explorer_desc", lang);
            if (TxtModFolderLabel != null) TxtModFolderLabel.Text = I18N.GetString("explorer_mod_folder_label", lang);

            // Display current GameMods directory
            if (TxtModFolderValue != null)
                TxtModFolderValue.Text = ModsDirectory;

            if (TxtInstallTypeLabel != null) TxtInstallTypeLabel.Text = I18N.GetString("explorer_install_type", lang);
            if (CmbItemSingle != null) CmbItemSingle.Content = I18N.GetString("explorer_type_single", lang);
            if (CmbItemExtract != null) CmbItemExtract.Content = I18N.GetString("explorer_type_extract", lang);

            if (BtnOpenModsFolder != null) BtnOpenModsFolder.Content = I18N.GetString("explorer_btn_open", lang);
            if (BtnRefreshMods != null) BtnRefreshMods.Content = I18N.GetString("explorer_btn_refresh", lang);
            if (BtnCheckAll != null) BtnCheckAll.Content = I18N.GetString("btn_check_all", lang);
            if (BtnUncheckAll != null) BtnUncheckAll.Content = I18N.GetString("btn_uncheck_all", lang);
            if (BtnImportMod != null) BtnImportMod.Content = I18N.GetString("btn_import", lang);

            ColInstallText = I18N.GetString("explorer_col_install", lang);
            ColFilenameText = I18N.GetString("explorer_col_filename", lang);
            ColSizeText = I18N.GetString("explorer_col_size", lang);
            ColModifiedText = I18N.GetString("explorer_col_modified", lang);
            MenuDeleteText = I18N.GetString("btn_delete_mod", lang);
            
            CheckPluginStatus();
        }

        private void CheckPluginStatus()
        {
            string gameDir = App.Settings.Current.GameDirectory;
            var activePlugin = PluginManager.Instance.LoadedPlugins.FirstOrDefault(p => PluginManager.Instance.IsPluginForGame(p, gameDir));
            if (activePlugin != null)
            {
                IsInstallAllowed = false;
                InstallToolTip = $"Installation is managed by the active plugin ({activePlugin.Name}). Use the dedicated Plugin Tab.";
                if (CmbInstallType != null)
                {
                    CmbInstallType.IsEnabled = false;
                    CmbInstallType.ToolTip = InstallToolTip;
                }
                if (BtnInstallChecked != null) BtnInstallChecked.IsEnabled = false;
                if (BtnUninstallChecked != null) BtnUninstallChecked.IsEnabled = false;
                if (BtnCheckAll != null) BtnCheckAll.IsEnabled = false;
                if (BtnUncheckAll != null) BtnUncheckAll.IsEnabled = false;
                if (CardPluginNotice != null && TxtPluginNotice != null)
                {
                    CardPluginNotice.Visibility = Visibility.Visible;
                    TxtPluginNotice.Text = $"⚡ Dedicated Plugin ({activePlugin.Name}) Active: Mod installation for this game is managed by the plugin. Please use the '{activePlugin.Name}' tab in the navigation menu to install and manage mods.";
                }
            }
            else
            {
                IsInstallAllowed = true;
                InstallToolTip = "Check to install this mod to target directory.";
                if (CmbInstallType != null)
                {
                    CmbInstallType.IsEnabled = true;
                    CmbInstallType.ToolTip = null;
                }
                if (BtnInstallChecked != null) BtnInstallChecked.IsEnabled = true;
                if (BtnUninstallChecked != null) BtnUninstallChecked.IsEnabled = true;
                if (BtnCheckAll != null) BtnCheckAll.IsEnabled = true;
                if (BtnUncheckAll != null) BtnUncheckAll.IsEnabled = true;
                if (CardPluginNotice != null) CardPluginNotice.Visibility = Visibility.Collapsed;
            }

            bool isSessionActive = (App.Server != null && App.Server.IsRunning) || (App.Client != null && App.Client.IsConnected);
            if (CmbExplorerPresets != null) CmbExplorerPresets.IsEnabled = !isSessionActive;
            if (BtnSaveProfile != null) BtnSaveProfile.IsEnabled = !isSessionActive;
            if (BtnDeleteProfile != null) BtnDeleteProfile.IsEnabled = !isSessionActive;
        }

        private void ListMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ListMods.SelectedItem is ModItemData mod)
            {
                PanelEmptyInspector.Visibility = Visibility.Collapsed;
                PanelModInspector.Visibility = Visibility.Visible;

                InspectorTitle.Text = mod.Filename;
                InspectorSize.Text = mod.Size;
                InspectorPriority.Text = mod.Priority.ToString();
                InspectorDateModified.Text = mod.DateModified;
                InspectorFilePath.Text = System.IO.Path.Combine(ModsDirectory, mod.Filename);

                if (mod.IsChecked)
                {
                    InspectorStatusText.Text = "Installed / Active";
                    InspectorStatusText.Foreground = (System.Windows.Media.Brush)FindResource("SystemFillColorSuccessBrush");
                    BtnInspectorToggle.Content = "Uninstall Mod";
                    BtnInspectorToggle.Appearance = Wpf.Ui.Controls.ControlAppearance.Secondary;
                }
                else
                {
                    InspectorStatusText.Text = "Uninstalled / Disabled";
                    InspectorStatusText.Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush");
                    BtnInspectorToggle.Content = "Install Mod";
                    BtnInspectorToggle.Appearance = Wpf.Ui.Controls.ControlAppearance.Success;
                }

                if (mod.ConflictVisibility == Visibility.Visible)
                {
                    CardInspectorConflict.Visibility = Visibility.Visible;
                    InspectorConflictText.Text = mod.ConflictWarningText;
                }
                else
                {
                    CardInspectorConflict.Visibility = Visibility.Collapsed;
                }

                if (mod.OwnersBadgeVisibility == Visibility.Visible)
                {
                    CardInspectorOwners.Visibility = Visibility.Visible;
                    InspectorOwnersText.Text = mod.OwnersBadgeText;
                }
                else
                {
                    CardInspectorOwners.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                PanelEmptyInspector.Visibility = Visibility.Visible;
                PanelModInspector.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnInspectorToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ListMods.SelectedItem is ModItemData mod)
            {
                if (!IsInstallAllowed)
                {
                    var plugin = PluginManager.Instance.LoadedPlugins.FirstOrDefault(p => PluginManager.Instance.IsPluginForGame(p, App.Settings.Current.GameDirectory));
                    string pName = plugin?.Name ?? "Plugin";
                    MessageBox.Show($"Mod installation for this game is managed by the '{pName}' plugin.\n\nPlease use the '{pName}' tab in the menu to manage and toggle mods.", "Managed by Plugin", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                mod.IsChecked = !mod.IsChecked;
                TryApplyInstallationState(mod, mod.IsChecked, out _);
                ListMods_SelectionChanged(sender, null!);
            }
        }

        private void BtnInspectorBackup_Click(object sender, RoutedEventArgs e)
        {
            if (ListMods.SelectedItem is ModItemData mod)
            {
                MenuItem_Backup_Click(sender, e);
            }
        }

        private void BtnInspectorDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListMods.SelectedItem is ModItemData mod)
            {
                MenuItem_Delete_Click(sender, e);
            }
        }

        private void BtnInstallChecked_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInstallAllowed) return;
            int count = 0;
            foreach (var item in _modItems.Where(m => m.IsChecked))
            {
                if (TryApplyInstallationState(item, true, out _)) count++;
            }
            TxtStatus.Text = $"⚡ Installed {count} checked mod(s) to target folder.";
        }

        private void BtnUninstallChecked_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInstallAllowed) return;
            int count = 0;
            foreach (var item in _modItems.Where(m => m.IsChecked))
            {
                if (TryApplyInstallationState(item, false, out _)) count++;
            }
            TxtStatus.Text = $"❎ Uninstalled {count} checked mod(s) from target folder.";
        }

        private void BtnInstallRow_Click(object sender, RoutedEventArgs e)
        {
            if (!IsInstallAllowed)
            {
                var plugin = PluginManager.Instance.LoadedPlugins.FirstOrDefault(p => PluginManager.Instance.IsPluginForGame(p, App.Settings.Current.GameDirectory));
                string pName = plugin?.Name ?? "Plugin";
                MessageBox.Show($"Mod installation for this game is managed by the '{pName}' plugin.\n\nPlease use the '{pName}' tab in the menu to manage and toggle mods.", "Managed by Plugin", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (sender is Button btn && btn.Tag is ModItemData mod)
            {
                mod.IsChecked = !mod.IsChecked;
            }
        }

        private void BtnCheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _modItems) item.IsChecked = true;
        }

        private void BtnUncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _modItems) item.IsChecked = false;
        }

        private void BtnScanConflicts_Click(object sender, RoutedEventArgs e)
        {
            RunConflictScan();
        }

        private void RunConflictScan()
        {
            if (!Directory.Exists(ModsDirectory)) return;

            var activeMods = _modItems.Where(m => m.IsChecked).Select(m => m.Filename).ToList();
            if (activeMods.Count == 0)
            {
                TxtStatus.Text = "No active (checked) mods to scan.";
                return;
            }

            TxtStatus.Text = "Scanning active mods for conflicts... ⚔️";
            
            System.Threading.Tasks.Task.Run(() =>
            {
                var scanResult = ModConflictDetector.AnalyzeConflicts(ModsDirectory, activeMods);

                Dispatcher.Invoke(() =>
                {
                    int conflictCount = 0;
                    foreach (var item in _modItems)
                    {
                        item.OwnersBadgeText = RecycleManager.Instance.GetOwnersBadgeText(item.Filename);
                        item.Priority = scanResult.ModPriorities.TryGetValue(item.Filename, out int p) ? p : 0;
                        if (scanResult.ConflictedModFilenames.Contains(item.Filename))
                        {
                            conflictCount++;
                            item.HasConflict = true;

                            var itemConflicts = scanResult.ConflictList.Where(c => c.ConflictingModFiles.Contains(item.Filename)).ToList();
                            var otherMods = itemConflicts.SelectMany(c => c.ConflictingModFiles).Where(m => m != item.Filename).Distinct().ToList();
                            var sampleFiles = itemConflicts.Take(3).Select(c => Path.GetFileName(c.InternalFilePath)).ToList();

                            item.ConflictWarningText = $"⚠️ Conflict Detected!\nOverlaps with: {string.Join(", ", otherMods)}\nSample Overlapping Files:\n - {string.Join("\n - ", sampleFiles)}";
                        }
                        else
                        {
                            item.HasConflict = false;
                            item.ConflictWarningText = string.Empty;
                        }
                    }

                    if (scanResult.HasConflicts)
                    {
                        TxtStatus.Text = $"⚠️ Warning: {scanResult.ConflictList.Count} conflicting file(s) found across {conflictCount} mod(s)! Hover ⚠️ icon for details.";
                    }
                    else
                    {
                        TxtStatus.Text = "✅ No conflicts detected. All active mods are clean!";
                    }
                });
            });
        }

        private void RefreshModsList()
        {
            _modItems.Clear();
            if (!Directory.Exists(ModsDirectory)) return;

            var files = Directory.GetFiles(ModsDirectory);
            foreach (var file in files)
            {
                var fi = new FileInfo(file);

                bool isInstalled = false;
                if (!string.IsNullOrWhiteSpace(TargetDirectory) && Directory.Exists(TargetDirectory))
                {
                    if (CmbInstallType.SelectedIndex == 0) // Single File
                    {
                        string targetFile = Path.Combine(TargetDirectory, fi.Name);
                        isInstalled = File.Exists(targetFile);
                    }
                    else // Extract File
                    {
                        string expectedFolder = Path.Combine(TargetDirectory, Path.GetFileNameWithoutExtension(fi.Name));
                        isInstalled = Directory.Exists(expectedFolder);
                    }
                }

                _modItems.Add(new ModItemData
                {
                    Filename = fi.Name,
                    SizeNum = fi.Length,
                    Size = (fi.Length / 1024.0 / 1024.0).ToString("0.00") + " MB",
                    DateNum = fi.LastWriteTime,
                    DateModified = fi.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    IsChecked = isInstalled
                });
            }
            TxtStatus.Text = $"{_modItems.Count} mods found.";
        }

        private void BtnOpenModsFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(ModsDirectory)) Directory.CreateDirectory(ModsDirectory);
            Process.Start(new ProcessStartInfo { FileName = ModsDirectory, UseShellExecute = true });
        }

        private void BtnRefreshMods_Click(object sender, RoutedEventArgs e)
        {
            RefreshModsList();
        }

        private async void Mod_Checked(object sender, RoutedEventArgs e)
        {
            if (!IsInstallAllowed)
            {
                var plugin = PluginManager.Instance.LoadedPlugins.FirstOrDefault(p => PluginManager.Instance.IsPluginForGame(p, App.Settings.Current.GameDirectory));
                string pName = plugin?.Name ?? "Plugin";
                MessageBox.Show($"Mod installation for this game is managed by the '{pName}' plugin.\n\nPlease use the '{pName}' tab in the menu to manage and toggle mods.", "Managed by Plugin", MessageBoxButton.OK, MessageBoxImage.Information);
                if (sender is CheckBox targetChk) targetChk.IsChecked = false;
                return;
            }
            if (sender is CheckBox chk && chk.DataContext is ModItemData mod)
            {
                string targetDir = TargetDirectory;
                if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
                {
                    MessageBox.Show(
                        "Game Directory is not configured or invalid. Please set it in Settings first.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    chk.IsChecked = false;
                    return;
                }

                string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                if (!File.Exists(sourceFile)) return;

                chk.IsEnabled = false;
                var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
                if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
                TxtStatus.Text = $"Processing: {mod.Filename}...";

                try
                {
                    await Task.Run(() =>
                    {
                        if (CmbInstallType.SelectedIndex == 0) // Single File
                        {
                            string destFile = Path.Combine(targetDir, mod.Filename);
                            File.Copy(sourceFile, destFile, true);
                        }
                        else // Extract File
                        {
                            if (sourceFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                ZipFile.ExtractToDirectory(sourceFile, targetDir, true);
                            }
                            else
                            {
                                Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Only .zip files are supported for extraction.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                                Application.Current.Dispatcher.Invoke(() => chk.IsChecked = false);
                            }
                        }
                    });

                    TxtStatus.Text = CmbInstallType.SelectedIndex == 0 ? $"Installed: {mod.Filename}" : $"Extracted: {mod.Filename}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to install mod: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    chk.IsChecked = false;
                }
                finally
                {
                    chk.IsEnabled = true;
                    if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void Mod_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.DataContext is ModItemData mod)
            {
                string targetDir = TargetDirectory;
                if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir)) return;

                chk.IsEnabled = false;
                var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
                if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
                TxtStatus.Text = $"Removing: {mod.Filename}...";

                try
                {
                    await Task.Run(() =>
                    {
                        if (CmbInstallType.SelectedIndex == 0) // Single File
                        {
                            string destFile = Path.Combine(targetDir, mod.Filename);
                            if (File.Exists(destFile))
                            {
                                File.Delete(destFile);
                            }
                        }
                        else // Extract File
                        {
                            if (mod.Filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                                if (File.Exists(sourceFile))
                                {
                                    using var archive = ZipFile.OpenRead(sourceFile);
                                    foreach (var entry in archive.Entries)
                                    {
                                        if (string.IsNullOrEmpty(entry.Name)) continue;
                                        string destFile = Path.Combine(targetDir, entry.FullName);
                                        if (File.Exists(destFile))
                                            File.Delete(destFile);
                                    }
                                }
                            }
                        }
                    });

                    TxtStatus.Text = $"Uninstalled: {mod.Filename}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to uninstall {mod.Filename}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    chk.IsChecked = true; // Revert
                }
                finally
                {
                    chk.IsEnabled = true;
                    if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
                }
            }
        }

        private async void BtnImportMod_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Mod Files (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                Directory.CreateDirectory(ModsDirectory);
                int imported = 0;
                
                var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
                if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
                TxtStatus.Text = $"Importing {dialog.FileNames.Length} file(s)...";

                await Task.Run(async () =>
                {
                    foreach (var file in dialog.FileNames)
                    {
                        try
                        {
                            string fileName = Path.GetFileName(file);
                            string dest = Path.Combine(ModsDirectory, fileName);
                            File.Copy(file, dest, true);
                            imported++;
    
                            // Trigger P2P sync for room
                            if (App.Server != null && App.Server.IsRunning)
                            {
                                App.Server.DeletedMods.TryRemove(fileName, out _);
                                App.Server.TriggerCacheRefresh();
                            }
                            else if (App.Client != null && App.Client.IsConnected)
                            {
                                await App.Client.UploadModAsync(dest, fileName);
                            }
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"Failed to import {Path.GetFileName(file)}: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error));
                        }
                    }
                });

                if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
                TxtStatus.Text = $"Imported {imported} file(s).";
                RefreshModsList();
            }
        }

        private void BtnBackupChecked_Click(object sender, RoutedEventArgs e)
        {
            var checkedItems = _modItems.Where(i => i.IsChecked).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show("No mods checked to backup.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            string backupDir = Path.Combine(ModsDirectory, "Backups");
            Directory.CreateDirectory(backupDir);
            int count = 0;
            foreach (var mod in checkedItems)
            {
                try
                {
                    string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                    if (File.Exists(sourceFile))
                    {
                        string backupFile = Path.Combine(backupDir, mod.Filename);
                        File.Copy(sourceFile, backupFile, true);
                        count++;
                    }
                }
                catch { }
            }
            TxtStatus.Text = $"Backed up {count} mod(s).";
            MessageBox.Show($"Successfully backed up {count} mod(s) to:\n{backupDir}", "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void BtnDeleteChecked_Click(object sender, RoutedEventArgs e)
        {
            var checkedItems = _modItems.Where(i => i.IsChecked).ToList();
            if (checkedItems.Count == 0)
            {
                MessageBox.Show("No mods checked to delete.", "Delete", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete {checkedItems.Count} mod(s)? They will be moved to the recycle bin and deleted for everyone else in the room.",
                "Delete Mods", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                string recycleDir = Path.Combine(ModsDirectory, ".recycle_mods");
                Directory.CreateDirectory(recycleDir);

                await System.Threading.Tasks.Task.Run(async () =>
                {
                    foreach (var mod in checkedItems)
                    {
                        string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                        if (File.Exists(sourceFile))
                        {
                            try
                            {
                                string recyclePath = Path.Combine(recycleDir, mod.Filename);
                                File.Move(sourceFile, recyclePath, true);

                                if (App.Server != null && App.Server.IsRunning)
                                {
                                    App.Server.DeletedMods.TryAdd(mod.Filename, App.Server.HostUsername);
                                }
                                else if (App.Client != null)
                                {
                                    await App.Client.DeleteModAsync(mod.Filename);
                                }
                            }
                            catch { }
                        }
                    }
                });

                RefreshModsList();
                TxtStatus.Text = $"Deleted {checkedItems.Count} mod(s).";
            }
        }

        private async void MenuItem_Delete_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.DataContext is ModItemData mod)
            {
                var result = MessageBox.Show($"Are you sure you want to delete '{mod.Filename}'? It will be moved to the recycle bin and deleted for everyone else in the room.", 
                    "Delete Mod", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                
                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                        if (File.Exists(sourceFile))
                        {
                            string recycleDir = Path.Combine(ModsDirectory, ".recycle_mods");
                            Directory.CreateDirectory(recycleDir);
                            string recyclePath = Path.Combine(recycleDir, mod.Filename);
                            File.Move(sourceFile, recyclePath, true);

                            if (App.Server != null && App.Server.IsRunning)
                            {
                                App.Server.DeletedMods.TryAdd(mod.Filename, App.Server.HostUsername);
                            }
                            else if (App.Client != null)
                            {
                                await App.Client.DeleteModAsync(mod.Filename);
                            }

                            TxtStatus.Text = $"Deleted: {mod.Filename}";
                            RefreshModsList();
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to delete {mod.Filename}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void MenuItem_Backup_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.DataContext is ModItemData mod)
            {
                try
                {
                    string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                    if (File.Exists(sourceFile))
                    {
                        string backupDir = Path.Combine(ModsDirectory, "Backups");
                        Directory.CreateDirectory(backupDir);
                        string backupFile = Path.Combine(backupDir, mod.Filename);
                        File.Copy(sourceFile, backupFile, true);
                        TxtStatus.Text = $"Backed up: {mod.Filename}";
                        MessageBox.Show($"Mod successfully backed up to:\n{backupFile}", "Backup Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to backup {mod.Filename}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuItem_OpenLocation_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menu && menu.DataContext is ModItemData mod)
            {
                try
                {
                    string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                    if (File.Exists(sourceFile))
                    {
                        Process.Start("explorer.exe", $"/select,\"{sourceFile}\"");
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open location:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void MenuMoveToPreset_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            PopulatePresetSubmenu(sender as MenuItem, isCopy: false);
        }

        private void MenuCopyToPreset_SubmenuOpened(object sender, RoutedEventArgs e)
        {
            PopulatePresetSubmenu(sender as MenuItem, isCopy: true);
        }

        private void PopulatePresetSubmenu(MenuItem? menuItem, bool isCopy)
        {
            if (menuItem == null) return;
            menuItem.Items.Clear();

            var presetNames = ModpackManager.Instance.GetPresetNames();
            if (presetNames.Count == 0)
            {
                menuItem.Items.Add(new MenuItem { Header = "(No Presets Created Yet)", IsEnabled = false });
            }
            else
            {
                foreach (var name in presetNames)
                {
                    var pItem = new MenuItem { Header = name };
                    var mod = menuItem.DataContext as ModItemData;
                    pItem.Click += (_, _) => MoveModToPreset(mod, name, isCopy);
                    menuItem.Items.Add(pItem);
                }
            }
        }

        private void MoveModToPreset(ModItemData? mod, string presetName, bool isCopy)
        {
            if (mod == null) return;
            string sourceFile = Path.Combine(ModsDirectory, mod.Filename);
            if (!File.Exists(sourceFile) && !Directory.Exists(sourceFile))
            {
                MessageBox.Show($"Mod file not found: {mod.Filename}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string presetDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ModTogether", "Presets", presetName);
            Directory.CreateDirectory(presetDir);
            string destFile = Path.Combine(presetDir, mod.Filename);

            try
            {
                if (Directory.Exists(sourceFile))
                {
                    ModpackManager.Instance.CopyDirectory(sourceFile, destFile);
                    if (!isCopy) Directory.Delete(sourceFile, true);
                }
                else
                {
                    File.Copy(sourceFile, destFile, true);
                    if (!isCopy) File.Delete(sourceFile);
                }

                TxtStatus.Text = isCopy ? $"Copied {mod.Filename} -> Preset '{presetName}'" : $"Moved {mod.Filename} -> Preset '{presetName}'";
                if (!isCopy) RefreshModsList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to move/copy mod to preset:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region Profile Presets Handlers

        private bool _loadingPresets;
        private const string AllModsPreset = "[Off / Default Mods]";

        private void LoadExplorerPresets(string? selectedName = null)
        {
            _loadingPresets = true;
            var savedPreset = SessionManager.Instance.State.SelectedExplorerPreset;
            var currentSelection = selectedName ?? CmbExplorerPresets.SelectedItem as string ?? savedPreset;
            var presets = ModpackManager.Instance.GetPresetNames();
            CmbExplorerPresets.Items.Clear();
            CmbExplorerPresets.Items.Add(AllModsPreset);
            foreach (var name in presets)
            {
                CmbExplorerPresets.Items.Add(name);
            }
            CmbExplorerPresets.SelectedItem = CmbExplorerPresets.Items.Contains(currentSelection) ? currentSelection : AllModsPreset;

            if (CmbInstallType != null)
            {
                CmbInstallType.SelectedIndex = SessionManager.Instance.State.ExplorerInstallTypeIndex;
            }
            _loadingPresets = false;
        }

        private void CmbExplorerPresets_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingPresets || CmbExplorerPresets.SelectedItem is not string presetName)
            {
                return;
            }

            bool isSessionActive = (App.Server != null && App.Server.IsRunning) || (App.Client != null && App.Client.IsConnected);
            if (isSessionActive)
            {
                MessageBox.Show(
                    "⚠️ Changing Mod Presets is strictly locked while an active room session is in progress (Hosting or Joined).\n\nPlease stop hosting or disconnect from the room first to prevent critical synchronization errors.",
                    "Preset Locked During Session", MessageBoxButton.OK, MessageBoxImage.Warning);
                _loadingPresets = true;
                CmbExplorerPresets.SelectedItem = e.RemovedItems.Count > 0 ? e.RemovedItems[0] : AllModsPreset;
                _loadingPresets = false;
                return;
            }

            SessionManager.Instance.State.SelectedExplorerPreset = presetName;
            SessionManager.Instance.Save();

            string oldPreset = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as string ?? "" : "";
            if (oldPreset == AllModsPreset || oldPreset.Contains("Default") || oldPreset.Contains("Off"))
            {
                ModpackManager.Instance.SaveOriginalMods(ModsDirectory);
            }

            if (presetName == AllModsPreset || presetName.Contains("Default") || presetName.Contains("Off"))
            {
                bool restored = ModpackManager.Instance.RestoreOriginalMods(ModsDirectory);
                TxtStatus.Text = restored ? "✅ Restored original mods from AllMods." : "Activated Default Mods.";
            }
            else
            {
                bool loaded = ModpackManager.Instance.LoadPreset(presetName, ModsDirectory);
                if (loaded)
                {
                    TxtStatus.Text = $"✅ Loaded Preset '{presetName}' (Original mods safely stored in AllMods)";
                }
            }

            RefreshModsList();
            App.Server?.TriggerCacheRefresh();
        }

        private void CmbInstallType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingPresets && CmbInstallType != null)
            {
                SessionManager.Instance.State.ExplorerInstallTypeIndex = CmbInstallType.SelectedIndex;
                SessionManager.Instance.Save();
            }
        }

        private bool TryApplyInstallationState(ModItemData mod, bool install, out string error)
        {
            error = string.Empty;
            var targetDir = TargetDirectory;
            if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir))
            {
                error = "Configure a valid game directory in Settings first.";
                return false;
            }

            try
            {
                var sourceFile = Path.Combine(ModsDirectory, mod.Filename);
                if (CmbInstallType.SelectedIndex == 0)
                {
                    var destinationFile = Path.Combine(targetDir, mod.Filename);
                    if (install)
                    {
                        if (!File.Exists(sourceFile))
                        {
                            error = "The source mod file is missing.";
                            return false;
                        }
                        File.Copy(sourceFile, destinationFile, true);
                    }
                    else if (File.Exists(destinationFile))
                    {
                        File.Delete(destinationFile);
                    }
                    return true;
                }

                if (!mod.Filename.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    error = "Extract mode supports .zip files only.";
                    return false;
                }

                if (install)
                {
                    if (!File.Exists(sourceFile))
                    {
                        error = "The source mod file is missing.";
                        return false;
                    }
                    ZipFile.ExtractToDirectory(sourceFile, targetDir, true);
                }
                else if (File.Exists(sourceFile))
                {
                    using var archive = ZipFile.OpenRead(sourceFile);
                    foreach (var entry in archive.Entries.Where(entry => !string.IsNullOrEmpty(entry.Name)))
                    {
                        var destinationFile = Path.GetFullPath(Path.Combine(targetDir, entry.FullName));
                        if (!destinationFile.StartsWith(Path.GetFullPath(targetDir), StringComparison.OrdinalIgnoreCase))
                        {
                            error = "The archive contains an unsafe path.";
                            return false;
                        }
                        if (File.Exists(destinationFile)) File.Delete(destinationFile);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            string name = TxtProfileName != null && !string.IsNullOrWhiteSpace(TxtProfileName.Text) 
                ? TxtProfileName.Text.Trim() 
                : $"Preset_{DateTime.Now:yyyyMMdd_HHmmss}";

            ModpackManager.Instance.SavePreset(name, ModsDirectory);
            TxtStatus.Text = $"✅ Saved Preset '{name}' to Documents/ModTogether/Presets/{name}";
            if (TxtProfileName != null) TxtProfileName.Text = "";
            LoadExplorerPresets(name);
        }

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (CmbExplorerPresets.SelectedItem is string profileName && profileName != AllModsPreset)
            {
                ModpackManager.Instance.DeletePreset(profileName);
                TxtStatus.Text = $"Deleted preset '{profileName}'";
                LoadExplorerPresets();
            }
        }

        #endregion
    }
}
