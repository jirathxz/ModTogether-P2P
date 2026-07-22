using System.Windows;
using System.Windows.Controls;

namespace ModTogetherMHW
{
    public partial class ClientPage : Page
    {
        public ClientPage()
        {
            InitializeComponent();
        }

        private async void BtnJoin_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtIp.Text)) return;
            var parts = TxtIp.Text.Split(':');
            string ip = parts[0];
            int port = parts.Length > 1 && int.TryParse(parts[1], out int p) ? p : 52100;
            string token = TxtPin.Text.ToUpper();
            
            App.Client.Configure(ip, port, token, Environment.UserName);
            
            // Hook events for UI
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

        private void Client_OnUsersUpdate(System.Collections.Generic.List<string> users)
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
            BtnScan.IsEnabled = false;
            TxtIp.PlaceholderText = "Scanning LAN...";
            
            var servers = await App.Network.ScanAsync();
            
            BtnScan.IsEnabled = true;
            TxtIp.PlaceholderText = Models.I18N.GetString("client_ip", App.Settings.Current.Language);
            
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

        private void BtnCancelScan_Click(object sender, RoutedEventArgs e)
        {
            ScanOverlay.Visibility = Visibility.Collapsed;
        }

        private void BtnSelectScan_Click(object sender, RoutedEventArgs e)
        {
            if (ListServers.SelectedItem is string selected)
            {
                TxtIp.Text = selected.Split(' ')[0]; // Gets the IP:Port part
                MainWindow.Instance?.Log($"✅ Found session(s). Selected: {TxtIp.Text}");
            }
            ScanOverlay.Visibility = Visibility.Collapsed;
        }
    }
}
