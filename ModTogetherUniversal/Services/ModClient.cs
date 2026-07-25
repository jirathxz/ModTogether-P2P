using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModTogetherUniversal.Services
{
    public class ModClient
    {
        private readonly HttpClient _httpClient;
        private string _hostIp = "";
        private int _port = 52100;
        private string _roomToken = "";
        private string _username = "";
        private int _lastChatIndex = 0;

        public event Action<string>? OnLog;
        public event Action<int>? OnDownloadProgress;
        public event Action<string>? OnModDownloaded;

        public bool IsConnected => !string.IsNullOrEmpty(_hostIp);

        public ModClient()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public void Configure(string hostIp, int port, string roomToken, string username)
        {
            _hostIp = hostIp;
            _port = port;
            _roomToken = roomToken;
            _username = username;
            _lastChatIndex = 0;
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-room-token", _roomToken);
            _httpClient.DefaultRequestHeaders.Add("x-app-type", "MHW_SPECIAL");
        }

        private CancellationTokenSource? _syncCts;
        public event Action<List<UserSyncState>>? OnUsersUpdate;
        public event Action? OnKicked;

        public bool IsSynced { get; private set; }
        public int CurrentSyncProgress { get; private set; }
        public string CurrentActivity { get; private set; } = "";
        public int LastPingMs { get; private set; } = 0;

        public async Task<bool> HeartbeatAsync()
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsync($"http://{_hostIp}:{_port}/heartbeat?username={Uri.EscapeDataString(_username)}&isSynced={IsSynced}&syncProgress={CurrentSyncProgress}&currentActivity={Uri.EscapeDataString(CurrentActivity)}&pingMs={LastPingMs}", null);
                sw.Stop();
                LastPingMs = (int)sw.ElapsedMilliseconds;

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadFromJsonAsync<HeartbeatResponse>();
                    if (content?.status == "kicked" || content?.status == "banned")
                    {
                        OnKicked?.Invoke();
                        return false;
                    }
                    return true;
                }
            }
            catch { }
            return false;
        }

        public void StartBackgroundTasks(string cacheDir)
        {
            StopBackgroundTasks();
            _syncCts = new CancellationTokenSource();
            
            Task.Run(() => HeartbeatLoop(_syncCts.Token));
            Task.Run(() => SyncLoop(cacheDir, _syncCts.Token));
        }

        public void StopBackgroundTasks()
        {
            if (_syncCts != null)
            {
                _syncCts.Cancel();
                _syncCts = null;
            }
        }

        private async Task HeartbeatLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await HeartbeatAsync();
                await Task.Delay(2000, token).ConfigureAwait(false);
            }
        }

        private async Task SyncLoop(string cacheDir, CancellationToken token)
        {
            var skippedRecycled = new HashSet<string>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    var serverData = await GetModsAsync();
                    if (serverData != null)
                    {
                        OnUsersUpdate?.Invoke(serverData.active_users);

                        if (serverData.chat_messages != null && serverData.chat_messages.Count > _lastChatIndex)
                        {
                            for (int i = _lastChatIndex; i < serverData.chat_messages.Count; i++)
                            {
                                OnLog?.Invoke(serverData.chat_messages[i]);
                            }
                            _lastChatIndex = serverData.chat_messages.Count;
                        }

                        var localMods = new HashSet<string>(Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(cacheDir, f).Replace("\\", "/"))
                            .Where(f => !f.StartsWith(".recycle_mods/"))!);

                        var recycleDir = Path.Combine(cacheDir, ".recycle_mods");

                        // Pre-calculate sync tasks
                        var modsToUpload = localMods.Where(m => !serverData.deleted_mods.ContainsKey(m) && !serverData.mods.ContainsKey(m)).ToList();
                        
                        var modsToDownload = new List<string>();
                        foreach (var kvp in serverData.mods)
                        {
                            var relPath = kvp.Key;
                            var serverSize = kvp.Value;
                            var fullPath = Path.Combine(cacheDir, relPath);
                            var localSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : -1L;
                            
                            bool needsDownload = !localMods.Contains(relPath) || (serverSize != -1 && localSize != serverSize);
                            if (needsDownload)
                            {
                                modsToDownload.Add(relPath);
                            }
                        }

                        int totalTasks = modsToUpload.Count + modsToDownload.Count;
                        int completedTasks = 0;

                        if (totalTasks == 0)
                        {
                            IsSynced = true;
                            CurrentSyncProgress = 100;
                        }
                        else
                        {
                            IsSynced = false;
                            CurrentSyncProgress = 0;
                        }

                        // Upload missing local mods
                        foreach (var localMod in localMods.ToList())
                        {
                            if (token.IsCancellationRequested) break;

                            if (serverData.deleted_mods.ContainsKey(localMod))
                            {
                                string deleterName = serverData.deleted_mods[localMod];
                                // Sync Delete
                                var fullPath = Path.Combine(cacheDir, localMod);
                                var recyclePath = Path.Combine(recycleDir, localMod);
                                Directory.CreateDirectory(recycleDir);
                                try
                                {
                                    File.Move(fullPath, recyclePath, true);
                                    OnLog?.Invoke($"🗑️ Sync: {deleterName} deleted mod: {localMod} (Moved to recycle bin)");
                                    localMods.Remove(localMod);
                                }
                                catch { }
                                continue;
                            }

                            if (!serverData.mods.ContainsKey(localMod))
                            {
                                CurrentActivity = $"Uploading {localMod}...";
                                await HeartbeatAsync();
                                
                                var fullPath = Path.Combine(cacheDir, localMod);
                                await UploadModAsync(fullPath, localMod);
                                
                                completedTasks++;
                                CurrentSyncProgress = (int)((completedTasks * 100.0) / totalTasks);
                            }
                        }

                        // Download missing server mods
                        foreach (var kvp in serverData.mods)
                        {
                            if (token.IsCancellationRequested) break;
                            
                            var relPath = kvp.Key;
                            var serverSize = kvp.Value;
                            var fullPath = Path.Combine(cacheDir, relPath);
                            var localSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : -1L;

                            bool needsDownload = !localMods.Contains(relPath) || (serverSize != -1 && localSize != serverSize);

                            if (needsDownload)
                            {
                                CurrentActivity = $"Downloading {Path.GetFileName(relPath)}...";
                                await HeartbeatAsync();
                                
                                OnLog?.Invoke($"📥 Syncing mod from Host: {relPath}");
                                await DownloadModAsync(relPath, cacheDir);
                                
                                completedTasks++;
                                CurrentSyncProgress = (int)((completedTasks * 100.0) / totalTasks);
                            }
                        }

                        CurrentActivity = "";
                        await HeartbeatAsync();
                    }
                }
                catch (Exception ex)
                {
                    OnLog?.Invoke($"⚠️ Sync Loop Error: {ex.Message}");
                }
                await Task.Delay(3000, token).ConfigureAwait(false);
            }
        }

        public async Task<ModListResponse?> GetModsAsync()
        {
            try
            {
                return await _httpClient.GetFromJsonAsync<ModListResponse>($"http://{_hostIp}:{_port}/mods");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Error fetching mods] {ex.Message}");
                return null;
            }
        }

        public async Task SendChatAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(message)) return;
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                client.DefaultRequestHeaders.Add("x-app-type", "MHW_SPECIAL");
                client.DefaultRequestHeaders.Add("x-room-token", _roomToken);
                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", _username),
                    new KeyValuePair<string, string>("message", message)
                });
                await client.PostAsync($"http://{_hostIp}:{_port}/chat", content);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Chat Error: {ex.Message}");
            }
        }

        public async Task<bool> DownloadModAsync(string relPath, string saveDirectory)
        {
            try
            {
                var savePath = Path.Combine(saveDirectory, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

                using var response = await _httpClient.GetAsync($"http://{_hostIp}:{_port}/download/{Uri.EscapeDataString(relPath)}", HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

                var totalRead = 0L;
                var buffer = new byte[8192];
                var isMoreToRead = true;

                do
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (read == 0)
                    {
                        isMoreToRead = false;
                    }
                    else
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        totalRead += read;
                        BandwidthTracker.AddDownloadedBytes(read);
                        
                        if (totalBytes != -1)
                        {
                            var progress = (int)((totalRead * 100) / totalBytes);
                            OnDownloadProgress?.Invoke(progress);
                        }
                    }
                } while (isMoreToRead);

                OnDownloadProgress?.Invoke(100);
                OnLog?.Invoke($"[✅] Downloaded: {relPath}");
                OnModDownloaded?.Invoke(relPath);
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[❌] Error downloading {relPath}: {ex.Message}");
                return false;
            }
        }
        
        public event Action<int>? OnUploadProgress;

        public async Task UploadModAsync(string filePath, string relPath)
        {
             try
             {
                 using var content = new MultipartFormDataContent();
                 
                 var fileStream = File.OpenRead(filePath);
                 var fileContent = new ProgressableStreamContent(fileStream, progress =>
                 {
                     OnUploadProgress?.Invoke(progress);
                 });
                 
                 content.Add(fileContent, "file", Path.GetFileName(filePath));
                 
                 content.Add(new StringContent(relPath), "rel_path");
                 content.Add(new StringContent(_username), "username");

                 var response = await _httpClient.PostAsync($"http://{_hostIp}:{_port}/upload", content);
                 response.EnsureSuccessStatusCode();
                 
                 OnLog?.Invoke($"[✅] Uploaded {relPath}");
             }
             catch (Exception ex)
             {
                 OnLog?.Invoke($"[❌] Error uploading {relPath}: {ex.Message}");
             }
        }
        
        public async Task DeleteModAsync(string relPath)
        {
             try
             {
                 var content = new FormUrlEncodedContent(new[]
                 {
                     new KeyValuePair<string, string>("rel_path", relPath),
                     new KeyValuePair<string, string>("username", _username)
                 });

                 await _httpClient.PostAsync($"http://{_hostIp}:{_port}/delete", content);
             }
             catch
             {
                 // Ignore
             }
        }
    }

    public class HeartbeatResponse
    {
        public string status { get; set; } = "";
    }

    public class ModListResponse
    {
        public Dictionary<string, long> mods { get; set; } = new();
        public Dictionary<string, string> deleted_mods { get; set; } = new();
        public List<UserSyncState> active_users { get; set; } = new();
        public List<string> chat_messages { get; set; } = new();
    }

    public class UserSyncState
    {
        public string Username { get; set; } = "";
        public bool IsSynced { get; set; }
        public int SyncProgress { get; set; }
        public string CurrentActivity { get; set; } = "";
        public int PingMs { get; set; } = 0;

        public bool IsHostUser => Username.EndsWith("(Host)", StringComparison.OrdinalIgnoreCase) || Username.Contains(" (Host)");
        public System.Windows.Visibility ManagementVisibility => IsHostUser ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
    }
}
