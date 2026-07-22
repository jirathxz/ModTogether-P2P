using System;
using System.Windows;
using System.Windows.Controls;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace ModTogetherMHW
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
        
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class ModFileItemData : INotifyPropertyChanged
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
        public string EntryKey { get; set; } = string.Empty;
        public string DisplayPath { get; set; } = string.Empty;
        
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public partial class ManagerPage : Page
    {
        private static readonly System.Collections.Generic.Dictionary<string, (DateTime LastWriteTime, System.Collections.Generic.List<string> Contents)> _archiveCache = new(StringComparer.OrdinalIgnoreCase);
        private string? _currentLoadedFilename = null;

        private static System.Collections.Generic.List<string> GetCachedArchiveContents(string fullPath)
        {
            var fi = new System.IO.FileInfo(fullPath);
            if (!fi.Exists) return new System.Collections.Generic.List<string>();

            if (_archiveCache.TryGetValue(fullPath, out var cached) && cached.LastWriteTime == fi.LastWriteTime)
            {
                return cached.Contents;
            }

            var contents = Services.ArchiveExtractor.GetArchiveContents(fullPath);
            _archiveCache[fullPath] = (fi.LastWriteTime, contents);
            return contents;
        }

        private bool _autoDetected = false;
        private System.Collections.Generic.List<ModItemData> _allModItems = new System.Collections.Generic.List<ModItemData>();
        private ObservableCollection<ModItemData> _modItems = new ObservableCollection<ModItemData>();
        private ObservableCollection<Models.ModTreeNode> _modTreeNodes = new ObservableCollection<Models.ModTreeNode>();
        private ObservableCollection<ModFileItemData> _modFileItems = new ObservableCollection<ModFileItemData>();
        
        public ManagerPage()
        {
            InitializeComponent();
            // Enable Drag and Drop
            this.AllowDrop = true;
            this.Drop += ManagerPage_Drop;
            
            this.Loaded += async (s, e) => 
            {
                if (_listMods != null) _listMods.ItemsSource = _modItems;
                if (_listModFiles != null) _listModFiles.ItemsSource = _modTreeNodes;
                await RunAutoDetectAsync();
                ScanMods();
            };
        }

        private async System.Threading.Tasks.Task RunAutoDetectAsync()
        {
            if (_autoDetected) return;
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath()) return;
            
            string cacheDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods");
            if (!System.IO.Directory.Exists(cacheDir)) return;

            if (App.Installer == null)
            {
                App.Installer = new Services.ModInstaller(App.Settings.Current.MhwDirectory);
                App.Installer.OnLog += msg => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log(msg));
            }

            MainWindow.Instance?.Log("🔍 Scanning GameMods folder to auto-detect installed mods...");
            bool changes = await System.Threading.Tasks.Task.Run(() => App.Installer.AutoDetectInstalledMods(cacheDir));
            if (changes)
            {
                MainWindow.Instance?.Log("✅ Auto-detected and registered manually installed mods.");
            }
            _autoDetected = true;
        }

        private void ManagerPage_Drop(object sender, DragEventArgs e)
        {
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath())
            {
                MainWindow.Instance?.Log("⚠️ Game path required before importing mods.");
                return;
            }

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                int imported = 0;
                string cacheDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods");
                System.IO.Directory.CreateDirectory(cacheDir);

                foreach (var file in files)
                {
                    string ext = System.IO.Path.GetExtension(file).ToLower();
                    if (ext == ".zip" || ext == ".7z" || ext == ".rar")
                    {
                        try
                        {
                            string dest = System.IO.Path.Combine(cacheDir, System.IO.Path.GetFileName(file));
                            System.IO.File.Copy(file, dest, true);
                            imported++;
                        }
                        catch (Exception ex)
                        {
                            MainWindow.Instance?.Log($"❌ Failed to import {System.IO.Path.GetFileName(file)}: {ex.Message}");
                        }
                    }
                }
                
                if (imported > 0)
                {
                    MainWindow.Instance?.Log($"✅ Successfully imported {imported} mod(s).");
                    BtnRefresh_Click(this, new RoutedEventArgs());
                }
            }
        }

        private void BtnImport_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath())
            {
                MainWindow.Instance?.Log("⚠️ Game path required before importing mods.");
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Multiselect = true,
                Filter = "Archive Files (*.zip;*.7z;*.rar)|*.zip;*.7z;*.rar"
            };
            
            if (dialog.ShowDialog() == true)
            {
                int imported = 0;
                string cacheDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods");
                System.IO.Directory.CreateDirectory(cacheDir);
                
                foreach (var file in dialog.FileNames)
                {
                    try
                    {
                        string dest = System.IO.Path.Combine(cacheDir, System.IO.Path.GetFileName(file));
                        System.IO.File.Copy(file, dest, true);
                        imported++;
                    }
                    catch (Exception ex)
                    {
                        MainWindow.Instance?.Log($"❌ Failed to import {System.IO.Path.GetFileName(file)}: {ex.Message}");
                    }
                }
                
                if (imported > 0)
                {
                    MainWindow.Instance?.Log($"✅ Successfully imported {imported} mod(s).");
                    BtnRefresh_Click(this, new RoutedEventArgs());
                }
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            ScanMods();
        }

        private ListBox _listMods => (ListBox)this.FindName("ListMods")!;
        private TextBlock _lblModInfo => (TextBlock)this.FindName("LblModInfo")!;
        private Wpf.Ui.Controls.Button _btnInstall => (Wpf.Ui.Controls.Button)this.FindName("BtnInstall")!;
        private Wpf.Ui.Controls.Button _btnUninstall => (Wpf.Ui.Controls.Button)this.FindName("BtnUninstall")!;
        private Wpf.Ui.Controls.Button _btnDelete => (Wpf.Ui.Controls.Button)this.FindName("BtnDelete")!;

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private void ComboSort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ApplyFilterAndSort();
        }

        private void ApplyFilterAndSort()
        {
            if (_allModItems == null || _modItems == null) return;

            var txtSearch = this.FindName("TxtSearch") as Wpf.Ui.Controls.TextBox;
            string query = txtSearch?.Text?.Trim() ?? string.Empty;

            var filtered = string.IsNullOrEmpty(query)
                ? _allModItems.ToList()
                : _allModItems.Where(m => m.Filename.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                                          m.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

            var combo = this.FindName("ComboSort") as ComboBox;
            int sortIdx = combo?.SelectedIndex ?? 0;

            if (sortIdx == 0) // Name
            {
                filtered = filtered.OrderBy(m => m.DisplayName).ToList();
            }
            else if (sortIdx == 1) // Date Modified
            {
                filtered = filtered.OrderByDescending(m => m.DateNum).ToList();
            }
            else if (sortIdx == 2) // Size
            {
                filtered = filtered.OrderByDescending(m => m.SizeNum).ToList();
            }

            string? currentSelection = (_listMods?.SelectedItem as ModItemData)?.Filename;

            _modItems.Clear();
            foreach (var item in filtered)
            {
                _modItems.Add(item);
            }

            if (currentSelection != null && _listMods != null)
            {
                var reselect = _modItems.FirstOrDefault(m => m.Filename == currentSelection);
                if (reselect != null)
                {
                    _listMods.SelectedItem = reselect;
                }
            }
        }

        private void CollectCheckedFiles(Models.ModTreeNode node, System.Collections.Generic.List<string> checkedFiles)
        {
            if (node.IsDirectory)
            {
                foreach (var child in node.Children)
                {
                    CollectCheckedFiles(child, checkedFiles);
                }
            }
            else
            {
                if (node.IsChecked == true && !string.IsNullOrEmpty(node.EntryKey))
                {
                    checkedFiles.Add(node.EntryKey);
                }
            }
        }

        private void ScanMods()
        {
            if (_listMods == null) return;
            _listMods.SelectionChanged -= ListMods_SelectionChanged;

            string? previousSelectedFilename = (_listMods.SelectedItem as ModItemData)?.Filename;

            _allModItems.Clear();
            _modItems.Clear();

            string mhwDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(mhwDir) || !System.IO.Directory.Exists(mhwDir) || !System.IO.File.Exists(System.IO.Path.Combine(mhwDir, "MonsterHunterWorld.exe")))
            {
                _listMods.SelectionChanged += ListMods_SelectionChanged;
                if (MainWindow.Instance != null) MainWindow.Instance.ValidateGamePath();
                return;
            }

            string cacheDir = System.IO.Path.Combine(mhwDir, "GameMods");
            System.IO.Directory.CreateDirectory(cacheDir);
            
            if (App.Installer == null)
            {
                App.Installer = new Services.ModInstaller(mhwDir);
                App.Installer.OnLog += msg => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log(msg));
                App.Installer.OnInstallProgress += pct => Application.Current.Dispatcher.Invoke(() => 
                {
                    MainWindow.Instance?.UpdateInstallProgress((int)pct);
                });
            }

            // Build set of ALL installed files (from OTHER mods) to detect conflicts
            var installedFiles = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var paths in App.Installer.State.InstalledMods.Values)
            {
                foreach (var p in paths) installedFiles.Add(p);
            }

            foreach (var file in System.IO.Directory.GetFiles(cacheDir))
            {
                try
                {
                    string ext = System.IO.Path.GetExtension(file).ToLower();
                    if (ext == ".zip" || ext == ".7z" || ext == ".rar")
                    {
                        string filename = System.IO.Path.GetFileName(file);
                        var isInstalled = App.Installer.IsModInstalled(filename);
                        
                        bool hasConflict = false;
                        if (!isInstalled)
                        {
                            try
                            {
                                var archiveFiles = GetCachedArchiveContents(file);
                                foreach (var rawEntry in archiveFiles)
                                {
                                    string entry = rawEntry.Replace('\\', '/');
                                    int idx = entry.IndexOf("nativePC/", StringComparison.OrdinalIgnoreCase);
                                    if (idx >= 0)
                                    {
                                        string relPath = entry.Substring(idx + "nativePC/".Length).Trim('/');
                                        if (!string.IsNullOrEmpty(relPath) && installedFiles.Contains(relPath))
                                        {
                                            hasConflict = true;
                                            break;
                                        }
                                    }
                                }
                            }
                            catch { }
                        }

                        var fileInfo = new System.IO.FileInfo(file);
                        
                        var item = new ModItemData
                        {
                            Filename = filename,
                            DisplayName = filename + (isInstalled ? " [Installed]" : ""),
                            DateModified = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                            DateNum = fileInfo.LastWriteTime,
                            Size = (fileInfo.Length / 1024.0 / 1024.0).ToString("0.00") + " MB",
                            SizeNum = fileInfo.Length,
                            IsChecked = false
                        };

                        if (isInstalled)
                            item.BackgroundColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 39, 174, 96)); // Green (Installed)
                        else if (hasConflict)
                            item.BackgroundColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 231, 76, 60)); // Red (Conflict)
                        else
                            item.BackgroundColor = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 149, 165, 166)); // Gray (Not Installed)

                        _allModItems.Add(item);
                    }
                }
                catch (Exception ex)
                {
                    MainWindow.Instance?.Log($"⚠️ Could not read mod file {System.IO.Path.GetFileName(file)}: {ex.Message}");
                }
            }
            
            if (_lblModInfo != null) _lblModInfo.Text = Models.I18N.GetString("info_default", App.Settings.Current.Language);
            if (_btnInstall != null) _btnInstall.IsEnabled = false;
            if (_btnUninstall != null) _btnUninstall.IsEnabled = false;
            if (_btnDelete != null) _btnDelete.IsEnabled = false;
            
            ApplyFilterAndSort();

            // Restore selection without jumping
            if (previousSelectedFilename != null && _listMods != null)
            {
                var match = _modItems.FirstOrDefault(m => m.Filename == previousSelectedFilename);
                if (match != null) _listMods.SelectedItem = match;
            }

            if (_listMods != null)
            {
                _listMods.SelectionChanged += ListMods_SelectionChanged;

                if (_listMods.SelectedItem != null)
                {
                    ListMods_SelectionChanged(_listMods, null!);
                }
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath()) return;
            string cacheDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods");
            System.IO.Directory.CreateDirectory(cacheDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() 
            { 
                FileName = cacheDir, 
                UseShellExecute = true, 
                Verb = "open" 
            });
        }

        private void BtnBackup_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath()) return;
            string nativePc = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "nativePC");
            if (!System.IO.Directory.Exists(nativePc))
            {
                MainWindow.Instance?.Log("⚠️ nativePC folder not found. Install mods first.");
                return;
            }
            
            string backupDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, $"nativePC_backup_{DateTime.Now:yyyyMMdd_HHmmss}");
            MainWindow.Instance?.Log($"⏳ Backing up nativePC to {System.IO.Path.GetFileName(backupDir)}...");
            
            System.Threading.Tasks.Task.Run(() => 
            {
                try
                {
                    CopyDirectory(nativePc, backupDir);
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"✅ Backup complete: {System.IO.Path.GetFileName(backupDir)}"));
                }
                catch (Exception ex)
                {
                    Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"❌ Backup failed: {ex.Message}"));
                }
            });
        }
        private void BtnValidate_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null && !MainWindow.Instance.ValidateGamePath()) return;
            if (App.Installer == null) return;
            string nativePc = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "nativePC");
            if (!System.IO.Directory.Exists(nativePc))
            {
                MainWindow.Instance?.Log("⚠️ Validate failed: nativePC directory not found.");
                return;
            }

            bool allGood = true;
            var corruptedMods = new System.Collections.Generic.List<string>();

            foreach (var kvp in App.Installer.State.InstalledMods)
            {
                string modName = kvp.Key;
                var files = kvp.Value;
                var missingFiles = new System.Collections.Generic.List<string>();

                foreach (var relPath in files)
                {
                    string target = System.IO.Path.Combine(nativePc, relPath.Replace('/', System.IO.Path.DirectorySeparatorChar));
                    if (!System.IO.File.Exists(target))
                    {
                        missingFiles.Add(relPath);
                    }
                }

                if (missingFiles.Count > 0)
                {
                    allGood = false;
                    corruptedMods.Add(modName);
                    MainWindow.Instance?.Log($"❌ Validation failed for {modName}: {missingFiles.Count} files missing! Marking as Uninstalled.");
                }
            }

            if (corruptedMods.Count > 0)
            {
                foreach (var mod in corruptedMods)
                {
                    App.Installer.UninstallMod(mod); // This will remove from state and cleanup remaining files
                }
                ScanMods();
            }

            if (allGood)
            {
                MainWindow.Instance?.Log("🌟 All installed mods are perfectly intact.");
            }
        }
        
        private static void CopyDirectory(string sourceDir, string destinationDir)
        {
            System.IO.Directory.CreateDirectory(destinationDir);
            foreach (var file in System.IO.Directory.GetFiles(sourceDir))
            {
                string target = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(file));
                System.IO.File.Copy(file, target, true);
            }
            foreach (var directory in System.IO.Directory.GetDirectories(sourceDir))
            {
                string target = System.IO.Path.Combine(destinationDir, System.IO.Path.GetFileName(directory));
                CopyDirectory(directory, target);
            }
        }
        private async void ListMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_listMods.SelectedItem is ModItemData item)
            {
                string filename = item.Filename;
                var isInstalled = App.Installer?.IsModInstalled(filename) ?? false;
                _btnInstall.IsEnabled = !isInstalled;
                _btnUninstall.IsEnabled = isInstalled;
                _btnDelete.IsEnabled = true;

                if (filename == _currentLoadedFilename && _modTreeNodes.Count > 0)
                {
                    return;
                }

                string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", filename);
                if (System.IO.File.Exists(fullPath))
                {
                    _currentLoadedFilename = filename;
                    _lblModInfo.Text = $"Loading {filename}... Please wait.";
                    
                    var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
                    if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
                    
                    try
                    {
                        var (optionCount, totalFiles, rootNodes) = await System.Threading.Tasks.Task.Run(() =>
                        {
                            var contents = GetCachedArchiveContents(fullPath);
                            
                            if (contents.Count == 1 && contents[0].StartsWith("[Error"))
                            {
                                throw new Exception(contents[0]);
                            }

                            var optionGroups = new System.Collections.Generic.Dictionary<string, (string Prefix, System.Collections.Generic.List<string> Files)>(StringComparer.OrdinalIgnoreCase);

                            foreach (var rawEntry in contents)
                            {
                                string entry = rawEntry.Replace('\\', '/').Trim();
                                if (string.IsNullOrEmpty(entry) || entry.EndsWith("/")) continue;
                                if (entry.StartsWith("__MACOSX/", StringComparison.OrdinalIgnoreCase) || entry.EndsWith(".DS_Store", StringComparison.OrdinalIgnoreCase)) continue;

                                // Filter out common images, text files, and fomod installers
                                string ext = System.IO.Path.GetExtension(entry).ToLowerInvariant();
                                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".bmp" || ext == ".txt" || ext == ".md" || ext == ".url") continue;
                                if (entry.StartsWith("fomod/", StringComparison.OrdinalIgnoreCase)) continue;

                                string[] parts = entry.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length == 0) continue;
                                if (parts.Length == 1 && (parts[0].Equals("nativePC", StringComparison.OrdinalIgnoreCase) || IsMhwFolder(parts[0]))) continue; // Skip directory-only entries

                                string prefix = "";
                                string optionName = "";
                                
                                int mhwFolderIndex = -1;
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (parts[i].Equals("nativePC", StringComparison.OrdinalIgnoreCase) || IsMhwFolder(parts[i]))
                                    {
                                        mhwFolderIndex = i;
                                        break;
                                    }
                                }

                                if (mhwFolderIndex < 0) continue; // Skip files not belonging to MHW structure

                                if (mhwFolderIndex > 0)
                                {
                                    prefix = string.Join("/", parts.Take(mhwFolderIndex));
                                    optionName = prefix;
                                }
                                else
                                {
                                    prefix = "";
                                    optionName = "nativePC (Default)";
                                }

                                if (!optionGroups.TryGetValue(optionName, out var group))
                                {
                                    group = (prefix, new System.Collections.Generic.List<string>());
                                    optionGroups[optionName] = group;
                                }
                                group.Files.Add(entry);
                            }

                            int totFiles = optionGroups.Values.Sum(g => g.Files.Count);
                            var roots = new ObservableCollection<Models.ModTreeNode>();
                            int optIndex = 0;

                            if (optionGroups.Count <= 1)
                            {
                                var singleGroup = optionGroups.FirstOrDefault();
                                string optName = singleGroup.Key ?? "nativePC (Default)";
                                string optPrefix = singleGroup.Value.Prefix ?? "";
                                var fileList = singleGroup.Value.Files ?? new System.Collections.Generic.List<string>();

                                var rootNode = BuildOptionTree(optName, optPrefix, fileList, true);
                                roots.Add(rootNode);
                            }
                            else
                            {
                                foreach (var kvp in optionGroups)
                                {
                                    string optName = kvp.Key;
                                    string optPrefix = kvp.Value.Prefix;
                                    var fileList = kvp.Value.Files;
                                    bool isDefaultSelected = (optIndex == 0);

                                    var optNode = BuildOptionTree(optName, optPrefix, fileList, isDefaultSelected);
                                    roots.Add(optNode);
                                    optIndex++;
                                }
                            }

                            return (optionGroups.Count, totFiles, roots);
                        });
                        
                        if (_listMods.SelectedItem != item)
                        {
                            return;
                        }

                        _lblModInfo.Text = $"{filename}\nNativePC Options: {optionCount} | Files: {totalFiles}";

                        _modTreeNodes.Clear();
                        foreach (var node in rootNodes)
                        {
                            _modTreeNodes.Add(node);
                        }

                        // Wait for WPF layout and rendering pass to finish rendering nodes on screen
                        await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Loaded);
                    }
                    catch (Exception ex)
                    {
                        _lblModInfo.Text = $"❌ Failed to read archive {filename}: {ex.Message}";
                        _modTreeNodes.Clear();
                    }
                    finally
                    {
                        if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
                    }
                }
            }
        }
        
        private void SetAllChecked(ObservableCollection<Models.ModTreeNode> nodes, bool isChecked)
        {
            if (nodes == null) return;
            foreach (var node in nodes)
            {
                node.IsChecked = isChecked;
            }
        }

        private void BtnCheckAllTree_Click(object sender, RoutedEventArgs e)
        {
            SetAllChecked(_modTreeNodes, true);
        }

        private void BtnUncheckAllTree_Click(object sender, RoutedEventArgs e)
        {
            SetAllChecked(_modTreeNodes, false);
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_listMods.SelectedItem is ModItemData item)
            {
                string filename = item.Filename;
                string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", filename);
                
                var selectedFiles = new System.Collections.Generic.List<string>();
                if (_modTreeNodes != null)
                {
                    foreach (var node in _modTreeNodes)
                    {
                        CollectCheckedFiles(node, selectedFiles);
                    }
                }

                _btnInstall.IsEnabled = false;
                _btnUninstall.IsEnabled = false;
                var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
                if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
                if (_lblModInfo != null) _lblModInfo.Text = $"Installing {filename}... Please wait.";

                await System.Threading.Tasks.Task.Run(() => 
                {
                    App.Installer?.InstallMod(fullPath, filename, selectedFiles.Count > 0 ? selectedFiles : null);
                });

                if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
                
                var pb = MainWindow.Instance?.FindName("PbInstall") as System.Windows.Controls.ProgressBar;
                if (pb != null) pb.Value = 0;
                
                ScanMods();
                
                // Re-select the item to refresh UI state
                var currentItem = _modItems.FirstOrDefault(m => m.Filename == filename);
                if (currentItem != null) _listMods.SelectedItem = currentItem;
            }
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            if (_listMods.SelectedItem is ModItemData item)
            {
                string filename = item.Filename;
                
                _btnInstall.IsEnabled = false;
                _btnUninstall.IsEnabled = false;
                var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
                if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
                if (_lblModInfo != null) _lblModInfo.Text = $"Uninstalling {filename}... Please wait.";

                await System.Threading.Tasks.Task.Run(() => 
                {
                    App.Installer?.UninstallMod(filename);
                });

                if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
                ScanMods();
                
                var currentItem = _modItems.FirstOrDefault(m => m.Filename == filename);
                if (currentItem != null) _listMods.SelectedItem = currentItem;
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (_listMods.SelectedItem is ModItemData item)
            {
                string filename = item.Filename;
                
                _btnInstall.IsEnabled = false;
                _btnUninstall.IsEnabled = false;
                _btnDelete.IsEnabled = false;
                var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
                if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
                if (_lblModInfo != null) _lblModInfo.Text = $"Deleting {filename}... Please wait.";

                await System.Threading.Tasks.Task.Run(async () => 
                {
                    App.Installer?.UninstallMod(filename); // Ensure it's uninstalled first
                    
                    string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", filename);
                    string recycleDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", ".recycle_mods");
                    System.IO.Directory.CreateDirectory(recycleDir);
                    
                    if (System.IO.File.Exists(fullPath))
                    {
                        try
                        {
                            System.IO.File.Move(fullPath, System.IO.Path.Combine(recycleDir, filename), true);
                            Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"🗑️ Mod moved to recycle bin: {filename}"));
                            
                            if (App.Server != null && App.Server.IsRunning)
                            {
                                App.Server.DeletedMods.TryAdd(filename, App.Server.HostUsername);
                            }
                            else if (App.Client != null)
                            {
                                await App.Client.DeleteModAsync(filename);
                            }
                        }
                        catch {}
                    }
                });

                if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
                ScanMods();
            }
        }

        private void BtnCheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _modItems)
            {
                item.IsChecked = true;
            }
        }

        private void BtnUncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _modItems)
            {
                item.IsChecked = false;
            }
        }

        private async void BtnInstallChecked_Click(object sender, RoutedEventArgs e)
        {
            if (App.Installer == null) return;
            var checkedItems = _modItems.Where(i => i.IsChecked).ToList();
            if (checkedItems.Count == 0)
            {
                MainWindow.Instance?.Log("⚠️ No mods checked to install. Please check the boxes next to the mods in the library first.");
                return;
            }

            var toInstall = checkedItems.Where(i => !App.Installer.IsModInstalled(i.Filename)).ToList();
            if (toInstall.Count == 0)
            {
                MainWindow.Instance?.Log("⚠️ All checked mods are already installed.");
                return;
            }
            
            var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
            if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
            if (_lblModInfo != null) _lblModInfo.Text = $"Installing {toInstall.Count} mod(s)... Please wait.";

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var item in toInstall)
                {
                    string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", item.Filename);
                    App.Installer.InstallMod(fullPath, item.Filename);
                }
            });

            if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
            var pb = MainWindow.Instance?.FindName("PbInstall") as System.Windows.Controls.ProgressBar;
            if (pb != null) pb.Value = 0;
            ScanMods();
        }

        private async void BtnUninstallChecked_Click(object sender, RoutedEventArgs e)
        {
            if (App.Installer == null) return;
            var checkedItems = _modItems.Where(i => i.IsChecked).ToList();
            if (checkedItems.Count == 0)
            {
                MainWindow.Instance?.Log("⚠️ No mods checked to uninstall. Please check the boxes next to the mods in the library first.");
                return;
            }

            var toUninstall = checkedItems.Where(i => App.Installer.IsModInstalled(i.Filename)).ToList();
            if (toUninstall.Count == 0)
            {
                MainWindow.Instance?.Log("⚠️ None of the checked mods are currently installed.");
                return;
            }
            
            var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
            if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
            if (_lblModInfo != null) _lblModInfo.Text = $"Uninstalling {toUninstall.Count} mod(s)... Please wait.";

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var item in toUninstall)
                {
                    App.Installer.UninstallMod(item.Filename);
                }
            });

            if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
            ScanMods();
        }

        private async void BtnDeleteChecked_Click(object sender, RoutedEventArgs e)
        {
            if (App.Installer == null) return;
            var toDelete = _modItems.Where(i => i.IsChecked).ToList();
            if (toDelete.Count == 0)
            {
                MainWindow.Instance?.Log("⚠️ No mods checked to delete. Please check the boxes next to the mods in the library first.");
                return;
            }
            
            string recycleDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", ".recycle_mods");
            System.IO.Directory.CreateDirectory(recycleDir);
            
            var loadingRing = this.FindName("LoadingRing") as Wpf.Ui.Controls.ProgressRing;
            if (loadingRing != null) loadingRing.Visibility = Visibility.Visible;
            if (_lblModInfo != null) _lblModInfo.Text = $"Deleting {toDelete.Count} mod(s)... Please wait.";

            await System.Threading.Tasks.Task.Run(async () =>
            {
                foreach (var item in toDelete)
                {
                    string filename = item.Filename;
                    App.Installer.UninstallMod(filename);
                    string fullPath = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", filename);
                    if (System.IO.File.Exists(fullPath))
                    {
                        try
                        {
                            System.IO.File.Move(fullPath, System.IO.Path.Combine(recycleDir, filename), true);
                            Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"🗑️ Mod moved to recycle bin: {filename}"));
                            
                            if (App.Server != null && App.Server.IsRunning)
                            {
                                App.Server.DeletedMods.TryAdd(filename, App.Server.HostUsername);
                            }
                            else if (App.Client != null)
                            {
                                await App.Client.DeleteModAsync(filename);
                            }
                        }
                        catch {}
                    }
                }
            });

            if (loadingRing != null) loadingRing.Visibility = Visibility.Collapsed;
            ScanMods();
        }

        private static Models.ModTreeNode BuildOptionTree(string optionName, string optionPrefix, System.Collections.Generic.List<string> fileList, bool isDefaultSelected)
        {
            var rootNode = new Models.ModTreeNode
            {
                Name = $"{optionName} ({fileList.Count} files)",
                EntryKey = optionPrefix,
                IsDirectory = true,
                IsChecked = isDefaultSelected,
                IsExpanded = true
            };

            foreach (var fullPath in fileList)
            {
                string relPath = fullPath;
                if (!string.IsNullOrEmpty(optionPrefix) && fullPath.StartsWith(optionPrefix + "/", StringComparison.OrdinalIgnoreCase))
                {
                    relPath = fullPath.Substring(optionPrefix.Length + 1);
                }

                string[] parts = relPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                ObservableCollection<Models.ModTreeNode> currentLevel = rootNode.Children;
                Models.ModTreeNode parent = rootNode;

                for (int i = 0; i < parts.Length; i++)
                {
                    bool isLast = (i == parts.Length - 1);
                    string part = parts[i];

                    var node = currentLevel.FirstOrDefault(n => n.Name.Equals(part, StringComparison.OrdinalIgnoreCase) && n.IsDirectory == !isLast);
                    if (node == null)
                    {
                        string subPathKey = isLast ? fullPath : (string.IsNullOrEmpty(optionPrefix) ? string.Join('/', parts.Take(i + 1)) : optionPrefix + "/" + string.Join('/', parts.Take(i + 1)));

                        node = new Models.ModTreeNode
                        {
                            Name = part,
                            EntryKey = subPathKey,
                            IsDirectory = !isLast,
                            IsChecked = isDefaultSelected,
                            IsExpanded = i < 2,
                            Parent = parent
                        };
                        currentLevel.Add(node);
                    }

                    if (!isLast)
                    {
                        parent = node;
                        currentLevel = node.Children;
                    }
                }
            }

            return rootNode;
        }

        private static bool IsMhwFolder(string folderName)
        {
            string[] mhwFolders = { "sound", "wp", "vfx", "stage", "art", "ui", "pl", "hm", "em", "facility", "gimmick", "collision", "shader", "ot", "item", "bg", "quest", "ev", "common" };
            return mhwFolders.Any(f => f.Equals(folderName, StringComparison.OrdinalIgnoreCase));
        }
    }
}
