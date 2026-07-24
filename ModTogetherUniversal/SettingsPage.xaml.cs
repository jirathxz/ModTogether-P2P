using System.Windows;
using System.Windows.Controls;

namespace ModTogetherUniversal
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
            TxtgameDir.Text = App.Settings.Current.GameDirectory;
            TxtModDir.Text = App.Settings.Current.ModDirectory;

            
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
            if (TxtgameDir != null) TxtgameDir.PlaceholderText = Models.I18N.GetString("placeholder_dir", lang);
            if (BtnSelectFolder != null) BtnSelectFolder.Content = Models.I18N.GetString("btn_select_folder", lang);
            if (BtnResetPath != null) BtnResetPath.Content = Models.I18N.GetString("btn_reset_path", lang);

            if (LblModDir != null) LblModDir.Text = Models.I18N.GetString("lbl_mod_dir", lang);
            if (DescModDir != null) DescModDir.Text = Models.I18N.GetString("desc_mod_dir", lang);
            if (TxtModDir != null) TxtModDir.PlaceholderText = Models.I18N.GetString("placeholder_mod_dir", lang);
            if (BtnSelectModFolder != null) BtnSelectModFolder.Content = Models.I18N.GetString("btn_select_mod_folder", lang);
            if (BtnResetModPath != null) BtnResetModPath.Content = Models.I18N.GetString("btn_reset_mod_path", lang);
            
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
                if (!string.IsNullOrEmpty(dialog.FolderName))
                {
                    App.Settings.Current.GameDirectory = dialog.FolderName;
                    App.Settings.Save();
                    
                    TxtgameDir.Text = dialog.FolderName;
                    MainWindow.Instance?.ReloadAllPlugins();
                }
            }
        }

        private void BtnResetPath_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.Current.GameDirectory = string.Empty;
            App.Settings.Save();
            TxtgameDir.Text = string.Empty;
            MainWindow.Instance?.Log("ðŸ”„ Game path has been reset. Please select a new Game folder.");
            MainWindow.Instance?.ValidateGamePath();
            MainWindow.Instance?.ReloadAllPlugins();
        }

        private void BtnSelectModFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Mod Folder Path (where mods will be installed)"
            };
            if (dialog.ShowDialog() == true)
            {
                if (!string.IsNullOrEmpty(dialog.FolderName))
                {
                    App.Settings.Current.ModDirectory = dialog.FolderName;
                    App.Settings.Save();
                    TxtModDir.Text = dialog.FolderName;
                    MainWindow.Instance?.Log($"ðŸ“‚ Mod Folder Path set to: {dialog.FolderName}");
                }
            }
        }

        private void BtnResetModPath_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.Current.ModDirectory = string.Empty;
            App.Settings.Save();
            TxtModDir.Text = string.Empty;
            MainWindow.Instance?.Log("ðŸ”„ Mod Folder Path has been reset.");
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
            await App.Updater.CheckForUpdatesAsync();
        }
    }
}

