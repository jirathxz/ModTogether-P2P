using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ModTogetherMHW.Services
{
    public class NetworkDiscovery
    {
        private const int BroadcastPort = 52101;
        private CancellationTokenSource? _hostCts;
        private string _username = "Unknown";
        
        public event Action<string>? OnLog;

        public void StartBroadcasting(int hostPort, string username)
        {
            StopBroadcasting();
            _username = username;
            _hostCts = new CancellationTokenSource();
            
            Task.Run(() => BroadcastLoop(hostPort, _hostCts.Token));
        }

        public void StopBroadcasting()
        {
            if (_hostCts != null)
            {
                _hostCts.Cancel();
                _hostCts = null;
            }
        }

        private async Task BroadcastLoop(int hostPort, CancellationToken token)
        {
            using var udpClient = new UdpClient();
            udpClient.EnableBroadcast = true;
            
            var msg = Encoding.UTF8.GetBytes($"MODTOGETHER_MHW_HOST:{hostPort}:{_username}");
            var endpoint = new IPEndPoint(IPAddress.Broadcast, BroadcastPort);

            while (!token.IsCancellationRequested)
            {
                try
                {
                    await udpClient.SendAsync(msg, msg.Length, endpoint);
                }
                catch
                {
                    // Ignore broadcast errors
                }
                
                await Task.Delay(1500, token).ConfigureAwait(false);
            }
        }

        public async Task<List<string>> ScanAsync()
        {
            var foundServers = new HashSet<string>();
            
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2.5));
                using var udpClient = new UdpClient();
                
                udpClient.Client.ExclusiveAddressUse = false;
                udpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                udpClient.Client.Bind(new IPEndPoint(IPAddress.Any, BroadcastPort));
                
                while (!cts.Token.IsCancellationRequested)
                {
                    var receiveResult = await udpClient.ReceiveAsync(cts.Token);
                    var msg = Encoding.UTF8.GetString(receiveResult.Buffer);
                    
                    if (msg.StartsWith("MODTOGETHER_MHW_HOST:"))
                    {
                        var parts = msg.Split(':');
                        if (parts.Length >= 3)
                        {
                            var port = parts[1];
                            var hostname = parts[2];
                            foundServers.Add($"{receiveResult.RemoteEndPoint.Address}:{port} ({hostname})");
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Timeout reached normally
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Scan error: {ex.Message}");
            }

            return new List<string>(foundServers);
        }
        
        public static bool IsPortInUse(int port)
        {
            bool inUse = false;
            try
            {
                using var client = new TcpClient();
                var result = client.BeginConnect("127.0.0.1", port, null, null);
                var success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                if (success)
                {
                    client.EndConnect(result);
                    inUse = true;
                }
            }
            catch
            {
                // Port not in use
            }
            return inUse;
        }
    }
}
