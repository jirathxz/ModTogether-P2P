using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Windows;
using System.Windows.Controls;
using ModTogetherUniversal.Models;

namespace ModTogetherUniversal
{
    public partial class RoomPage : Page
    {
        private bool _isHosting = false;
        private System.Threading.CancellationTokenSource? _statusMonitorCancellation;

        public RoomPage()
        {
            InitializeComponent();
            LoadIps();
            ApplyTranslations();

            Loaded += (_, _) => 
            {
                var state = Services.SessionManager.Instance.State;
                if (!string.IsNullOrWhiteSpace(state.HostCustomPort) && TxtCustomPort != null) TxtCustomPort.Text = state.HostCustomPort;
                if (ToggleCustomPort != null) ToggleCustomPort.IsChecked = state.HostToggleCustomPort;
                if (!string.IsNullOrWhiteSpace(state.ClientIp) && TxtIp != null) TxtIp.Text = state.ClientIp;
                if (!string.IsNullOrWhiteSpace(state.ClientPin) && TxtPin != null) TxtPin.Text = state.ClientPin;

                if (App.Server != null && App.Server.IsRunning)
                {
                    _isHosting = true;
                    BtnHost.Content = I18N.GetString("btn_stop_host", App.Settings.Current.Language);
                    BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;
                    if (LblHostStatus != null)
                    {
                        LblHostStatus.Text = "Status: Hosting";
                        LblHostStatus.Foreground = System.Windows.Media.Brushes.LightGreen;
                    }
                    CmbIp.Visibility = Visibility.Visible;
                    BtnCopyIp.Visibility = Visibility.Visible;
                    BtnCopyPin.Visibility = Visibility.Visible;
                    LblHostPin.Text = $"PIN: {App.Server.RoomToken}";
                    LblHostPin.Foreground = System.Windows.Media.Brushes.LightGreen;
                }

                if (App.Client != null && App.Client.IsConnected)
                {
                    BtnJoin.Content = "Disconnect";
                    BtnJoin.Appearance = Wpf.Ui.Controls.ControlAppearance.Danger;
                }

                StartStatusMonitor();
            };
            Unloaded += (_, _) => StopStatusMonitor();

            App.Settings.OnSettingsChanged += () =>
            {
                Dispatcher.Invoke(ApplyTranslations);
            };

        }



        private void StartStatusMonitor()
        {
            if (_statusMonitorCancellation != null) return;

            _statusMonitorCancellation = new System.Threading.CancellationTokenSource();
            _ = MonitorStatusAsync(_statusMonitorCancellation.Token);
        }

        private void StopStatusMonitor()
        {
            _statusMonitorCancellation?.Cancel();
            _statusMonitorCancellation?.Dispose();
            _statusMonitorCancellation = null;
        }

        private async System.Threading.Tasks.Task MonitorStatusAsync(System.Threading.CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    if (App.Server != null && App.Server.IsRunning)
                    {
                        _isHosting = true;
                    }

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
                        if (App.Server == null || !App.Server.IsRunning)
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
                                 
                                 bool isClientActive = App.Client != null && App.Client.IsConnected;
                             });
                             continue;
                         }

                         await Application.Current.Dispatcher.InvokeAsync(() =>
                         {
                         });

                        // We are hosting, update the active users UI
                        await Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            var users = App.Server.ActiveUsers.Select(kvp => new Services.UserSyncState 
                            { 
                                Username = kvp.Key, 
                                IsSynced = kvp.Value.IsSynced, 
                                SyncProgress = kvp.Value.SyncProgress,
                                CurrentActivity = !string.IsNullOrEmpty(kvp.Value.CurrentActivity) 
                                    ? kvp.Value.CurrentActivity 
                                    : (kvp.Value.IsSynced ? "🟢 Ready" : $"⚡ Syncing {kvp.Value.SyncProgress}%"),
                                PingMs = kvp.Value.PingMs
                            }).ToList();
                            
                            users.Add(new Services.UserSyncState 
                            { 
                                Username = $"{App.Server.HostUsername} (Host)", 
                                IsSynced = true, 
                                SyncProgress = 100,
                                CurrentActivity = "👑 Host (Ready)",
                                PingMs = 0 
                            });

                             if (ListSessionMembers != null)
                             {
                                 var activePlugin = Services.PluginManager.Instance.LoadedPlugins.FirstOrDefault();
                                 string gameName = activePlugin?.TargetGame ?? "Monster Hunter: World";
                                 Services.DiscordRpcService.Instance.UpdatePresence($"Playing ModTogether | {gameName}", "Host Room", users.Count, 4, App.Server.RoomToken);
                                 ListSessionMembers.Items.Clear();
                                 foreach (var u in users)
                                 {
                                     ListSessionMembers.Items.Add(u);
                                 }
                                TxtSessionEmpty.Visibility = Visibility.Collapsed;
                                LblSessionSummary.Text = $"{users.Count} member{(users.Count == 1 ? string.Empty : "s")} connected · {users.Count(u => u.IsSynced)} ready";
                            }

                            if (MainWindow.Instance != null && MainWindow.Instance.UserList != null)
                            {
                                MainWindow.Instance.UserList.Items.Clear();
                                foreach (var u in users)
                                {
                                    MainWindow.Instance.UserList.Items.Add(u);
                                }
                                int syncedCount = users.Count(u => u.IsSynced);
                                MainWindow.Instance.LblUsers.Text = $"Party Readiness: {syncedCount}/{users.Count} Ready";
                                MainWindow.Instance.UserList.Visibility = Visibility.Visible;
                            }
                        });
                    }
                    await System.Threading.Tasks.Task.Delay(3000, cancellationToken);
                }
            }
            catch (System.OperationCanceledException)
            {
                // Page navigation intentionally stops this background monitor.
            }
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
            CmbIp.Items.Clear();
            CmbIp.Items.Add("127.0.0.1 (Localhost)");

            try
            {
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
                foreach (var ni in interfaces)
                {
                    if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                    if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                    var ipProps = ni.GetIPProperties();
                    foreach (var addr in ipProps.UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            string ipStr = addr.Address.ToString();
                            string label = GetInterfaceLabel(ni, ipStr);
                            CmbIp.Items.Add($"{ipStr} ({label})");
                        }
                    }
                }
            }
            catch
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        CmbIp.Items.Add(ip.ToString());
                    }
                }
            }

            if (CmbIp.Items.Count > 0) CmbIp.SelectedIndex = 0;
        }

        private string GetInterfaceLabel(System.Net.NetworkInformation.NetworkInterface ni, string ip)
        {
            string name = ni.Name.ToLowerInvariant();
            string desc = ni.Description.ToLowerInvariant();

            if (name.Contains("zerotier") || desc.Contains("zerotier")) return "ZeroTier";
            if (name.Contains("tailscale") || desc.Contains("tailscale")) return "Tailscale";
            if (name.Contains("hamachi") || desc.Contains("hamachi")) return "Hamachi";
            if (name.Contains("radmin") || desc.Contains("radmin")) return "Radmin VPN";
            if (name.Contains("wireguard") || desc.Contains("wireguard")) return "WireGuard VPN";
            if (name.Contains("openvpn") || desc.Contains("openvpn")) return "OpenVPN";
            if (name.Contains("veth") || name.Contains("wsl") || desc.Contains("hyper-v")) return "WSL / Virtual";

            if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211) return "Wi-Fi LAN";
            if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet) return "Ethernet LAN";

            return ni.Name;
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

                    if (ToggleUpnp.IsChecked == true)
                    {
                        int stopPort = 52100;
                        if (ToggleCustomPort.IsChecked == true && int.TryParse(TxtCustomPort.Text, out int parsedPort2)) stopPort = parsedPort2;
                        await Services.UpnpService.Instance.DeletePortMappingAsync(stopPort);
                    }

                    _isHosting = false;
                    BtnHost.Content = I18N.GetString("btn_host", App.Settings.Current.Language);
                    BtnHost.Appearance = Wpf.Ui.Controls.ControlAppearance.Primary;
                    LblHostStatus.Text = "Status: Ready";
                    CmbIp.Visibility = Visibility.Collapsed;
                    BtnCopyIp.Visibility = Visibility.Collapsed;
                    BtnCopyPin.Visibility = Visibility.Collapsed;
                    LblHostPin.Text = I18N.GetString("host_pin", App.Settings.Current.Language);
                    LblHostPin.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(160, 160, 160));
                    ResetSessionDashboard();
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

                if (ToggleUpnp.IsChecked == true)
                {
                    MainWindow.Instance?.Log($"Attempting UPnP Port Forwarding for port {port}...");
                    bool upnpOk = await Services.UpnpService.Instance.TryCreatePortMappingAsync(port);
                    if (upnpOk) MainWindow.Instance?.Log("✅ UPnP Port Forwarding successful!");
                    else MainWindow.Instance?.Log("⚠️ UPnP Failed. You may need manual port forwarding.");
                }

                MainWindow.Instance?.Log($"Starting Host Server on Port {port}...");

                App.Server.OnLog += msg => MainWindow.Instance?.Log(msg);

                string hostDir = App.Settings.Current.GameDirectory;
                if (string.IsNullOrEmpty(hostDir)) hostDir = System.AppDomain.CurrentDomain.BaseDirectory;
                string cacheDir = System.IO.Path.Combine(hostDir, "GameMods");
                System.IO.Directory.CreateDirectory(cacheDir);

                App.Server.SetEnabledMods(null);
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
            int killed = KillAllModTogetherHostProcesses();
            MainWindow.Instance?.Log($"✅ Killed {killed} old background host process(es).");
        }

        public static int KillAllModTogetherHostProcesses(int port = 0)
        {
            int killed = 0;
            var currentPid = System.Diagnostics.Process.GetCurrentProcess().Id;
            try
            {
                foreach (var proc in System.Diagnostics.Process.GetProcessesByName("ModTogetherUniversal"))
                {
                    if (proc.Id != currentPid)
                    {
                        proc.Kill();
                        killed++;
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"⚠️ Error killing old host: {ex.Message}");
            }
            return killed;
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
            if (string.IsNullOrWhiteSpace(TxtIp.Text))
            {
                MainWindow.Instance?.Log("Enter a room address before joining.");
                TxtIp.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(TxtPin.Text))
            {
                MainWindow.Instance?.Log("Enter the room PIN before joining.");
                TxtPin.Focus();
                return;
            }

            try
            {
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

                    string cacheDir = System.IO.Path.Combine(App.Settings.Current.GameDirectory, "GameMods");
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

        private void Client_OnUsersUpdate(List<Services.UserSyncState> users)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (ListSessionMembers != null)
                {
                    ListSessionMembers.Items.Clear();
                    foreach (var u in users)
                    {
                        var copy = new Services.UserSyncState
                        {
                            Username = u.Username,
                            IsSynced = u.IsSynced,
                            SyncProgress = u.SyncProgress,
                            CurrentActivity = !string.IsNullOrEmpty(u.CurrentActivity) 
                                ? u.CurrentActivity 
                                : (u.IsSynced ? "🟢 Ready" : $"⚡ Syncing {u.SyncProgress}%"),
                            PingMs = u.PingMs
                        };
                        ListSessionMembers.Items.Add(copy);
                    }
                    TxtSessionEmpty.Visibility = Visibility.Collapsed;
                    LblSessionSummary.Text = $"{users.Count} member{(users.Count == 1 ? string.Empty : "s")} connected · {users.Count(u => u.IsSynced)} ready";
                }

                if (MainWindow.Instance != null && MainWindow.Instance.UserList != null)
                {
                    MainWindow.Instance.UserList.Items.Clear();
                    foreach (var u in users)
                    {
                        MainWindow.Instance.UserList.Items.Add(u);
                    }
                    int syncedCount = users.Count(u => u.IsSynced);
                    MainWindow.Instance.LblUsers.Text = $"Party Readiness: {syncedCount}/{users.Count} Ready";
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
                ResetSessionDashboard();
            });
        }

        private void ResetSessionDashboard()
        {
            ListSessionMembers?.Items.Clear();
            if (TxtSessionEmpty != null) TxtSessionEmpty.Visibility = Visibility.Visible;
            if (LblSessionSummary != null) LblSessionSummary.Text = "No active session";
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

        #region Added Event Handlers

        private void ScrollViewer_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
        }



        private bool IsHostUser(string username)
        {
            if (string.IsNullOrWhiteSpace(username)) return false;
            string hostName = App.Server?.HostUsername ?? "Host";
            return username.Equals(hostName, StringComparison.OrdinalIgnoreCase) 
                || username.StartsWith(hostName + " (Host)", StringComparison.OrdinalIgnoreCase)
                || username.EndsWith("(Host)", StringComparison.OrdinalIgnoreCase);
        }

        private void BtnKickUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string username)
            {
                if (IsHostUser(username))
                {
                    MainWindow.Instance?.Log("❌ You cannot kick the host.");
                    return;
                }

                if (App.Server == null || !App.Server.IsRunning)
                {
                    MainWindow.Instance?.Log("Start hosting before managing session members.");
                    return;
                }

                App.Server.KickedUsers.Add(username);
                App.Server.ActiveUsers.TryRemove(username, out _);
                MainWindow.Instance?.Log($"🚫 Kicked user: {username}");
            }
        }

        private void BtnBanUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string username)
            {
                if (IsHostUser(username))
                {
                    MainWindow.Instance?.Log("❌ You cannot ban the host.");
                    return;
                }

                if (App.Server == null || !App.Server.IsRunning)
                {
                    MainWindow.Instance?.Log("Start hosting before managing session members.");
                    return;
                }

                App.Server.BannedUsers.TryAdd(username, true);
                App.Server.ActiveUsers.TryRemove(username, out _);
                MainWindow.Instance?.Log($"⛔ Banned user: {username}");
                RefreshBannedUsersList();
            }
        }

        private void BtnUnbanUser_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string username)
            {
                if (App.Server != null)
                {
                    App.Server.BannedUsers.TryRemove(username, out _);
                    MainWindow.Instance?.Log($"✅ Unbanned user: {username}");
                    RefreshBannedUsersList();
                }
            }
        }

        private void RefreshBannedUsersList()
        {
            if (PanelBannedUsers == null || ListBannedUsers == null) return;

            if (App.Server != null && App.Server.IsRunning && App.Server.BannedUsers.Count > 0)
            {
                PanelBannedUsers.Visibility = Visibility.Visible;
                ListBannedUsers.Items.Clear();
                foreach (var banned in App.Server.BannedUsers.Keys)
                {
                    ListBannedUsers.Items.Add(banned);
                }
            }
            else
            {
                PanelBannedUsers.Visibility = Visibility.Collapsed;
            }
        }

        #endregion
    }
}
