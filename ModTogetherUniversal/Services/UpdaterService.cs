using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModTogetherUniversal.Services
{
    public class UpdaterService
    {
        private const string RepoOwner = "jirathxz";
        private const string RepoName = "ModTogether-P2P";
        public const string CurrentVersion = "v1.0.1"; 

        public event Action<string, System.Collections.Generic.List<(string Name, string Url)>>? OnUpdateAvailable; // version, assets
        public event Action<string>? OnLog;

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                OnLog?.Invoke("🔍 Checking for updates...");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ModTogetherUniversal", "1.0"));
                
                var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var release in doc.RootElement.EnumerateArray())
                        {
                            var tag = release.GetProperty("tag_name").GetString() ?? "";
                            if (string.IsNullOrEmpty(tag)) continue;
                            
                            // Skip MHW-specific tags
                            if (tag.EndsWith("-mhw", StringComparison.OrdinalIgnoreCase)) continue;

                            string cleanCurrent = CurrentVersion.Replace("v", "", StringComparison.OrdinalIgnoreCase).Split('-')[0];
                            string cleanTag = tag.Replace("v", "", StringComparison.OrdinalIgnoreCase).Split('-')[0];

                            if (Version.TryParse(cleanTag, out var remoteVersion) && Version.TryParse(cleanCurrent, out var localVersion))
                            {
                                if (remoteVersion > localVersion)
                                {
                                    var availableAssets = new System.Collections.Generic.List<(string Name, string Url)>();
                                    if (release.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                                    {
                                        foreach (var asset in assets.EnumerateArray())
                                        {
                                            string u = asset.GetProperty("browser_download_url").GetString() ?? "";
                                            string n = asset.GetProperty("name").GetString() ?? "update.exe";
                                            // Only grab Universal or generic assets
                                            if (n.Contains("Universal", StringComparison.OrdinalIgnoreCase) || !n.Contains("MHW", StringComparison.OrdinalIgnoreCase))
                                            {
                                                availableAssets.Add((n, u));
                                            }
                                        }
                                    }
                                    
                                    if (availableAssets.Count > 0)
                                    {
                                        OnLog?.Invoke($"💡 New update available: {tag}");
                                        OnUpdateAvailable?.Invoke(tag, availableAssets);
                                        return;
                                    }
                                }
                                else
                                {
                                    // Found the latest applicable release, and it's not newer
                                    break;
                                }
                            }
                        }
                    }
                }
                OnLog?.Invoke("✅ No new updates found. You are using the latest version.");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Failed to check for updates: {ex.Message}");
            }
        }

        public async Task DownloadAndInstallUpdateAsync(string downloadUrl, string assetName, Action<int> progressCallback)
        {
            try
            {
                var newFilePath = "new_" + assetName;
                using var client = new HttpClient();
                using var response = await client.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using var contentStream = await response.Content.ReadAsStreamAsync();
                using var fileStream = new FileStream(newFilePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

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

                        if (canReportProgress)
                        {
                            progressCallback((int)((totalRead * 100) / totalBytes));
                        }
                    }
                }
                while (isMoreToRead);
                
                // Write updater.bat
                string currentExeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "ModTogetherUniversal.exe");
                string batPath = "updater.bat";
                var batLines = new[]
                {
                    "@echo off",
                    "echo Updating ModTogether... Please wait.",
                    "timeout /t 2 /nobreak > NUL",
                    $"del /f /q \"{currentExeName}\"",
                    $"ren \"{newFilePath}\" \"{assetName}\"",
                    $"start \"\" \"{assetName}\"",
                    "del \"%~f0\""
                };
                
                await File.WriteAllLinesAsync(batPath, batLines);
                
                // Start bat and exit
                Process.Start(new ProcessStartInfo { FileName = batPath, UseShellExecute = true, CreateNoWindow = false });
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"⚠️ Update failed: {ex.Message}");
            }
        }
    }
}
