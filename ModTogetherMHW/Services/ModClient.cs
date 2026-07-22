using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ModTogetherMHW.Services
{
    public class ModClient
    {
        private readonly HttpClient _httpClient;
        private string _hostIp = "";
        private int _port = 52100;
        private string _roomToken = "";
        private string _username = "";

        public event Action<string>? OnLog;
        public event Action<int>? OnDownloadProgress;
        public event Action<string>? OnModDownloaded;

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
            
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-room-token", _roomToken);
            _httpClient.DefaultRequestHeaders.Add("x-app-type", "MHW_SPECIAL");
        }

        private CancellationTokenSource? _syncCts;
        public event Action<List<string>>? OnUsersUpdate;
        public event Action? OnKicked;

        public async Task<bool> HeartbeatAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"http://{_hostIp}:{_port}/heartbeat?username={Uri.EscapeDataString(_username)}", null);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadFromJsonAsync<HeartbeatResponse>();
                    if (content?.status == "kicked")
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

                        var localMods = new HashSet<string>(Directory.GetFiles(cacheDir, "*.*")
                            .Where(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                                        f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || 
                                        f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase))
                            .Select(Path.GetFileName)!);

                        var recycleDir = Path.Combine(cacheDir, ".recycle_mods");

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
                                var fullPath = Path.Combine(cacheDir, localMod);
                                await UploadModAsync(fullPath, localMod);
                            }
                        }

                        // Download missing server mods
                        foreach (var kvp in serverData.mods)
                        {
                            if (token.IsCancellationRequested) break;
                            
                            var relPath = kvp.Key;
                            var serverSize = kvp.Value;
                            var fullPath = Path.Combine(cacheDir, relPath);
                            var recyclePath = Path.Combine(recycleDir, relPath);
                            
                            var isRecycled = File.Exists(recyclePath);
                            var localSize = File.Exists(fullPath) ? new FileInfo(fullPath).Length : -1L;

                            // Smart Restore
                            if (isRecycled && (!localMods.Contains(relPath) || (serverSize != -1 && localSize != serverSize)))
                            {
                                try
                                {
                                    File.Move(recyclePath, fullPath, true);
                                    OnLog?.Invoke($"♻️ Smart Restore: Restored from recycle bin: {relPath}");
                                    isRecycled = false;
                                    localMods.Add(relPath);
                                    localSize = new FileInfo(fullPath).Length;
                                }
                                catch { }
                            }

                            if (isRecycled && !skippedRecycled.Contains(relPath))
                            {
                                OnLog?.Invoke($"⚠️ Skipped sync for {relPath} (exists in local recycle bin)");
                                skippedRecycled.Add(relPath);
                            }

                            bool needsDownload = (!localMods.Contains(relPath) || (serverSize != -1 && localSize != serverSize)) && !isRecycled;

                            if (needsDownload)
                            {
                                OnLog?.Invoke($"📥 Syncing mod from Host: {relPath}");
                                await DownloadModAsync(relPath, cacheDir);
                            }
                        }
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
        public List<string> active_users { get; set; } = new();
    }
}
