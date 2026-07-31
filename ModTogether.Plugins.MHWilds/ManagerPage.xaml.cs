using System;
using System.Windows;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using ModTogether.Plugins.MHWilds.Models;

namespace ModTogether.Plugins.MHWilds
{
    public class ModItemData : INotifyPropertyChanged
    {
        private bool _isChecked;
        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }
        public string Filename { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DateModified { get; set; } = string.Empty;
        public DateTime DateNum { get; set; }
        public string Size { get; set; } = string.Empty;
        public long SizeNum { get; set; }
        public System.Windows.Media.SolidColorBrush? BackgroundColor { get; set; }
        
        // Metadata fields
        public string ThumbnailPath { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        
        private bool _isInstalled;
        public bool IsInstalled
        {
            get => _isInstalled;
            set
            {
                if (_isInstalled != value)
                {
                    _isInstalled = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsInstalled)));
                }
            }
        }
        
        public System.Collections.ObjectModel.ObservableCollection<ModItemData> SubMods { get; set; } = new System.Collections.ObjectModel.ObservableCollection<ModItemData>();
        public string SubFolderPath { get; set; } = string.Empty;
        public bool IsSubMod { get; set; } = false;

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsProcessing)));
                }
            }
        }

        private string _conflictWarningText = string.Empty;
        public string ConflictWarningText
        {
            get => _conflictWarningText;
            set
            {
                if (_conflictWarningText != value)
                {
                    _conflictWarningText = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictWarningText)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ConflictVisibility)));
                }
            }
        }

        public Visibility ConflictVisibility => string.IsNullOrEmpty(ConflictWarningText) ? Visibility.Collapsed : Visibility.Visible;

        private int _priority = -1;
        public int Priority
        {
            get => _priority;
            set
            {
                if (_priority != value)
                {
                    _priority = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Priority)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PriorityText)));
                }
            }
        }

        public string PriorityText => Priority >= 0 ? $"#{Priority:D3}" : "";

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class ManagerPage : Page
    {
        private bool _autoDetected = false;
        private System.IO.FileSystemWatcher? _watcher;
        private ObservableCollection<ModItemData> _modItems = new ObservableCollection<ModItemData>();
        private System.Collections.Generic.List<ModItemData> _allModItems = new System.Collections.Generic.List<ModItemData>();
        private bool _isScanning = false;
        
        public ManagerPage()
        {
            InitializeComponent();
            
            this.AllowDrop = true;
            this.Drop += ManagerPage_Drop;
            
            this.Loaded += async (s, e) => 
            {
                if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath()) return;
                var listMods = this.FindName("ListMods") as ListView;
                if (listMods != null) listMods.ItemsSource = _modItems;
                await RunAutoDetectAsync();
                ScanMods();
            };
        }

        private async System.Threading.Tasks.Task RunAutoDetectAsync()
        {
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath()) return;
            if (_autoDetected) return;
            
            string cacheDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods");
            System.IO.Directory.CreateDirectory(cacheDir);

            if (_watcher == null)
            {
                _watcher = new System.IO.FileSystemWatcher(cacheDir)
                {
                    NotifyFilter = System.IO.NotifyFilters.FileName | System.IO.NotifyFilters.LastWrite,
                    IncludeSubdirectories = false,
                    EnableRaisingEvents = true
                };
                
                System.IO.FileSystemEventHandler handler = (s, e) => 
                {
                    Application.Current.Dispatcher.InvokeAsync(() => ScanMods());
                };
                System.IO.RenamedEventHandler renamedHandler = (s, e) =>
                {
                    Application.Current.Dispatcher.InvokeAsync(() => ScanMods());
                };

                _watcher.Created += handler;
                _watcher.Deleted += handler;
                _watcher.Changed += handler;
                _watcher.Renamed += renamedHandler;
            }

            if (App.Installer == null)
            {
                App.Installer = new Services.PakModInstaller(App.Settings.Current.MhwDirectory);
                App.Installer.OnLog += msg => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log(msg));
                App.Installer.OnInstallProgress += pct => Application.Current.Dispatcher.Invoke(() => 
                {
                    MainWindow.Instance?.UpdateInstallProgress((int)pct);
                });
            }

            _autoDetected = true;
        }

        private async void ManagerPage_Drop(object sender, DragEventArgs e)
        {
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath()) return;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                string cacheDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods");
                System.IO.Directory.CreateDirectory(cacheDir);

                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var file in files)
                    {
                        try
                        {
                            string dest = System.IO.Path.Combine(cacheDir, System.IO.Path.GetFileName(file));
                            if (System.IO.Directory.Exists(file))
                            {
                                // Handle folder drop
                                CopyDirectory(file, dest);
                            }
                            else
                            {
                                // Copy mod file (.zip, .7z, .rar, .pak, etc.) directly without extracting
                                System.IO.File.Copy(file, dest, true);
                            }
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"❌ Failed to import {System.IO.Path.GetFileName(file)}: {ex.Message}"));
                        }
                    }
                });
                ScanMods();
            }
        }

        private static void CopyDirectory(string sourceDir, string destDir)
        {
            System.IO.Directory.CreateDirectory(destDir);
            foreach (var file in System.IO.Directory.GetFiles(sourceDir))
                System.IO.File.Copy(file, System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(file)), true);
            foreach (var dir in System.IO.Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, System.IO.Path.Combine(destDir, System.IO.Path.GetFileName(dir)));
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ScanMods();
        }

        private void ScanMods()
        {
            if (_isScanning) return;
            _isScanning = true;
            
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods == null) { _isScanning = false; return; }
            
            listMods.SelectionChanged -= ListMods_SelectionChanged;
            
            string? previousSelectedFilename = (listMods.SelectedItem as ModItemData)?.Filename;
            _allModItems.Clear();
            _modItems.Clear();

            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir) || !System.IO.Directory.Exists(mhwDir))
            {
                listMods.SelectionChanged += ListMods_SelectionChanged;
                _isScanning = false;
                return;
            }

            string cacheDir = System.IO.Path.Combine(mhwDir, "GameMods");
            System.IO.Directory.CreateDirectory(cacheDir);

            if (App.Installer == null)
            {
                App.Installer = new Services.PakModInstaller(mhwDir);
                App.Installer.OnLog += msg => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log(msg));
                App.Installer.OnInstallProgress += pct => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.UpdateInstallProgress((int)pct));
            }

            var allEntries = System.IO.Directory.GetFileSystemEntries(cacheDir);

            foreach (var entry in allEntries)
            {
                try
                {
                    bool isDir = System.IO.Directory.Exists(entry);
                    string ext = isDir ? "" : System.IO.Path.GetExtension(entry).ToLower();
                    
                    if (isDir || ext == ".zip" || ext == ".7z" || ext == ".rar" || ext == ".pak")
                    {
                        string filename = System.IO.Path.GetFileName(entry);
                        var isInstalled = App.Installer.IsModInstalled(filename);
                        
                        DateTime lastWrite;
                        long size;
                        
                        if (isDir)
                        {
                            var di = new System.IO.DirectoryInfo(entry);
                            lastWrite = di.LastWriteTime;
                            size = 0; // Or calculate total size recursively if desired
                        }
                        else
                        {
                            var fi = new System.IO.FileInfo(entry);
                            lastWrite = fi.LastWriteTime;
                            size = fi.Length;
                        }
                        
                        var item = new ModItemData
                        {
                            Filename = filename,
                            DisplayName = filename,
                            DateModified = lastWrite.ToString("yyyy-MM-dd HH:mm"),
                            DateNum = lastWrite,
                            Size = size > 0 ? (size / 1024.0 / 1024.0).ToString("0.00") + " MB" : "Folder",
                            SizeNum = size,
                            IsInstalled = isInstalled
                        };

                        if (isDir)
                        {
                            ParseModInfo(entry, item);
                        }
                        else if (ext == ".zip" || ext == ".7z" || ext == ".rar")
                        {
                            ParseZipModInfo(entry, item);
                        }
                        
                        _allModItems.Add(item);
                        _modItems.Add(item);
                    }
                }
                catch { }
            }

            // Assign priorities
            foreach (var item in _allModItems)
            {
                if (item.IsInstalled && App.Installer != null)
                {
                    item.Priority = App.Installer.GetModPriority(item.Filename);
                }
                else
                {
                    item.Priority = -1;
                }
            }

            ApplyFilter();

            // Update status counts
            int installedCount = _modItems.Count(m => m.IsInstalled);
            var txtStatus = this.FindName("TxtStatus") as TextBlock;
            var txtModCount = this.FindName("TxtModCount") as TextBlock;
            if (txtStatus != null) txtStatus.Text = $"{_modItems.Count} mods in library";
            if (txtModCount != null) txtModCount.Text = $"{installedCount}/{_modItems.Count} installed";

            listMods.SelectionChanged += ListMods_SelectionChanged;
            if (listMods.SelectedItem != null) ListMods_SelectionChanged(listMods, null!);
            else
            {
                var lblTitle = this.FindName("LblModTitle") as TextBlock;
                if (lblTitle != null) lblTitle.Text = "Select a mod";
                var lblDesc = this.FindName("LblModDesc") as TextBlock;
                if (lblDesc != null) lblDesc.Text = "";
                var lblAuthor = this.FindName("LblModAuthor") as TextBlock;
                if (lblAuthor != null) lblAuthor.Text = "";
                var lblVersion = this.FindName("LblModVersion") as TextBlock;
                if (lblVersion != null) lblVersion.Text = "";
                var imgPreview = this.FindName("ImgPreview") as Image;
                if (imgPreview != null) imgPreview.Source = null;
                var btnIn = this.FindName("BtnInstall") as Wpf.Ui.Controls.Button;
                var btnUn = this.FindName("BtnUninstall") as Wpf.Ui.Controls.Button;
                var btnDel = this.FindName("BtnDelete") as Wpf.Ui.Controls.Button;
                if (btnIn != null) btnIn.IsEnabled = false;
                if (btnUn != null) btnUn.IsEnabled = false;
                if (btnDel != null) btnDel.IsEnabled = false;
            }
            
            _isScanning = false;
        }

        private void ListMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item)
            {
                var lblTitle = this.FindName("LblModTitle") as TextBlock;
                var lblVersion = this.FindName("LblModVersion") as TextBlock;
                var lblDesc = this.FindName("LblModDesc") as TextBlock;
                var lblAuthor = this.FindName("LblModAuthor") as TextBlock;
                var imgPreview = this.FindName("ImgPreview") as Image;
                var badgeCategory = this.FindName("BadgeCategory") as System.Windows.Controls.Border;
                var txtCategory = this.FindName("TxtCategory") as TextBlock;
                var btnIn = this.FindName("BtnInstall") as Wpf.Ui.Controls.Button;
                var btnUn = this.FindName("BtnUninstall") as Wpf.Ui.Controls.Button;
                var btnDel = this.FindName("BtnDelete") as Wpf.Ui.Controls.Button;

                if (btnIn != null) btnIn.IsEnabled = !item.IsInstalled;
                if (btnUn != null) btnUn.IsEnabled = item.IsInstalled;
                if (btnDel != null) btnDel.IsEnabled = true;

                if (lblTitle != null) lblTitle.Text = item.DisplayName;
                
                var versionText = string.IsNullOrEmpty(item.Version) ? "" : $"v{item.Version}";
                if (lblVersion != null) lblVersion.Text = versionText;
                var badgeVersion = this.FindName("BadgeVersion") as System.Windows.Controls.Border;
                if (badgeVersion != null) badgeVersion.Visibility = string.IsNullOrEmpty(versionText) ? Visibility.Collapsed : Visibility.Visible;
                if (lblAuthor != null) lblAuthor.Text = string.IsNullOrEmpty(item.Author) ? "" : item.Author;
                if (lblDesc != null) lblDesc.Text = item.Description;

                if (badgeCategory != null && txtCategory != null)
                {
                    if (!string.IsNullOrEmpty(item.Category))
                    {
                        txtCategory.Text = item.Category;
                        badgeCategory.Visibility = Visibility.Visible;
                    }
                    else
                    {
                        badgeCategory.Visibility = Visibility.Collapsed;
                    }
                }
                
                if (imgPreview != null)
                {
                    if (!string.IsNullOrEmpty(item.ThumbnailPath) && System.IO.File.Exists(item.ThumbnailPath))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(item.ThumbnailPath);
                            bitmap.EndInit();
                            imgPreview.Source = bitmap;
                        }
                        catch
                        {
                            imgPreview.Source = null;
                        }
                    }
                    else
                    {
                        imgPreview.Source = null;
                    }
                }
                
                var listSubMods = this.FindName("ListSubMods") as ListBox;
                if (listSubMods != null)
                {
                    listSubMods.ItemsSource = item.SubMods;
                }
            }
        }

        private void ListSubMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var listSubMods = this.FindName("ListSubMods") as ListBox;
            var listMods = this.FindName("ListMods") as ListView;
            var mainItem = listMods?.SelectedItem as ModItemData;
            
            var targetItem = listSubMods?.SelectedItem as ModItemData ?? mainItem;
            if (targetItem != null)
            {
                var lblDesc = this.FindName("LblModDesc") as TextBlock;
                var imgPreview = this.FindName("ImgPreview") as Image;
                
                if (lblDesc != null) lblDesc.Text = targetItem.Description;
                
                if (imgPreview != null)
                {
                    if (!string.IsNullOrEmpty(targetItem.ThumbnailPath) && System.IO.File.Exists(targetItem.ThumbnailPath))
                    {
                        try
                        {
                            var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(targetItem.ThumbnailPath);
                            bitmap.EndInit();
                            imgPreview.Source = bitmap;
                        }
                        catch { imgPreview.Source = null; }
                    }
                    else
                    {
                        if (targetItem.IsSubMod && mainItem != null && !string.IsNullOrEmpty(mainItem.ThumbnailPath) && System.IO.File.Exists(mainItem.ThumbnailPath))
                        {
                            try
                            {
                                var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                                bitmap.BeginInit();
                                bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                                bitmap.UriSource = new Uri(mainItem.ThumbnailPath);
                                bitmap.EndInit();
                                imgPreview.Source = bitmap;
                            }
                            catch { imgPreview.Source = null; }
                        }
                        else
                        {
                            imgPreview.Source = null;
                        }
                    }
                }
            }
        }

        // --- Filter and Search Handlers ---

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void CmbFilter_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var txtSearch = this.FindName("TxtSearch") as Wpf.Ui.Controls.TextBox;
            var cmbFilter = this.FindName("CmbFilter") as ComboBox;
            string query = txtSearch?.Text?.Trim().ToLower() ?? "";
            int filterIdx = cmbFilter?.SelectedIndex ?? 0;

            _modItems.Clear();
            foreach (var item in _allModItems)
            {
                bool matchesQuery = string.IsNullOrEmpty(query) ||
                    item.DisplayName.ToLower().Contains(query) ||
                    item.Filename.ToLower().Contains(query) ||
                    item.Author.ToLower().Contains(query) ||
                    item.Category.ToLower().Contains(query);

                bool matchesStatus = filterIdx switch
                {
                    1 => item.IsInstalled,
                    2 => !item.IsInstalled,
                    _ => true
                };

                if (matchesQuery && matchesStatus)
                {
                    _modItems.Add(item);
                }
            }
        }

        // --- Toolbar Handlers ---

        private void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir)) return;

            string exePath1 = System.IO.Path.Combine(mhwDir, "MonsterHunterWilds.exe");
            string exePath2 = System.IO.Path.Combine(mhwDir, "MonsterHunterWildsBeta.exe");

            string? targetExe = System.IO.File.Exists(exePath1) ? exePath1 : (System.IO.File.Exists(exePath2) ? exePath2 : null);

            if (targetExe != null)
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = targetExe,
                        WorkingDirectory = mhwDir,
                        UseShellExecute = true
                    });
                    MainWindow.Instance?.Log($"[🚀] Launching game: {targetExe}");
                }
                catch (Exception ex)
                {
                    MainWindow.Instance?.Log($"[❌] Failed to launch game: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "steam://rungameid/2246340",
                        UseShellExecute = true
                    });
                    MainWindow.Instance?.Log("[🚀] Launching Monster Hunter Wilds via Steam protocol...");
                }
                catch (Exception ex)
                {
                    MainWindow.Instance?.Log($"[❌] Could not find executable or launch via Steam: {ex.Message}");
                }
            }
        }

        private async void BtnScanConflicts_Click(object sender, RoutedEventArgs e)
        {
            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir)) return;
            string cacheDir = System.IO.Path.Combine(mhwDir, "GameMods");

            var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
            if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;

            System.Collections.Generic.Dictionary<string, Services.ConflictInfo>? conflicts = null;

            await System.Threading.Tasks.Task.Run(() =>
            {
                conflicts = Services.ConflictScanner.ScanConflicts(cacheDir);
            });

            if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;

            int count = 0;
            foreach (var item in _allModItems)
            {
                if (conflicts != null && conflicts.TryGetValue(item.Filename, out var cInfo))
                {
                    item.ConflictWarningText = $"Conflicts with: {string.Join(", ", cInfo.ConflictingMods)} ({cInfo.ConflictingFiles.Count} file(s))";
                    count++;
                }
                else
                {
                    item.ConflictWarningText = string.Empty;
                }
            }

            MainWindow.Instance?.Log(count > 0 
                ? $"[⚠️] Conflict scan complete. Found {count} mod(s) with conflicting files."
                : "[✅] Conflict scan complete. No file conflicts detected.");
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir)) return;

            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Mod State Backup",
                Filter = "JSON State File (*.json)|*.json",
                FileName = $"installed_mods_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            };

            if (dialog.ShowDialog() == true)
            {
                if (Services.BackupManager.CreateBackup(mhwDir, dialog.FileName))
                {
                    MainWindow.Instance?.Log($"[✅] Backup saved to: {dialog.FileName}");
                }
                else
                {
                    MainWindow.Instance?.Log("[❌] Failed to create backup.");
                }
            }
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir)) return;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Restore Mod State Backup",
                Filter = "JSON State File (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                if (Services.BackupManager.RestoreBackup(mhwDir, dialog.FileName))
                {
                    MainWindow.Instance?.Log($"[✅] Backup restored from: {dialog.FileName}");
                    App.Installer = new Services.PakModInstaller(mhwDir);
                    ScanMods();
                }
                else
                {
                    MainWindow.Instance?.Log("[❌] Failed to restore backup.");
                }
            }
        }

        private async void BtnImportMod_Click(object sender, RoutedEventArgs e)
        {
            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir) || !System.IO.Directory.Exists(mhwDir))
            {
                MainWindow.Instance?.Log("[❌] Game directory not configured. Please set it in Settings.");
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Mod Archive",
                Filter = "Mod Archives|*.zip;*.7z;*.rar;*.pak|All Files|*.*",
                Multiselect = true
            };

            if (dialog.ShowDialog() == true)
            {
                string cacheDir = System.IO.Path.Combine(mhwDir, "GameMods");
                System.IO.Directory.CreateDirectory(cacheDir);

                await System.Threading.Tasks.Task.Run(() =>
                {
                    foreach (var file in dialog.FileNames)
                    {
                        try
                        {
                            string dest = System.IO.Path.Combine(cacheDir, System.IO.Path.GetFileName(file));
                            System.IO.File.Copy(file, dest, true);
                            Application.Current.Dispatcher.Invoke(() =>
                                MainWindow.Instance?.Log($"[INFO] Imported: {System.IO.Path.GetFileName(file)}"));
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() =>
                                MainWindow.Instance?.Log($"[❌] Failed to import {System.IO.Path.GetFileName(file)}: {ex.Message}"));
                        }
                    }
                });
                ScanMods();
            }
        }

        private void BtnOpenModsFolder_Click(object sender, RoutedEventArgs e)
        {
            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir)) { MainWindow.Instance?.Log("[❌] Game directory not configured."); return; }
            string modsFolder = System.IO.Path.Combine(mhwDir, "GameMods");
            System.IO.Directory.CreateDirectory(modsFolder);
            System.Diagnostics.Process.Start("explorer.exe", modsFolder);
        }

        private async void BtnUninstallAll_Click(object sender, RoutedEventArgs e)
        {
            var installed = _modItems.Where(m => m.IsInstalled).ToList();
            if (installed.Count == 0)
            {
                MainWindow.Instance?.Log("[INFO] No mods are currently installed.");
                return;
            }

            var result = MessageBox.Show(
                $"Uninstall all {installed.Count} installed mod(s)?",
                "Confirm Uninstall All",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            foreach (var item in installed)
            {
                item.IsProcessing = true;
            }

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var item in installed)
                {
                    string modKey = item.Filename;
                    App.Installer?.UninstallMod(modKey);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.IsInstalled = false;
                        item.IsProcessing = false;
                    });
                }
            });

            ScanMods();
        }

        // --- Context Menu Handlers ---

        private void MenuMoveUp_Click(object sender, RoutedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item && item.IsInstalled)
            {
                if (App.Installer?.MoveModPriority(item.Filename, -1) == true)
                {
                    ScanMods();
                }
            }
        }

        private void MenuMoveDown_Click(object sender, RoutedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item && item.IsInstalled)
            {
                if (App.Installer?.MoveModPriority(item.Filename, 1) == true)
                {
                    ScanMods();
                }
            }
        }

        private void MenuOpenLocation_Click(object sender, RoutedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item)
            {
                string mhwDir = App.Settings.Current.MhwDirectory;
                string fullPath = System.IO.Path.Combine(mhwDir, "GameMods", item.Filename);
                if (System.IO.File.Exists(fullPath))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{fullPath}\"");
                else if (System.IO.Directory.Exists(fullPath))
                    System.Diagnostics.Process.Start("explorer.exe", fullPath);
            }
        }

        private void MenuDeleteMod_Click(object sender, RoutedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item)
            {
                var result = MessageBox.Show(
                    $"Delete '{item.DisplayName}' from library? It will be moved to the recycle bin.",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                string mhwDir = App.Settings.Current.MhwDirectory;
                string fullPath = System.IO.Path.Combine(mhwDir, "GameMods", item.Filename);
                string recycleDir = System.IO.Path.Combine(mhwDir, "GameMods", ".recycle_mods");

                if (App.Installer?.IsModInstalled(item.Filename) == true)
                    App.Installer.UninstallMod(item.Filename);

                try
                {
                    System.IO.Directory.CreateDirectory(recycleDir);
                    string recyclePath = System.IO.Path.Combine(recycleDir, item.Filename);

                    if (System.IO.Directory.Exists(fullPath))
                    {
                        if (System.IO.Directory.Exists(recyclePath)) System.IO.Directory.Delete(recyclePath, true);
                        ModTogether.API.FileHelper.SafeMoveDirectory(fullPath, recyclePath);
                    }
                    else if (System.IO.File.Exists(fullPath))
                    {
                        ModTogether.API.FileHelper.SafeMove(fullPath, recyclePath, true);
                    }
                    MainWindow.Instance?.Log($"🗑️ Mod moved to recycle bin: {item.Filename}");
                }
                catch (Exception ex)
                {
                    MainWindow.Instance?.Log($"[❌] Delete failed: {ex.Message}");
                }
                ScanMods();
            }
        }

        private void BtnToggleSubMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is ModItemData item)
            {
                _ = ToggleModInstall(item, !item.IsInstalled);
            }
        }

        private async System.Threading.Tasks.Task ToggleModInstall(ModItemData item, bool install)
        {
            try
            {
                item.IsProcessing = true;
                string mhwDir = App.Settings.Current.MhwDirectory;
                if (string.IsNullOrEmpty(mhwDir) || !System.IO.Directory.Exists(mhwDir))
                {
                    Application.Current.Dispatcher.Invoke(() => 
                        MainWindow.Instance?.Log("❌ Cannot install mod: MH Wilds game directory is empty or invalid. Please configure it in Settings."));
                    return;
                }

                if (App.Installer == null)
                {
                    App.Installer = new Services.PakModInstaller(mhwDir);
                    App.Installer.OnLog += msg => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log(msg));
                    App.Installer.OnInstallProgress += pct => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.UpdateInstallProgress((int)pct));
                }

                string fullPath = System.IO.Path.Combine(mhwDir, "GameMods", item.Filename);
                string modKey = item.IsSubMod ? $"{item.Filename}|{item.SubFolderPath}" : item.Filename;

                Application.Current.Dispatcher.Invoke(() => 
                    MainWindow.Instance?.Log($"[INFO] Triggering {(install ? "Install" : "Uninstall")} for '{item.DisplayName}' ({modKey})..."));

                await System.Threading.Tasks.Task.Run(() =>
                {
                    if (install) App.Installer.InstallMod(fullPath, modKey, item.SubFolderPath);
                    else App.Installer.UninstallMod(modKey);
                });

                bool isInstalledNow = App.Installer.IsModInstalled(modKey);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.IsInstalled = isInstalledNow;
                    item.IsChecked = isInstalledNow;
                });
            }
            catch (Exception ex)
            {
                Application.Current.Dispatcher.Invoke(() => 
                    MainWindow.Instance?.Log($"❌ Error during mod install/uninstall: {ex.Message}"));
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() => item.IsProcessing = false);
            }
        }

        private void ParseModInfo(string folderPath, ModItemData item)
        {
            try
            {
                var iniFiles = System.IO.Directory.GetFiles(folderPath, "modinfo.ini", System.IO.SearchOption.AllDirectories);
                if (iniFiles.Length > 0)
                {
                    string coverIni = iniFiles.FirstOrDefault(f => f.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0) ?? iniFiles[0];
                    ParseSingleIniDir(coverIni, item, folderPath);
                    
                    if (iniFiles.Length > 1)
                    {
                        var tempSubs = new System.Collections.Generic.List<ModItemData>();
                        foreach (var subIni in iniFiles)
                        {
                            if (subIni == coverIni) continue;
                            var subItem = new ModItemData { IsSubMod = true, Filename = item.Filename };
                            string subFolder = System.IO.Path.GetDirectoryName(subIni)!;
                            subItem.SubFolderPath = subFolder.Substring(folderPath.Length).TrimStart('\\', '/').Replace("\\", "/");
                            
                            ParseSingleIniDir(subIni, subItem, folderPath);
                            if (string.IsNullOrEmpty(subItem.DisplayName)) subItem.DisplayName = System.IO.Path.GetFileName(subFolder);
                            
                            subItem.IsInstalled = App.Installer?.IsModInstalled(item.Filename + "|" + subItem.SubFolderPath) ?? false;
                            subItem.IsChecked = subItem.IsInstalled;

                            tempSubs.Add(subItem);
                        }

                        CleanSubModNames(tempSubs);
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            foreach(var sub in tempSubs) item.SubMods.Add(sub);
                        });
                    }
                }
            }
            catch { }
        }

        private void ParseSingleIniDir(string iniPath, ModItemData item, string folderPath)
        {
            try
            {
                string modRootPath = System.IO.Path.GetDirectoryName(iniPath)!;
                var lines = System.IO.File.ReadAllLines(iniPath);
                foreach (var line in lines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim().ToLower();
                        string val = parts[1].Trim();
                        switch (key)
                        {
                            case "name": 
                                if (!item.IsSubMod && (string.IsNullOrEmpty(item.DisplayName) || item.DisplayName == item.Filename)) item.DisplayName = val; 
                                break;
                            case "nameasbundle": 
                                if (!item.IsSubMod) item.DisplayName = val; 
                                break;
                            case "version": item.Version = val; break;
                            case "description": item.Description = val; break;
                            case "author": item.Author = val; break;
                            case "category": item.Category = val; break;
                            case "screenshot":
                                string imgPath = System.IO.Path.Combine(modRootPath, val);
                                if (!string.IsNullOrEmpty(val) && System.IO.File.Exists(imgPath)) item.ThumbnailPath = imgPath;
                                break;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(item.ThumbnailPath))
                {
                    string autoPng = System.IO.Path.Combine(modRootPath, "screenshot.png");
                    string autoJpg = System.IO.Path.Combine(modRootPath, "screenshot.jpg");
                    if (System.IO.File.Exists(autoPng)) item.ThumbnailPath = autoPng;
                    else if (System.IO.File.Exists(autoJpg)) item.ThumbnailPath = autoJpg;
                }
            }
            catch { }
        }

        private void ParseZipModInfo(string zipPath, ModItemData item)
        {
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                var options = new SharpCompress.Readers.ReaderOptions
                {
                    ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding { Default = System.Text.Encoding.GetEncoding("Shift_JIS") }
                };

                using var archive = zipPath.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) 
                    ? (SharpCompress.Archives.IArchive)SharpCompress.Archives.SevenZip.SevenZipArchive.Open(zipPath, options)
                    : SharpCompress.Archives.ArchiveFactory.Open(zipPath, options);

                var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                var iniEntries = entries.Where(e => System.IO.Path.GetFileName(e.Key)?.Equals("modinfo.ini", StringComparison.OrdinalIgnoreCase) == true).ToList();
                
                if (iniEntries.Count > 0)
                {
                    var coverEntry = iniEntries.FirstOrDefault(e => e.Key != null && e.Key.IndexOf("cover", StringComparison.OrdinalIgnoreCase) >= 0);
                    var mainEntry = coverEntry ?? (iniEntries.Count == 1 ? iniEntries[0] : null);
                    
                    if (mainEntry != null)
                    {
                        ParseSingleIni(mainEntry, item, zipPath, entries);
                    }
                    
                    if (iniEntries.Count > 1)
                    {
                        var tempSubs = new System.Collections.Generic.List<ModItemData>();
                        foreach (var subEntry in iniEntries)
                        {
                            if (subEntry == coverEntry) continue;
                            var subItem = new ModItemData { IsSubMod = true, Filename = item.Filename };
                            subItem.SubFolderPath = System.IO.Path.GetDirectoryName(subEntry.Key)?.Replace("\\", "/") ?? "";
                            ParseSingleIni(subEntry, subItem, zipPath, entries);
                            if (string.IsNullOrEmpty(subItem.DisplayName)) subItem.DisplayName = System.IO.Path.GetFileName(subItem.SubFolderPath);
                            
                            subItem.IsInstalled = App.Installer?.IsModInstalled(item.Filename + "|" + subItem.SubFolderPath) ?? false;
                            subItem.IsChecked = subItem.IsInstalled;

                            tempSubs.Add(subItem);
                        }

                        CleanSubModNames(tempSubs);
                        Application.Current.Dispatcher.Invoke(() => 
                        {
                            foreach(var sub in tempSubs) item.SubMods.Add(sub);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"ParseZipModInfo error: {ex.Message}");
            }
        }

        private void CleanSubModNames(System.Collections.Generic.List<ModItemData> subs)
        {
            if (subs.Count <= 1) return;
            string prefix = subs[0].DisplayName;
            foreach (var sub in subs)
            {
                int i = 0;
                while (i < prefix.Length && i < sub.DisplayName.Length && char.ToLower(prefix[i]) == char.ToLower(sub.DisplayName[i])) i++;
                prefix = prefix.Substring(0, i);
                if (prefix == "") break;
            }
            
            int lastDelimiter = prefix.LastIndexOfAny(new[] { ' ', '-', '_' });
            if (lastDelimiter > 0)
            {
                prefix = prefix.Substring(0, lastDelimiter + 1);
            }
            
            if (prefix.Length > 3)
            {
                foreach (var sub in subs)
                {
                    string newName = sub.DisplayName.Substring(prefix.Length).Trim(' ', '-', '_');
                    if (!string.IsNullOrEmpty(newName))
                    {
                        sub.DisplayName = newName;
                    }
                }
            }
        }

        private void ParseSingleIni(SharpCompress.Archives.IArchiveEntry iniEntry, ModItemData item, string zipPath, System.Collections.Generic.List<SharpCompress.Archives.IArchiveEntry> entries)
        {
            try
            {
                using var stream = iniEntry.OpenEntryStream();
                using var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8, true);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        string key = parts[0].Trim().ToLower();
                        string val = parts[1].Trim();
                        switch (key)
                        {
                            case "name": 
                                if (!item.IsSubMod && (string.IsNullOrEmpty(item.DisplayName) || item.DisplayName == item.Filename)) item.DisplayName = val; 
                                break;
                            case "nameasbundle": 
                                if (!item.IsSubMod) item.DisplayName = val; 
                                break;
                            case "version": item.Version = val; break;
                            case "description": item.Description = val; break;
                            case "author": item.Author = val; break;
                            case "category": item.Category = val; break;
                            case "screenshot":
                                if (!string.IsNullOrEmpty(val))
                                {
                                    var imgEntry = entries.FirstOrDefault(e => System.IO.Path.GetFileName(e.Key)?.Equals(val, StringComparison.OrdinalIgnoreCase) == true && (e.Key != null && iniEntry.Key != null && System.IO.Path.GetDirectoryName(e.Key) == System.IO.Path.GetDirectoryName(iniEntry.Key)));
                                    if (imgEntry != null)
                                    {
                                        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ModTogether_Thumbnails");
                                        System.IO.Directory.CreateDirectory(tempDir);
                                        string tempFile = System.IO.Path.Combine(tempDir, $"{System.IO.Path.GetFileNameWithoutExtension(zipPath)}_{System.IO.Path.GetFileName(imgEntry.Key)}_{Guid.NewGuid().ToString().Substring(0, 4)}");
                                        if (!System.IO.File.Exists(tempFile))
                                        {
                                            using var imgStream = imgEntry.OpenEntryStream();
                                            using var fileStream = System.IO.File.Create(tempFile);
                                            imgStream.CopyTo(fileStream);
                                        }
                                        item.ThumbnailPath = tempFile;
                                    }
                                }
                                break;
                        }
                    }
                }
                
                if (string.IsNullOrEmpty(item.ThumbnailPath))
                {
                    var fallbackEntry = entries.FirstOrDefault(e => (System.IO.Path.GetFileName(e.Key)?.Equals("screenshot.png", StringComparison.OrdinalIgnoreCase) == true || System.IO.Path.GetFileName(e.Key)?.Equals("screenshot.jpg", StringComparison.OrdinalIgnoreCase) == true) && (e.Key != null && iniEntry.Key != null && System.IO.Path.GetDirectoryName(e.Key) == System.IO.Path.GetDirectoryName(iniEntry.Key)));
                    if (fallbackEntry != null)
                    {
                        string tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "ModTogether_Thumbnails");
                        System.IO.Directory.CreateDirectory(tempDir);
                        string tempFile = System.IO.Path.Combine(tempDir, $"{System.IO.Path.GetFileNameWithoutExtension(zipPath)}_{System.IO.Path.GetFileName(fallbackEntry.Key)}_{Guid.NewGuid().ToString().Substring(0, 4)}");
                        if (!System.IO.File.Exists(tempFile))
                        {
                            using var imgStream = fallbackEntry.OpenEntryStream();
                            using var fileStream = System.IO.File.Create(tempFile);
                            imgStream.CopyTo(fileStream);
                        }
                        item.ThumbnailPath = tempFile;
                    }
                }
            }
            catch { }
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item)
            {
                string filename = item.Filename;
                string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", filename);
                
                var btnIn = this.FindName("BtnInstall") as Wpf.Ui.Controls.Button;
                var btnUn = this.FindName("BtnUninstall") as Wpf.Ui.Controls.Button;
                if (btnIn != null) btnIn.IsEnabled = false;
                if (btnUn != null) btnUn.IsEnabled = false;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    App.Installer?.InstallMod(fullPath, filename);
                });
                ScanMods();
            }
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item)
            {
                string filename = item.Filename;
                var btnIn = this.FindName("BtnInstall") as Wpf.Ui.Controls.Button;
                var btnUn = this.FindName("BtnUninstall") as Wpf.Ui.Controls.Button;
                if (btnIn != null) btnIn.IsEnabled = false;
                if (btnUn != null) btnUn.IsEnabled = false;

                await System.Threading.Tasks.Task.Run(() =>
                {
                    App.Installer?.UninstallMod(filename);
                });
                ScanMods();
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var listMods = this.FindName("ListMods") as ListView;
            if (listMods?.SelectedItem is ModItemData item)
            {
                string filename = item.Filename;
                if (App.Installer?.IsModInstalled(filename) == true)
                {
                    App.Installer.UninstallMod(filename);
                }
                
                string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", filename);
                string recycleDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", ".recycle_mods");
                
                try 
                { 
                    System.IO.Directory.CreateDirectory(recycleDir);
                    string recyclePath = System.IO.Path.Combine(recycleDir, filename);

                    if (System.IO.Directory.Exists(fullPath))
                    {
                        if (System.IO.Directory.Exists(recyclePath)) System.IO.Directory.Delete(recyclePath, true);
                        ModTogether.API.FileHelper.SafeMoveDirectory(fullPath, recyclePath);
                    }
                    else if (System.IO.File.Exists(fullPath))
                    {
                        ModTogether.API.FileHelper.SafeMove(fullPath, recyclePath, true);
                    }
                    MainWindow.Instance?.Log($"🗑️ Mod moved to recycle bin: {filename}");
                } 
                catch (Exception ex)
                {
                    MainWindow.Instance?.Log($"[❌] Delete failed: {ex.Message}");
                }
                ScanMods();
            }
        }
    }
}
