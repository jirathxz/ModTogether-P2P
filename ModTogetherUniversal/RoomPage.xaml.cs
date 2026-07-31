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

        // Bug A & B Fix: Store delegates so we can properly remove them (prevent event handler leaks)
        private readonly Action _settingsChangedHandler;
        private readonly Action<string> _serverLogHandler;

        public RoomPage()
        {
            InitializeComponent();
            // Initialize delegates once so += and -= refer to the same instance
            _settingsChangedHandler = () => Dispatcher.Invoke(ApplyTranslations);
            _serverLogHandler = msg => MainWindow.Instance?.Log(msg);
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
                    ShowActiveState("HOSTING LOBBY", App.Server.RoomToken, "0.0.0.0:" + App.Server.Port);
                }
                else if (App.Client != null && App.Client.IsConnected)
                {
                    ShowActiveState("CONNECTED TO LOBBY", App.Client.Token, App.Client.ServerIp + ":" + App.Client.ServerPort);
                    
                    App.Client.OnUsersUpdate -= Client_OnUsersUpdate;
                    App.Client.OnUsersUpdate += Client_OnUsersUpdate;
                    App.Client.OnKicked -= Client_OnKicked;
                    App.Client.OnKicked += Client_OnKicked;
                    
                    Client_OnUsersUpdate(App.Client.LastKnownUsers);
                }
                else
                {
                    ShowIdleState();
                }

                StartStatusMonitor();
            };
            Unloaded += (_, _) => 
            {
                StopStatusMonitor();
                if (App.Client != null)
                {
                    App.Client.OnUsersUpdate -= Client_OnUsersUpdate;
                    App.Client.OnKicked -= Client_OnKicked;
                }
            };

            App.Settings.OnSettingsChanged -= _settingsChangedHandler;
            App.Settings.OnSettingsChanged += _settingsChangedHandler;

        }

        private void ShowIdleState()
        {
            if (ViewIdle != null) ViewIdle.Visibility = Visibility.Visible;
            if (ViewActive != null) ViewActive.Visibility = Visibility.Collapsed;
        }

        private void ShowActiveState(string role, string pin, string ip)
        {
            if (ViewIdle != null) ViewIdle.Visibility = Visibility.Collapsed;
            if (ViewActive != null) ViewActive.Visibility = Visibility.Visible;
            
            if (LblActiveRole != null) LblActiveRole.Text = role;
            if (LblActivePin != null) LblActivePin.Text = pin;
            if (LblActiveIp != null) LblActiveIp.Text = ip;
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
                                 ResetSessionDashboard();
                                 ShowIdleState();
                                 
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
                            var users = App.Server.ActiveUsers.Select(kvp => new UserSyncViewModel 
                            { 
                                Username = kvp.Key, 
                                IsSynced = kvp.Value.IsSynced, 
                                SyncProgress = kvp.Value.SyncProgress,
                                CurrentActivity = !string.IsNullOrEmpty(kvp.Value.CurrentActivity) 
                                    ? kvp.Value.CurrentActivity 
                                    : (kvp.Value.IsSynced ? "🟢 Ready" : $"⚡ Syncing {kvp.Value.SyncProgress}%"),
                                PingMs = kvp.Value.PingMs
                            }).ToList();
                            
                            users.Insert(0, new UserSyncViewModel 
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
                                 ListSessionMembers.ItemsSource = users;
                                 LblSessionSummary.Text = $"({users.Count} Connected)";
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
            if (BtnHost != null && !_isHosting) BtnHost.Content = I18N.GetString("btn_host", lang);
            if (BtnHost != null && _isHosting) BtnHost.Content = I18N.GetString("btn_stop_host", lang);
            if (BtnKillHost != null) BtnKillHost.Content = I18N.GetString("btn_kill_host", lang);
            if (BtnJoin != null) BtnJoin.Content = I18N.GetString("btn_join", lang);
            if (BtnScan != null) BtnScan.Content = I18N.GetString("btn_scan", lang);
            if (TxtIp != null) TxtIp.PlaceholderText = I18N.GetString("client_ip", lang);
            if (TxtPin != null) TxtPin.PlaceholderText = I18N.GetString("client_pin", lang);
        }

        #region Host Logic

        private string GetPrimaryLocalIp()
        {
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
                            return addr.Address.ToString();
                        }
                    }
                }
            }
            catch { }
            return "127.0.0.1";
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
                    ResetSessionDashboard();
                    ShowIdleState();
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

                App.Server.OnLog -= _serverLogHandler; // Remove any previous handler first (Bug A fix)
                App.Server.OnLog += _serverLogHandler;

                string hostDir = App.Settings.Current.GameDirectory;
                if (string.IsNullOrEmpty(hostDir)) hostDir = System.AppDomain.CurrentDomain.BaseDirectory;
                string cacheDir = System.IO.Path.Combine(hostDir, "GameMods");
                System.IO.Directory.CreateDirectory(cacheDir);

                App.Server.SetEnabledMods(null);
                await App.Server.StartAsync(cacheDir, port, token);
                App.Client.Configure("127.0.0.1", port, token, Environment.UserName);
                App.Watcher.Start(cacheDir);

                string username = Environment.UserName;
                App.Network.StartBroadcasting(port, username);

                _isHosting = true;
                
                string ipDisplay = GetPrimaryLocalIp();
                ShowActiveState("HOSTING LOBBY", token, $"{ipDisplay}:{port}");
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
            if (!_isHosting)
            {
                if (LblActiveIp != null && !string.IsNullOrEmpty(LblActiveIp.Text))
                {
                    Clipboard.SetText(LblActiveIp.Text);
                    MainWindow.Instance?.Log($"📋 Copied Host IP: {LblActiveIp.Text}");
                }
                return;
            }

            if (!(sender is FrameworkElement btn)) return;

            var menu = new ContextMenu();
            var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            int port = App.Server?.Port ?? 52100;

            foreach (var ni in interfaces)
            {
                if (ni.OperationalStatus != System.Net.NetworkInformation.OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback) continue;

                var ipProps = ni.GetIPProperties();
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        string ip = addr.Address.ToString();
                        string label = GetInterfaceLabel(ni, ip);
                        string fullIp = $"{ip}:{port}";
                        
                        var item = new MenuItem { Header = $"{label} ({fullIp})" };
                        item.Click += (s, ev) => 
                        {
                            Clipboard.SetText(fullIp);
                            MainWindow.Instance?.Log($"📋 Copied {label} IP: {fullIp}");
                            if (LblActiveIp != null) LblActiveIp.Text = fullIp;
                        };
                        menu.Items.Add(item);
                    }
                }
            }

            if (menu.Items.Count == 0)
            {
                if (LblActiveIp != null && !string.IsNullOrEmpty(LblActiveIp.Text))
                {
                    Clipboard.SetText(LblActiveIp.Text);
                    MainWindow.Instance?.Log($"📋 Copied IP: {LblActiveIp.Text}");
                }
                return;
            }

            menu.PlacementTarget = btn;
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            menu.IsOpen = true;
        }

        private void BtnCopyPin_Click(object sender, RoutedEventArgs e)
        {
            if (LblActivePin != null && !string.IsNullOrEmpty(LblActivePin.Text))
            {
                Clipboard.SetText(LblActivePin.Text);
                MainWindow.Instance?.Log($"📋 Copied PIN: {LblActivePin.Text}");
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
                    ShowActiveState("CONNECTED TO LOBBY", token, $"{ip}:{port}");

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
                    var viewModels = users.Select(u => new UserSyncViewModel
                    {
                        Username = u.Username,
                        IsSynced = u.IsSynced,
                        SyncProgress = u.SyncProgress,
                        CurrentActivity = !string.IsNullOrEmpty(u.CurrentActivity) 
                            ? u.CurrentActivity 
                            : (u.IsSynced ? "🟢 Ready" : $"⚡ Syncing {u.SyncProgress}%"),
                        PingMs = u.PingMs
                    }).ToList();

                    ListSessionMembers.ItemsSource = viewModels;
                    LblSessionSummary.Text = $"({users.Count} Connected)";
                }
            });
        }

        private void Client_OnKicked()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ResetSessionDashboard();
                ShowIdleState();
            });
        }

        private async void BtnLeaveRoom_Click(object sender, RoutedEventArgs e)
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
            }
            else
            {
                App.Client.StopBackgroundTasks();
                if (MainWindow.Instance != null && MainWindow.Instance.BtnDisconnect != null)
                {
                    MainWindow.Instance.BtnDisconnect.IsEnabled = false;
                }
            }

            ResetSessionDashboard();
            ShowIdleState();
        }

        private void ResetSessionDashboard()
        {
            ListSessionMembers.ItemsSource = null;
            if (LblSessionSummary != null) LblSessionSummary.Text = "(0 Connected)";
            
            if (MainWindow.Instance != null)
            {
                if (MainWindow.Instance.UserList != null)
                {
                    MainWindow.Instance.UserList.Items.Clear();
                }
                if (MainWindow.Instance.LblUsers != null)
                {
                    MainWindow.Instance.LblUsers.Text = Models.I18N.GetString("lbl_users", App.Settings.Current.Language);
                }
            }
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

                App.Server.KickedUsers.TryAdd(username, true);
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
