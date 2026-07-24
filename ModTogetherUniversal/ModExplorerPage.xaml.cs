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
                string dir = App.Settings.Current.ModDirectory;
                return string.IsNullOrWhiteSpace(dir) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameMods") : dir;
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

        private System.IO.FileSystemWatcher? _watcher;

        public ModExplorerPage()
        {
            InitializeComponent();
            DataContext = this;
            ListMods.ItemsSource = _modItems;

            Loaded += (s, e) =>
            {
                ApplyTranslations();
                if (!string.IsNullOrWhiteSpace(ModsDirectory) && !Directory.Exists(ModsDirectory))
                    try { Directory.CreateDirectory(ModsDirectory); } catch { }
                
                if (_watcher == null && Directory.Exists(ModsDirectory))
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
                
                RefreshModsList();
            };
            App.Settings.OnSettingsChanged += () =>
            {
                ApplyTranslations();
                RefreshModsList();
            };
        }

        private void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            if (TxtTitle != null) TxtTitle.Text = I18N.GetString("explorer_title", lang);
            if (TxtDesc != null) TxtDesc.Text = I18N.GetString("explorer_desc", lang);
            if (TxtModFolderLabel != null) TxtModFolderLabel.Text = I18N.GetString("explorer_mod_folder_label", lang);

            // Display current ModDirectory or fallback message
            string modDir = App.Settings.Current.ModDirectory;
            if (TxtModFolderValue != null)
                TxtModFolderValue.Text = string.IsNullOrWhiteSpace(modDir)
                    ? I18N.GetString("explorer_no_mod_folder", lang)
                    : modDir;

            if (TxtInstallTypeLabel != null) TxtInstallTypeLabel.Text = I18N.GetString("explorer_install_type", lang);
            if (CmbItemSingle != null) CmbItemSingle.Content = I18N.GetString("explorer_type_single", lang);
            if (CmbItemExtract != null) CmbItemExtract.Content = I18N.GetString("explorer_type_extract", lang);

            if (BtnOpenModsFolder != null) BtnOpenModsFolder.Content = I18N.GetString("explorer_btn_open", lang);
            if (BtnRefreshMods != null) BtnRefreshMods.Content = I18N.GetString("explorer_btn_refresh", lang);
            if (BtnCheckAll != null) BtnCheckAll.Content = I18N.GetString("btn_check_all", lang);
            if (BtnUncheckAll != null) BtnUncheckAll.Content = I18N.GetString("btn_uncheck_all", lang);
            if (BtnImportMod != null) BtnImportMod.Content = I18N.GetString("btn_import", lang);
            if (BtnDeleteChecked != null) BtnDeleteChecked.Content = I18N.GetString("btn_delete_checked", lang);
            if (BtnBackupChecked != null) BtnBackupChecked.Content = I18N.GetString("btn_backup", lang);

            ColInstallText = I18N.GetString("explorer_col_install", lang);
            ColFilenameText = I18N.GetString("explorer_col_filename", lang);
            ColSizeText = I18N.GetString("explorer_col_size", lang);
            ColModifiedText = I18N.GetString("explorer_col_modified", lang);
            MenuDeleteText = I18N.GetString("btn_delete_mod", lang);
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

        private void Mod_Checked(object sender, RoutedEventArgs e)
        {
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

                try
                {
                    if (CmbInstallType.SelectedIndex == 0) // Single File
                    {
                        string destFile = Path.Combine(targetDir, mod.Filename);
                        File.Copy(sourceFile, destFile, true);
                        TxtStatus.Text = $"Installed: {mod.Filename}";
                    }
                    else // Extract File
                    {
                        if (sourceFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                        {
                            ZipFile.ExtractToDirectory(sourceFile, targetDir, true);
                            TxtStatus.Text = $"Extracted: {mod.Filename}";
                        }
                        else
                        {
                            MessageBox.Show("Only .zip files are supported for extraction.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            chk.IsChecked = false;
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to install mod: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    chk.IsChecked = false;
                }
            }
        }

        private void Mod_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.DataContext is ModItemData mod)
            {
                string targetDir = TargetDirectory;
                if (string.IsNullOrWhiteSpace(targetDir) || !Directory.Exists(targetDir)) return;

                try
                {
                    if (CmbInstallType.SelectedIndex == 0) // Single File
                    {
                        string destFile = Path.Combine(targetDir, mod.Filename);
                        if (File.Exists(destFile))
                        {
                            File.Delete(destFile);
                            TxtStatus.Text = $"Uninstalled: {mod.Filename}";
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
                                TxtStatus.Text = $"Uninstalled: {mod.Filename}";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to uninstall {mod.Filename}:\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    chk.IsChecked = true; // Revert
                }
            }
        }

        private void BtnImportMod_Click(object sender, RoutedEventArgs e)
        {
            if (!Directory.Exists(ModsDirectory)) Directory.CreateDirectory(ModsDirectory);
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Mod Files (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar|All Files (*.*)|*.*"
            };
            if (dialog.ShowDialog() == true)
            {
                int imported = 0;
                foreach (var file in dialog.FileNames)
                {
                    try
                    {
                        string dest = Path.Combine(ModsDirectory, Path.GetFileName(file));
                        File.Copy(file, dest, true);
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"Failed to import {Path.GetFileName(file)}: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                if (imported > 0)
                {
                    TxtStatus.Text = $"Imported {imported} mod(s).";
                    RefreshModsList();
                }
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
    }
}
