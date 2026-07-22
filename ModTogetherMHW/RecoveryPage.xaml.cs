using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ModTogetherMHW
{
    public partial class RecoveryPage : Page
    {
        private ObservableCollection<ModItemData> _modItems = new();
        private System.Windows.Threading.DispatcherTimer _searchTimer;

        public RecoveryPage()
        {
            InitializeComponent();
            ListMods.ItemsSource = _modItems;

            _searchTimer = new System.Windows.Threading.DispatcherTimer();
            _searchTimer.Interval = TimeSpan.FromMilliseconds(300);
            _searchTimer.Tick += (s, e) =>
            {
                _searchTimer.Stop();
                FilterMods();
            };

            Loaded += (s, e) => 
            {
                ApplyTranslations();
                ScanRecycleBin();
            };
            App.Settings.OnSettingsChanged += ApplyTranslations;
        }

        private void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            if (LblTitle != null) LblTitle.Text = Models.I18N.GetString("recovery_title", lang);
            if (TxtSearch != null) TxtSearch.PlaceholderText = Models.I18N.GetString("search_placeholder", lang);
            if (BtnCheckAll != null) BtnCheckAll.Content = Models.I18N.GetString("btn_check_all", lang);
            if (BtnUncheckAll != null) BtnUncheckAll.Content = Models.I18N.GetString("btn_uncheck_all", lang);
            
            if (BtnRestoreChecked != null) BtnRestoreChecked.Content = Models.I18N.GetString("btn_restore_all", lang);
            if (BtnDeleteChecked != null) BtnDeleteChecked.Content = Models.I18N.GetString("btn_delete_all_permanently", lang);
            
            if (BtnRestore != null) BtnRestore.Content = Models.I18N.GetString("btn_restore", lang);
            if (BtnDelete != null) BtnDelete.Content = Models.I18N.GetString("btn_delete_permanently", lang);
        }

        private void ScanRecycleBin()
        {
            _modItems.Clear();
            string recycleDir = Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", ".recycle_mods");

            if (Directory.Exists(recycleDir))
            {
                var files = Directory.GetFiles(recycleDir, "*.*")
                    .Where(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || 
                                f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var file in files)
                {
                    var info = new FileInfo(file);
                    _modItems.Add(new ModItemData
                    {
                        Filename = info.Name,
                        DisplayName = info.Name,
                        DateModified = info.LastWriteTime.ToString("yyyy-MM-dd HH:mm"),
                        Size = FormatSize(info.Length)
                    });
                }
            }
            FilterMods();
        }

        private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchTimer.Stop();
            _searchTimer.Start();
        }

        private void FilterMods()
        {
            if (TxtSearch == null) return;
            string filter = TxtSearch.Text.Trim().ToLowerInvariant();
            
            var view = System.Windows.Data.CollectionViewSource.GetDefaultView(_modItems);
            if (view != null)
            {
                view.Filter = item =>
                {
                    if (string.IsNullOrEmpty(filter)) return true;
                    if (item is ModItemData m)
                    {
                        return m.Filename.ToLowerInvariant().Contains(filter);
                    }
                    return false;
                };
            }
        }

        private void ListMods_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var hasSelection = ListMods.SelectedItem != null;
            BtnRestore.IsEnabled = hasSelection;
            BtnDelete.IsEnabled = hasSelection;
        }

        private void BtnCheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _modItems) item.IsChecked = true;
        }

        private void BtnUncheckAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in _modItems) item.IsChecked = false;
        }

        private async void RestoreMods(System.Collections.Generic.List<ModItemData> mods)
        {
            if (mods.Count == 0) return;
            
            string mhwDir = App.Settings.Current.MhwDirectory;
            string modsDir = Path.Combine(mhwDir, "GameMods");
            string recycleDir = Path.Combine(modsDir, ".recycle_mods");
            Directory.CreateDirectory(modsDir);

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var mod in mods)
                {
                    string recyclePath = Path.Combine(recycleDir, mod.Filename);
                    string targetPath = Path.Combine(modsDir, mod.Filename);

                    if (File.Exists(recyclePath))
                    {
                        try
                        {
                            File.Move(recyclePath, targetPath, true);
                            Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"♻️ Restored mod: {mod.Filename}"));
                            
                            // Remove from Server's deleted list if hosting so it gets synced again
                            if (App.Server != null && App.Server.IsRunning)
                            {
                                App.Server.DeletedMods.TryRemove(mod.Filename, out _);
                                App.Server.TriggerCacheRefresh();
                            }
                            // Note: Clients don't have an explicit 'restore' API yet, but next sync they will upload it back to Host since it's missing on host
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"❌ Failed to restore {mod.Filename}: {ex.Message}"));
                        }
                    }
                }
            });

            ScanRecycleBin();
        }

        private async void DeleteModsPermanently(System.Collections.Generic.List<ModItemData> mods)
        {
            if (mods.Count == 0) return;

            string recycleDir = Path.Combine(App.Settings.Current.MhwDirectory, "GameMods", ".recycle_mods");

            await System.Threading.Tasks.Task.Run(() =>
            {
                foreach (var mod in mods)
                {
                    string recyclePath = Path.Combine(recycleDir, mod.Filename);
                    if (File.Exists(recyclePath))
                    {
                        try
                        {
                            File.Delete(recyclePath);
                            Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"🗑️ Mod permanently deleted from recycle bin: {mod.Filename}"));
                        }
                        catch (Exception ex)
                        {
                            Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log($"❌ Failed to delete {mod.Filename}: {ex.Message}"));
                        }
                    }
                }
            });

            ScanRecycleBin();
        }

        private void BtnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (ListMods.SelectedItem is ModItemData item)
            {
                RestoreMods(new System.Collections.Generic.List<ModItemData> { item });
            }
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (ListMods.SelectedItem is ModItemData item)
            {
                DeleteModsPermanently(new System.Collections.Generic.List<ModItemData> { item });
            }
        }

        private void BtnRestoreChecked_Click(object sender, RoutedEventArgs e)
        {
            var checkedItems = _modItems.Where(i => i.IsChecked).ToList();
            RestoreMods(checkedItems);
        }

        private void BtnDeleteChecked_Click(object sender, RoutedEventArgs e)
        {
            var checkedItems = _modItems.Where(i => i.IsChecked).ToList();
            DeleteModsPermanently(checkedItems);
        }

        private string FormatSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}
