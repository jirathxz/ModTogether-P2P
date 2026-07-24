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

namespace ModTogetherUniversal
{
    public partial class ModExplorerPage : Page, INotifyPropertyChanged
    {
        private ObservableCollection<ModItemData> _modItems = new ObservableCollection<ModItemData>();
        private string ModsDirectory => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "GameMods");

        public event PropertyChangedEventHandler? PropertyChanged;

        private string _colInstallText = "Install";
        public string ColInstallText { get => _colInstallText; set { _colInstallText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColInstallText))); } }
        
        private string _colFilenameText = "Filename";
        public string ColFilenameText { get => _colFilenameText; set { _colFilenameText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColFilenameText))); } }

        private string _colSizeText = "Size";
        public string ColSizeText { get => _colSizeText; set { _colSizeText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColSizeText))); } }

        private string _colModifiedText = "Modified";
        public string ColModifiedText { get => _colModifiedText; set { _colModifiedText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ColModifiedText))); } }

        // Returns the target directory from Settings (ModDirectory)
        private string TargetDirectory => App.Settings.Current.ModDirectory;

        public ModExplorerPage()
        {
            InitializeComponent();
            DataContext = this;
            ListMods.ItemsSource = _modItems;

            Loaded += (s, e) =>
            {
                ApplyTranslations();
                if (!Directory.Exists(ModsDirectory))
                    Directory.CreateDirectory(ModsDirectory);
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

            ColInstallText = I18N.GetString("explorer_col_install", lang);
            ColFilenameText = I18N.GetString("explorer_col_filename", lang);
            ColSizeText = I18N.GetString("explorer_col_size", lang);
            ColModifiedText = I18N.GetString("explorer_col_modified", lang);
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
                        "Mod Folder Path is not configured. Please set it in Settings first.",
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
                    MessageBox.Show($"Failed to uninstall mod: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    chk.IsChecked = true;
                }
            }
        }
    }
}
