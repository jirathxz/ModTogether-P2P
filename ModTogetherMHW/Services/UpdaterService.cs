using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModTogetherMHW.Services
{
    public class UpdaterService
    {
        private const string RepoOwner = "jirathxz";
        private const string RepoName = "ModTogether-P2P";
        public const string CurrentVersion = "v1.0.0a6"; 

        public event Action<string, string, string>? OnUpdateAvailable; // version, url, filename
        public event Action<string>? OnLog;

        public async Task CheckForUpdatesAsync()
        {
            try
            {
                OnLog?.Invoke("🔍 Checking for updates...");
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ModTogetherMHW", "1.0"));
                
                var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;
                    
                    var tag = root.GetProperty("tag_name").GetString() ?? "";
                    if (!string.IsNullOrEmpty(tag) && tag != CurrentVersion)
                    {
                        // In a real app, compare version numbers properly.
                        // Here we just check if it's different.
                        
                        string downloadUrl = "";
                        string assetName = "";
                        
                        if (root.TryGetProperty("assets", out var assets) && assets.GetArrayLength() > 0)
                        {
                            var firstAsset = assets[0];
                            downloadUrl = firstAsset.GetProperty("browser_download_url").GetString() ?? "";
                            assetName = firstAsset.GetProperty("name").GetString() ?? "update.exe";
                        }
                        
                        if (!string.IsNullOrEmpty(downloadUrl))
                        {
                            OnLog?.Invoke($"💡 New update available: {tag}");
                            OnUpdateAvailable?.Invoke(tag, downloadUrl, assetName);
                            return;
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
                string currentExeName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "ModTogetherMHW.exe");
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
