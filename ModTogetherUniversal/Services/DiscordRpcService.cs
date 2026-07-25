using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModTogetherUniversal.Services
{
    public class DiscordRpcService
    {
        private static DiscordRpcService? _instance;
        public static DiscordRpcService Instance => _instance ??= new DiscordRpcService();

        private const string CLIENT_ID = "1200000000000000000"; // ModTogether Client ID
        private NamedPipeClientStream? _pipe;
        private bool _isConnected;

        public void Initialize()
        {
            Task.Run(ConnectAsync);
        }

        private async Task ConnectAsync()
        {
            if (_isConnected) return;
            try
            {
                for (int i = 0; i < 10; i++)
                {
                    try
                    {
                        _pipe = new NamedPipeClientStream(".", $"discord-ipc-{i}", PipeDirection.InOut);
                        await _pipe.ConnectAsync(1000);
                        _isConnected = true;
                        SendHandshake();
                        UpdatePresence("Browsing Mods", "Idle");
                        break;
                    }
                    catch
                    {
                        _pipe?.Dispose();
                        _pipe = null;
                    }
                }
            }
            catch
            {
                _isConnected = false;
            }
        }

        private void SendHandshake()
        {
            if (!_isConnected || _pipe == null) return;
            try
            {
                var payload = JsonSerializer.Serialize(new { v = 1, client_id = CLIENT_ID });
                SendPacket(0, payload);
            }
            catch { }
        }

        private void SendPacket(int opcode, string json)
        {
            if (!_isConnected || _pipe == null || !_pipe.IsConnected) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                byte[] header = new byte[8];
                BitConverter.GetBytes(opcode).CopyTo(header, 0);
                BitConverter.GetBytes(bytes.Length).CopyTo(header, 4);

                _pipe.Write(header, 0, 8);
                _pipe.Write(bytes, 0, bytes.Length);
                _pipe.Flush();
            }
            catch
            {
                _isConnected = false;
            }
        }

        public void UpdatePresence(string details, string state, int currentParty = 0, int maxParty = 0, string roomPin = "")
        {
            if (!_isConnected) return;

            try
            {
                var presenceObj = new
                {
                    cmd = "SET_ACTIVITY",
                    args = new
                    {
                        pid = System.Diagnostics.Process.GetCurrentProcess().Id,
                        activity = new
                        {
                            details = details,
                            state = !string.IsNullOrEmpty(roomPin) ? $"{state} (PIN: {roomPin})" : state,
                            assets = new
                            {
                                large_image = "modtogether_logo",
                                large_text = "ModTogether Universal"
                            },
                            party = maxParty > 0 ? new { size = new[] { currentParty, maxParty } } : null
                        }
                    },
                    nonce = Guid.NewGuid().ToString()
                };

                string json = JsonSerializer.Serialize(presenceObj);
                SendPacket(1, json);
            }
            catch { }
        }

        public void Shutdown()
        {
            try
            {
                _pipe?.Dispose();
                _isConnected = false;
            }
            catch { }
        }
    }
}
