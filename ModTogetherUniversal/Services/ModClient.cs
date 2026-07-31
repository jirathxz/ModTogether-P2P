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

        public System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> IgnoreSyncTriggers = new();

        public string ServerIp => _hostIp;
        public int ServerPort => _port;
        public string Token => _roomToken;

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
        public List<UserSyncState> LastKnownUsers { get; private set; } = new();

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
            // Bug G Fix: Clear host IP so IsConnected returns false after disconnect
            _hostIp = "";
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
                        LastKnownUsers = serverData.active_users;
                        OnUsersUpdate?.Invoke(serverData.active_users);

                        if (serverData.chat_messages != null && serverData.chat_messages.Count > _lastChatIndex)
                        {
                            for (int i = _lastChatIndex; i < serverData.chat_messages.Count; i++)
                            {
                                OnLog?.Invoke(serverData.chat_messages[i]);
                            }
                            _lastChatIndex = serverData.chat_messages.Count;
                        }
                        else if (serverData.chat_messages != null && _lastChatIndex > serverData.chat_messages.Count)
                        {
                            // Bug #4 Fix: Server pruned history (capped at 100), reset our index
                            _lastChatIndex = serverData.chat_messages.Count;
                        }

                        var serverMods = new Dictionary<string, long>(serverData.mods, StringComparer.OrdinalIgnoreCase);
                        var serverDeletedMods = new Dictionary<string, string>(serverData.deleted_mods, StringComparer.OrdinalIgnoreCase);
                        
                        var localMods = new HashSet<string>(Directory.GetFiles(cacheDir, "*.*", SearchOption.AllDirectories)
                            .Select(f => Path.GetRelativePath(cacheDir, f).Replace("\\", "/"))
                            .Where(f => !f.StartsWith(".recycle_mods/"))!, StringComparer.OrdinalIgnoreCase);

                        var recycleDir = Path.Combine(cacheDir, ".recycle_mods");

                        // Pre-calculate sync tasks
                        var modsToUpload = localMods.Where(m => !serverDeletedMods.ContainsKey(m) && !serverMods.ContainsKey(m)).ToList();
                        
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

                            if (serverDeletedMods.ContainsKey(localMod))
                            {
                                string deleterName = serverDeletedMods[localMod];
                                // Sync Delete
                                var fullPath = Path.Combine(cacheDir, localMod);
                                var recyclePath = Path.Combine(recycleDir, localMod);
                                Directory.CreateDirectory(recycleDir);
                                try
                                {
                                    IgnoreSyncTriggers[localMod] = DateTime.UtcNow;
                                    ModTogether.API.FileHelper.SafeMove(fullPath, recyclePath, true);
                                    OnLog?.Invoke($"🗑️ Sync: {deleterName} deleted mod: {localMod} (Moved to recycle bin)");
                                    localMods.Remove(localMod);
                                }
                                catch { }
                                continue;
                            }

                            if (!serverMods.ContainsKey(localMod))
                            {
                                CurrentActivity = $"Uploading {Path.GetFileName(localMod)}...";
                                await HeartbeatAsync();
                                
                                var fullPath = Path.Combine(cacheDir, localMod);
                                bool success = await UploadModAsync(fullPath, localMod);
                                
                                if (success)
                                {
                                    completedTasks++;
                                    CurrentSyncProgress = (int)((completedTasks * 100.0) / totalTasks);
                                }
                                else
                                {
                                    CurrentActivity = $"❌ Error Uploading: {Path.GetFileName(localMod)}";
                                    goto doneProcessing; // Stop and wait for next loop
                                }
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
                                bool success = await DownloadModAsync(relPath, cacheDir);
                                
                                if (success)
                                {
                                    completedTasks++;
                                    CurrentSyncProgress = (int)((completedTasks * 100.0) / totalTasks);
                                }
                                else
                                {
                                    CurrentActivity = $"❌ Error Downloading: {Path.GetFileName(relPath)}";
                                    goto doneProcessing; // Stop and wait for next loop
                                }
                            }
                        }

                        // Bug #3 Fix: Set IsSynced=true after all tasks complete in THIS iteration
                        IsSynced = true;
                        CurrentSyncProgress = 100;
                        // Bug #1 Fix: Clear error activity now that everything succeeded
                        CurrentActivity = "";

                        doneProcessing:
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
            // Bug C Fix: Write to .tmp file first, then atomically move on success to avoid corrupt partial files
            var savePath = Path.Combine(saveDirectory, relPath);
            var tmpPath = savePath + ".tmp";
            try
            {
                IgnoreSyncTriggers[relPath] = DateTime.UtcNow;
                Directory.CreateDirectory(Path.GetDirectoryName(savePath)!);

                using var response = await _httpClient.GetAsync($"http://{_hostIp}:{_port}/download/{Uri.EscapeDataString(relPath)}", HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                using var contentStream = await response.Content.ReadAsStreamAsync();

                // Use a non-using scope so we can Close() before File.Move (avoid double-dispose)
                var fileStream = new FileStream(tmpPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);
                try
                {
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
                }
                finally
                {
                    await fileStream.DisposeAsync(); // Ensure file is flushed and closed before Move
                }

                // Atomically swap tmp -> final file only after full download
                if (File.Exists(savePath)) File.Delete(savePath);
                File.Move(tmpPath, savePath);

                OnDownloadProgress?.Invoke(100);
                OnLog?.Invoke($"[✅] Downloaded: {relPath}");
                OnModDownloaded?.Invoke(relPath);
                return true;
            }
            catch (Exception ex)
            {
                // Clean up partial tmp file
                try { if (File.Exists(tmpPath)) File.Delete(tmpPath); } catch { }
                OnLog?.Invoke($"[❌] Error downloading {relPath}: {ex.Message}");
                return false;
            }
        }
        
        public event Action<int>? OnUploadProgress;

        public async Task<bool> UploadModAsync(string filePath, string relPath)
        {
             try
             {
                 using var content = new MultipartFormDataContent();
                 
                 // Bug #2 Fix: Use explicit `using` to guarantee FileStream is always disposed
                 using var fileStream = File.OpenRead(filePath);
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
                 return true;
             }
             catch (Exception ex)
             {
                 OnLog?.Invoke($"[❌] Error uploading {relPath}: {ex.Message}");
                 return false;
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
