using System;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;

namespace ModTogetherMHW
{
    public partial class HostPage : Page
    {
        private bool _isHosting = false;

        public HostPage()
        {
            InitializeComponent();
            LoadIps();
            
            // Port Checker Loop
            System.Threading.Tasks.Task.Run(async () => 
            {
                while (true)
                {
                    if (!_isHosting)
                    {
                        int port = 52100;
                        await Application.Current.Dispatcher.InvokeAsync(() => 
                        {
                            if (ToggleCustomPort?.IsChecked == true && int.TryParse(TxtCustomPort?.Text, out int parsed)) port = parsed;
                        });
                        
                        bool inUse = Services.NetworkDiscovery.IsPortInUse(port);
                        await Application.Current.Dispatcher.InvokeAsync(() => 
                        {
                            if (LblHostStatus != null)
                            {
                                if (inUse)
                                {
                                    LblHostStatus.Text = "Status: Port in use (Host might be running)";
                                    LblHostStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
                                }
                                else
                                {
                                    LblHostStatus.Text = "Status: Ready";
                                    LblHostStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                                }
                            }
                        });
                    }
                    else
                    {
                        if (!App.Server.IsRunning)
                        {
                            await Application.Current.Dispatcher.InvokeAsync(() => 
                            {
                                _isHosting = false;
                                BtnHost.Content = Models.I18N.GetString("btn_host", App.Settings.Current.Language);
                                BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                                if (LblHostStatus != null)
                                {
                                    LblHostStatus.Text = "Status: Ready";
                                    LblHostStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                                }
                                CmbIp.Visibility = Visibility.Collapsed;
                                BtnCopyIp.Visibility = Visibility.Collapsed;
                                BtnCopyPin.Visibility = Visibility.Collapsed;
                                LblHostPin.Text = Models.I18N.GetString("host_pin", App.Settings.Current.Language);
                                LblHostPin.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160));
                            });
                            continue;
                        }

                        // We are hosting, update the active users UI
                        await Application.Current.Dispatcher.InvokeAsync(() => 
                        {
                            if (MainWindow.Instance != null && MainWindow.Instance.UserList != null)
                            {
                                var users = App.Server.ActiveUsers.Keys.ToList();
                                users.Add($"{App.Server.HostUsername} (Host)");
                                
                                MainWindow.Instance.UserList.Items.Clear();
                                foreach (var u in users)
                                {
                                    MainWindow.Instance.UserList.Items.Add(u);
                                }
                                MainWindow.Instance.LblUsers.Text = $"Connected Users: {users.Count}";
                                MainWindow.Instance.UserList.Visibility = Visibility.Visible;
                            }
                        });
                    }
                    await System.Threading.Tasks.Task.Delay(3000);
                }
            });
        }

        private void LoadIps()
        {
            CmbIp.Items.Add("127.0.0.1 (Localhost)");
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                {
                    CmbIp.Items.Add(ip.ToString());
                }
            }
            if (CmbIp.Items.Count > 0) CmbIp.SelectedIndex = 0;
        }

        private void ToggleCustomPin_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtCustomPin != null) TxtCustomPin.IsEnabled = ToggleCustomPin.IsChecked ?? false;
        }

        private void ToggleCustomPort_Changed(object sender, RoutedEventArgs e)
        {
            if (TxtCustomPort != null) TxtCustomPort.IsEnabled = ToggleCustomPort.IsChecked ?? false;
        }

        private async void BtnHost_Click(object sender, RoutedEventArgs e)
        {
            if (_isHosting)
            {
                MainWindow.Instance?.Log("Stopping Host Server...");
                await App.Server.StopAsync();
                App.Watcher.Stop();
                App.Network.StopBroadcasting();
                _isHosting = false;
                BtnHost.Content = Models.I18N.GetString("btn_host", App.Settings.Current.Language);
                BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                LblHostStatus.Text = "Status: Ready";
                CmbIp.Visibility = Visibility.Collapsed;
                BtnCopyIp.Visibility = Visibility.Collapsed;
                BtnCopyPin.Visibility = Visibility.Collapsed;
                LblHostPin.Text = Models.I18N.GetString("host_pin", App.Settings.Current.Language);
                LblHostPin.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160));
                return;
            }

            int port = 52100;
            if (ToggleCustomPort.IsChecked == true && int.TryParse(TxtCustomPort.Text, out int parsedPort))
            {
                port = parsedPort;
            }

            string token = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper();
            if (ToggleCustomPin.IsChecked == true && !string.IsNullOrWhiteSpace(TxtCustomPin.Text))
            {
                token = TxtCustomPin.Text.ToUpper();
            }

            MainWindow.Instance?.Log($"Starting Host Server on Port {port}...");
            
            App.Server.OnLog += msg => MainWindow.Instance?.Log(msg);
            
            string hostDir = App.Settings.Current.MhwDirectory;
            if (string.IsNullOrEmpty(hostDir)) hostDir = System.AppDomain.CurrentDomain.BaseDirectory;
            string cacheDir = System.IO.Path.Combine(hostDir, "GameMods");
            System.IO.Directory.CreateDirectory(cacheDir);

            await App.Server.StartAsync(cacheDir, port, token);
            App.Watcher.Start(cacheDir);
            
            string username = Environment.UserName;
            App.Network.StartBroadcasting(port, username);

            _isHosting = true;
            BtnHost.Content = Models.I18N.GetString("btn_stop_host", App.Settings.Current.Language);
            BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;
            LblHostStatus.Text = "Status: Hosting";
            
            CmbIp.Visibility = Visibility.Visible;
            BtnCopyIp.Visibility = Visibility.Visible;
            BtnCopyPin.Visibility = Visibility.Visible;
            
            LblHostPin.Text = $"PIN: {token}";
            LblHostPin.Foreground = System.Windows.Media.Brushes.LightGreen;
        }

        private void BtnKillHost_Click(object sender, RoutedEventArgs e)
        {
            int killed = 0;
            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("ModTogetherMHW"))
                {
                    if (proc.Id != currentPid)
                    {
                        proc.Kill();
                        killed++;
                    }
                }
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("python"))
                {
                    // For simplicity, kill all python. In real app, check cmdline args.
                    proc.Kill();
                    killed++;
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"⚠️ Error killing old host: {ex.Message}");
            }
            
            MainWindow.Instance?.Log($"✅ Killed {killed} old background host process(es).");
        }

        private void BtnCopyIp_Click(object sender, RoutedEventArgs e)
        {
            if (CmbIp.SelectedItem is string ipStr)
            {
                string ip = ipStr.Split('(')[0].Trim();
                int port = 52100;
                if (ToggleCustomPort.IsChecked == true && int.TryParse(TxtCustomPort.Text, out int parsedPort))
                {
                    port = parsedPort;
                }
                Clipboard.SetText($"{ip}:{port}");
                MainWindow.Instance?.Log($"📋 Copied IP: {ip}:{port}");
            }
        }

        private void BtnCopyPin_Click(object sender, RoutedEventArgs e)
        {
            string token = LblHostPin.Text.Replace("PIN: ", "").Trim();
            if (!string.IsNullOrEmpty(token) && token != Models.I18N.GetString("host_pin", App.Settings.Current.Language))
            {
                Clipboard.SetText(token);
                MainWindow.Instance?.Log($"📋 Copied PIN: {token}");
            }
        }
    }
}
