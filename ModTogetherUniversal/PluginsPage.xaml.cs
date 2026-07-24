using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ModTogetherUniversal.Services;

namespace ModTogetherUniversal
{
    public partial class PluginsPage : Page
    {
        public PluginsPage()
        {
            InitializeComponent();
            this.Loaded += PluginsPage_Loaded;
            App.Settings.OnSettingsChanged += ApplyTranslations;
        }

        private void ApplyTranslations()
        {
            var lang = App.Settings.Current.Language;
            if (TxtTitle != null) TxtTitle.Text = Models.I18N.GetString("plugins_title", lang);
            if (TxtDesc != null) TxtDesc.Text = Models.I18N.GetString("plugins_desc", lang);
            if (BtnOpenFolder != null) BtnOpenFolder.Content = Models.I18N.GetString("plugins_btn_open", lang);
        }

        private void PluginsPage_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyTranslations();
            ListExtensions.ItemsSource = null;
            ListExtensions.ItemsSource = PluginManager.Instance.LoadedPlugins;
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            string pluginsPath = PluginManager.Instance.GetPluginsPath();
            Directory.CreateDirectory(pluginsPath);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo() 
            { 
                FileName = pluginsPath, 
                UseShellExecute = true, 
                Verb = "open" 
            });
        }
    }
}

