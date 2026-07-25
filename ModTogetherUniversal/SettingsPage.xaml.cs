using System;
using System.IO;
using System.Linq;
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

            if (ToggleDebugLog != null) ToggleDebugLog.IsChecked = App.Settings.Current.EnableDebugLog;
            if (ToggleErrorLog != null) ToggleErrorLog.IsChecked = App.Settings.Current.EnableErrorLog;
            if (TogglePluginSecurity != null) TogglePluginSecurity.IsChecked = App.Settings.Current.StrictPluginSecurity;

            if (TxtDownloadLimit != null) TxtDownloadLimit.Text = App.Settings.Current.MaxDownloadSpeedKbps.ToString();
            if (TxtUploadLimit != null) TxtUploadLimit.Text = App.Settings.Current.MaxUploadSpeedKbps.ToString();

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

            RefreshGameProfilesList();
            _isLoaded = true;
        }

        private void RefreshGameProfilesList()
        {
            if (CmbGameProfiles == null) return;
            CmbGameProfiles.Items.Clear();

            var currentPath = App.Settings.Current.GameDirectory;
            if (!string.IsNullOrWhiteSpace(currentPath) && !App.Settings.Current.GamePathHistory.Contains(currentPath))
            {
                App.Settings.Current.GamePathHistory.Add(currentPath);
                App.Settings.Save();
            }

            foreach (var path in App.Settings.Current.GamePathHistory)
            {
                string folderName = Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                if (string.IsNullOrWhiteSpace(folderName)) folderName = path;
                CmbGameProfiles.Items.Add($"{folderName} ({path})");
            }

            if (!string.IsNullOrWhiteSpace(currentPath))
            {
                var match = CmbGameProfiles.Items.Cast<string>().FirstOrDefault(i => i.Contains(currentPath));
                if (match != null) CmbGameProfiles.SelectedItem = match;
            }
        }

        private void CmbGameProfiles_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded || CmbGameProfiles.SelectedItem is not string selected) return;
            
            foreach (var path in App.Settings.Current.GamePathHistory)
            {
                if (selected.Contains(path))
                {
                    App.Settings.Current.GameDirectory = path;
                    App.Settings.Save();
                    TxtgameDir.Text = path;
                    MainWindow.Instance?.Log($"🎮 Switched Game Profile to: {path}");
                    MainWindow.Instance?.ReloadAllPlugins();
                    break;
                }
            }
        }

        private void BandwidthLimit_Changed(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (int.TryParse(TxtDownloadLimit?.Text, out int dl)) App.Settings.Current.MaxDownloadSpeedKbps = Math.Max(0, dl);
            if (int.TryParse(TxtUploadLimit?.Text, out int ul)) App.Settings.Current.MaxUploadSpeedKbps = Math.Max(0, ul);
            App.Settings.Save();
        }

        private void BtnSpeedUnlimited_Click(object sender, RoutedEventArgs e)
        {
            if (TxtDownloadLimit != null) TxtDownloadLimit.Text = "0";
            if (TxtUploadLimit != null) TxtUploadLimit.Text = "0";
        }

        private void BtnSpeed2MB_Click(object sender, RoutedEventArgs e)
        {
            if (TxtDownloadLimit != null) TxtDownloadLimit.Text = "2048";
            if (TxtUploadLimit != null) TxtUploadLimit.Text = "2048";
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

            if (LblTheme != null) LblTheme.Text = Models.I18N.GetString("lbl_theme", lang);
            if (DescTheme != null) DescTheme.Text = Models.I18N.GetString("desc_theme", lang);
            if (OptThemeLight != null) OptThemeLight.Content = Models.I18N.GetString("theme_light", lang);
            if (OptThemeDark != null) OptThemeDark.Content = Models.I18N.GetString("theme_dark", lang);
            if (OptThemeSystem != null) OptThemeSystem.Content = Models.I18N.GetString("theme_system", lang);
            
            if (LblLanguage != null) LblLanguage.Text = Models.I18N.GetString("lbl_language", lang);
            if (DescLanguage != null) DescLanguage.Text = Models.I18N.GetString("desc_language", lang);

            if (LblDebugLog != null) LblDebugLog.Text = Models.I18N.GetString("lbl_debug_log", lang);
            if (DescDebugLog != null) DescDebugLog.Text = Models.I18N.GetString("desc_debug_log", lang);

            if (LblErrorLog != null) LblErrorLog.Text = Models.I18N.GetString("lbl_error_log", lang);
            if (DescErrorLog != null) DescErrorLog.Text = Models.I18N.GetString("desc_error_log", lang);

            if (LblPluginSecurity != null) LblPluginSecurity.Text = Models.I18N.GetString("lbl_plugin_security", lang);
            if (DescPluginSecurity != null) DescPluginSecurity.Text = Models.I18N.GetString("desc_plugin_security", lang);
            
            if (LblAppUpdate != null) LblAppUpdate.Text = Models.I18N.GetString("lbl_app_update", lang);
            if (DescUpdate != null) DescUpdate.Text = Models.I18N.GetString("desc_update", lang);
            if (BtnCheckUpdate != null) BtnCheckUpdate.Content = Models.I18N.GetString("btn_check_update", lang);
        }

        private void ToggleDebugLog_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            App.Settings.Current.EnableDebugLog = ToggleDebugLog.IsChecked ?? true;
            App.Settings.Save();
            MainWindow.Instance?.Log($"⚙️ Debug Logging set to: {(App.Settings.Current.EnableDebugLog ? "ON" : "OFF")}");
        }

        private void ToggleErrorLog_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            App.Settings.Current.EnableErrorLog = ToggleErrorLog.IsChecked ?? true;
            App.Settings.Save();
            MainWindow.Instance?.Log($"⚙️ Error Log file writing set to: {(App.Settings.Current.EnableErrorLog ? "ON" : "OFF")}");
        }

        private void TogglePluginSecurity_Click(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            App.Settings.Current.StrictPluginSecurity = TogglePluginSecurity.IsChecked ?? true;
            App.Settings.Save();
            MainWindow.Instance?.Log($"🛡️ Plugin Security Inspection set to: {(App.Settings.Current.StrictPluginSecurity ? "STRICT (ON)" : "DISABLED (OFF)")}");
        }

        private void BtnSelectFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog();
            if (dialog.ShowDialog() == true)
            {
                if (!string.IsNullOrEmpty(dialog.FolderName))
                {
                    var path = dialog.FolderName;
                    App.Settings.Current.GameDirectory = path;
                    if (!App.Settings.Current.GamePathHistory.Contains(path))
                    {
                        App.Settings.Current.GamePathHistory.Add(path);
                    }
                    App.Settings.Save();
                    
                    TxtgameDir.Text = path;
                    RefreshGameProfilesList();
                    MainWindow.Instance?.ReloadAllPlugins();
                }
            }
        }

        private void BtnResetPath_Click(object sender, RoutedEventArgs e)
        {
            App.Settings.Current.GameDirectory = string.Empty;
            App.Settings.Save();
            TxtgameDir.Text = string.Empty;
            MainWindow.Instance?.Log("🔄 Game path has been reset. Please select a new Game folder.");
            MainWindow.Instance?.ValidateGamePath();
            MainWindow.Instance?.ReloadAllPlugins();
        }

        private void ComboTheme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (ComboTheme?.SelectedItem is ComboBoxItem item && item.Tag is string theme)
            {
                App.Settings.Current.Theme = theme;
                App.Settings.Save();
                
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

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            if (sender is ScrollViewer scv)
            {
                scv.ScrollToVerticalOffset(scv.VerticalOffset - e.Delta);
                e.Handled = true;
            }
        }
    }
}
