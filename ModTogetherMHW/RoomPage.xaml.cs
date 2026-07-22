using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using ModTogetherMHW.Models;

namespace ModTogetherMHW
{
    public partial class RoomPage : Page
    {
        private bool _isHosting = false;

        public RoomPage()
        {
            InitializeComponent();
            LoadIps();
            ApplyTranslations();

            App.Settings.OnSettingsChanged += () =>
            {
                Dispatcher.Invoke(ApplyTranslations);
            };

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
                                BtnHost.Content = I18N.GetString("btn_host", App.Settings.Current.Language);
                                BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                                if (LblHostStatus != null)
                                {
                                    LblHostStatus.Text = "Status: Ready";
                                    LblHostStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                                }
                                CmbIp.Visibility = Visibility.Collapsed;
                                BtnCopyIp.Visibility = Visibility.Collapsed;
                                BtnCopyPin.Visibility = Visibility.Collapsed;
                                LblHostPin.Text = I18N.GetString("host_pin", App.Settings.Current.Language);
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

        public void ApplyTranslations()
        {
            string lang = App.Settings.Current.Language;
            if (TxtHostTitle != null) TxtHostTitle.Text = I18N.GetString("host_title", lang);
            if (TxtClientTitle != null) TxtClientTitle.Text = I18N.GetString("client_title", lang);
            if (BtnHost != null && !_isHosting) BtnHost.Content = I18N.GetString("btn_host", lang);
            if (BtnHost != null && _isHosting) BtnHost.Content = I18N.GetString("btn_stop_host", lang);
            if (BtnKillHost != null) BtnKillHost.Content = I18N.GetString("btn_kill_host", lang);
            if (BtnJoin != null) BtnJoin.Content = I18N.GetString("btn_join", lang);
            if (BtnScan != null) BtnScan.Content = I18N.GetString("btn_scan", lang);
            if (TxtIp != null) TxtIp.PlaceholderText = I18N.GetString("client_ip", lang);
            if (TxtPin != null) TxtPin.PlaceholderText = I18N.GetString("client_pin", lang);
            if (LblHostPin != null && !_isHosting) LblHostPin.Text = I18N.GetString("host_pin", lang);
        }

        #region Host Logic

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
            try
            {
                if (_isHosting)
                {
                    MainWindow.Instance?.Log("Stopping Host Server...");
                    await App.Server.StopAsync();
                    App.Watcher.Stop();
                    App.Network.StopBroadcasting();
                    _isHosting = false;
                    BtnHost.Content = I18N.GetString("btn_host", App.Settings.Current.Language);
                    BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                    LblHostStatus.Text = "Status: Ready";
                    CmbIp.Visibility = Visibility.Collapsed;
                    BtnCopyIp.Visibility = Visibility.Collapsed;
                    BtnCopyPin.Visibility = Visibility.Collapsed;
                    LblHostPin.Text = I18N.GetString("host_pin", App.Settings.Current.Language);
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
                BtnHost.Content = I18N.GetString("btn_stop_host", App.Settings.Current.Language);
                BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;
                LblHostStatus.Text = "Status: Hosting";

                CmbIp.Visibility = Visibility.Visible;
                BtnCopyIp.Visibility = Visibility.Visible;
                BtnCopyPin.Visibility = Visibility.Visible;

                LblHostPin.Text = $"PIN: {token}";
                LblHostPin.Foreground = System.Windows.Media.Brushes.LightGreen;
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"❌ Failed to start host: {ex.Message}");
                _isHosting = false;
                BtnHost.Content = I18N.GetString("btn_host", App.Settings.Current.Language);
                BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                LblHostStatus.Text = "Status: Error starting server";
                LblHostStatus.Foreground = System.Windows.Media.Brushes.OrangeRed;
            }
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
            if (!string.IsNullOrEmpty(token) && token != I18N.GetString("host_pin", App.Settings.Current.Language))
            {
                Clipboard.SetText(token);
                MainWindow.Instance?.Log($"📋 Copied PIN: {token}");
            }
        }

        #endregion

        #region Client Logic

        private async void BtnJoin_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(TxtIp.Text)) return;
                var parts = TxtIp.Text.Trim().Split(':');
                string ip = parts[0];
                int port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 52100;
                string token = TxtPin.Text.ToUpper();

                App.Client.Configure(ip, port, token, Environment.UserName);

                App.Client.OnUsersUpdate -= Client_OnUsersUpdate;
                App.Client.OnUsersUpdate += Client_OnUsersUpdate;
                App.Client.OnKicked -= Client_OnKicked;
                App.Client.OnKicked += Client_OnKicked;

                MainWindow.Instance?.Log($"Joining {ip}:{port}...");
                bool ok = await App.Client.HeartbeatAsync();
                if (ok)
                {
                    MainWindow.Instance?.Log("✅ Connected to Host!");

                    string cacheDir = System.IO.Path.Combine(App.Settings.Current.MhwDirectory, "GameMods");
                    System.IO.Directory.CreateDirectory(cacheDir);

                    App.Client.StartBackgroundTasks(cacheDir);

                    if (MainWindow.Instance != null && MainWindow.Instance.BtnDisconnect != null)
                    {
                        MainWindow.Instance.BtnDisconnect.IsEnabled = true;
                    }
                }
                else
                {
                    MainWindow.Instance?.Log("❌ Failed to connect. Check IP/Port and PIN.");
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"❌ Join error: {ex.Message}");
            }
        }

        private void Client_OnUsersUpdate(List<string> users)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (MainWindow.Instance != null && MainWindow.Instance.UserList != null)
                {
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

        private void Client_OnKicked()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                App.Client.StopBackgroundTasks();
                MainWindow.Instance?.Log("🚫 You have been kicked from the session.");
                if (MainWindow.Instance != null && MainWindow.Instance.BtnDisconnect != null)
                {
                    MainWindow.Instance.BtnDisconnect.IsEnabled = false;
                    MainWindow.Instance.UserList.Visibility = Visibility.Collapsed;
                    MainWindow.Instance.LblUsers.Text = "Connected Users: -";
                }
            });
        }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                BtnScan.IsEnabled = false;
                TxtIp.PlaceholderText = "Scanning LAN...";

                var servers = await App.Network.ScanAsync();

                BtnScan.IsEnabled = true;
                TxtIp.PlaceholderText = I18N.GetString("client_ip", App.Settings.Current.Language);

                if (servers.Count == 0)
                {
                    MainWindow.Instance?.Log("⚠️ No hosts found on the local network.");
                }
                else
                {
                    ListServers.ItemsSource = servers;
                    ListServers.SelectedIndex = 0;
                    ScanOverlay.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                BtnScan.IsEnabled = true;
                TxtIp.PlaceholderText = I18N.GetString("client_ip", App.Settings.Current.Language);
                MainWindow.Instance?.Log($"⚠️ Scan error: {ex.Message}");
            }
        }

        private void BtnCancelScan_Click(object sender, RoutedEventArgs e)
        {
            ScanOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnSelectScan_Click(object sender, RoutedEventArgs e)
        {
            if (ListServers.SelectedItem is string selected)
            {
                TxtIp.Text = selected.Split(' ')[0];
                MainWindow.Instance?.Log($"✅ Found session(s). Selected: {TxtIp.Text}");
            }
            ScanOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion
    }
}
