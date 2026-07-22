using System.Windows;
using System.Windows.Controls;

namespace ModTogetherMHW
{
    public partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            InitializeComponent();
            Loaded += (s, e) => 
            {
                ApplyTranslations();
                LoadSettings();
            };
            App.Settings.OnSettingsChanged += ApplyTranslations;
        }

        private bool _isLoaded = false;

        private void LoadSettings()
        {
            _isLoaded = false;
            TxtMhwDir.Text = App.Settings.Current.MhwDirectory;
            ToggleAutoEnable.IsChecked = App.Settings.Current.AutoEnableMods;
            
            string currentLang = App.Settings.Current.Language;
            foreach (ComboBoxItem item in ComboLanguage.Items)
            {
                if (item.Tag?.ToString() == currentLang)
                {
                    ComboLanguage.SelectedItem = item;
                    break;
                }
            }

            string currentTheme = App.Settings.Current.Theme;
            foreach (ComboBoxItem item in ComboTheme.Items)
            {
                if (item.Tag?.ToString() == currentTheme)
                {
                    ComboTheme.SelectedItem = item;
                    break;
                }
            }
            _isLoaded = true;
        }

        private void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            if (LblSettingsTitle != null) LblSettingsTitle.Text = Models.I18N.GetString("tab_settings", lang);
            
            if (LblGameDir != null) LblGameDir.Text = Models.I18N.GetString("game_dir", lang);
            if (DescGameDir != null) DescGameDir.Text = Models.I18N.GetString("desc_game_dir", lang);
            if (TxtMhwDir != null) TxtMhwDir.PlaceholderText = Models.I18N.GetString("placeholder_dir", lang);
            if (BtnSelectFolder != null) BtnSelectFolder.Content = Models.I18N.GetString("btn_select_folder", lang);
            if (BtnResetPath != null) BtnResetPath.Content = Models.I18N.GetString("btn_reset_path", lang);
            
            if (LblAutoEnable != null) LblAutoEnable.Text = Models.I18N.GetString("auto_enable", lang);
            if (DescAutoEnable != null) DescAutoEnable.Text = Models.I18N.GetString("desc_auto_enable", lang);
            
            if (LblTheme != null) LblTheme.Text = Models.I18N.GetString("lbl_theme", lang);
            if (DescTheme != null) DescTheme.Text = Models.I18N.GetString("desc_theme", lang);
            if (OptThemeLight != null) OptThemeLight.Content = Models.I18N.GetString("theme_light", lang);
            if (OptThemeDark != null) OptThemeDark.Content = Models.I18N.GetString("theme_dark", lang);
            if (OptThemeSystem != null) OptThemeSystem.Content = Models.I18N.GetString("theme_system", lang);
            
            if (LblLanguage != null) LblLanguage.Text = Models.I18N.GetString("lbl_language", lang);
            if (DescLanguage != null) DescLanguage.Text = Models.I18N.GetString("desc_language", lang);
            
            if (LblAppUpdate != null) LblAppUpdate.Text = Models.I18N.GetString("lbl_app_update", lang);
            if (DescUpdate != null) DescUpdate.Text = Models.I18N.GetString("desc_update", lang);
            if (BtnCheckUpdate != null) BtnCheckUpdate.Content = Models.I18N.GetString("btn_check_update", lang);
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                string exePath = System.IO.Path.Combine(dialog.FolderName, "MonsterHunterWorld.exe");
                if (!System.IO.File.Exists(exePath))
                {
                    MessageBox.Show(
                        Models.I18N.GetString("err_invalid_dir_reset", App.Settings.Current.Language),
                        Models.I18N.GetString("title_path_error", App.Settings.Current.Language),
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);

                    App.Settings.Current.MhwDirectory = string.Empty;
                    App.Settings.Save();
                    TxtMhwDir.Text = string.Empty;
                    App.Installer = null;
                    MainWindow.Instance?.ValidateGamePath();
                    return;
                }

                App.Settings.Current.MhwDirectory = dialog.FolderName;
                App.Settings.Save();
                TxtMhwDir.Text = dialog.FolderName;
                App.Installer = new Services.ModInstaller(dialog.FolderName);
                MainWindow.Instance?.Log($"✅ Game path set to: {dialog.FolderName}");
                MainWindow.Instance?.ValidateGamePath();
            }
        }

        private void BtnResetPath_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.Current.MhwDirectory = string.Empty;
            App.Settings.Save();
            TxtMhwDir.Text = string.Empty;
            App.Installer = null;
            MainWindow.Instance?.Log("🔄 Game path has been reset. Please select a new Monster Hunter World folder.");
            MainWindow.Instance?.ValidateGamePath();
        }

        private void ToggleAutoEnable_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded || ToggleAutoEnable == null) return;
            App.Settings.Current.AutoEnableMods = ToggleAutoEnable.IsChecked == true;
            App.Settings.Save();
        }

        private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboTheme?.SelectedItem is ComboBoxItem item && item.Tag is string theme)
            {
                App.Settings.Current.Theme = theme;
                App.Settings.Save();
                
                // Apply theme immediately
                App.ApplyTheme(theme);
            }
        }

        private void ComboLanguage_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboLanguage?.SelectedItem is ComboBoxItem item && item.Tag is string lang)
            {
                App.Settings.Current.Language = lang;
                App.Settings.Save();
            }
        }

        private async void BtnCheckUpdate_Click(object sender, RoutedEventArgs e)
        {
            App.Updater.OnLog += msg => Application.Current.Dispatcher.Invoke(() => MainWindow.Instance?.Log(msg));
            App.Updater.OnUpdateAvailable += (version, url, filename) => 
            {
                Application.Current.Dispatcher.Invoke(() => 
                {
                    MainWindow.Instance?.Log($"Downloading update {version}...");
                    _ = App.Updater.DownloadAndInstallUpdateAsync(url, filename, progress => 
                    {
                        // Fire and forget, or update some UI progress bar
                    });
                });
            };
            
            await App.Updater.CheckForUpdatesAsync();
        }
    }
}
