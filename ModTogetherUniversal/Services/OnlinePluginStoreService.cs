using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace ModTogetherUniversal.Services
{
    public class PluginStoreItem
    {
        public string Name { get; set; } = "";
        public string GameId { get; set; } = "";
        public string TargetGame { get; set; } = "";
        public string Description { get; set; } = "";
        public string Version { get; set; } = "1.0.0";
        public string Author { get; set; } = "jirathxz";
        public string DownloadUrl { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public string FileSizeText => FileSizeBytes > 0 ? $"{FileSizeBytes / 1024.0:F1} KB" : "Release Asset";
        public string Permissions { get; set; } = "📁 Disk Access, 🌐 P2P Sync";
        public string TrustLevel { get; set; } = "Official Release";
        public bool IsInstalled { get; set; }
        public bool IsUpdateAvailable { get; set; }
        public string LocalSha256 { get; set; } = "";
        public string DllFileName { get; set; } = "";
    }

    public class OnlinePluginStoreService
    {
        private static OnlinePluginStoreService? _instance;
        public static OnlinePluginStoreService Instance => _instance ??= new OnlinePluginStoreService();

        private const string REPO_OWNER = "jirathxz";
        private const string REPO_NAME = "ModTogether-P2P";

        public async Task<List<PluginStoreItem>> FetchCatalogFromGitHubAsync()
        {
            var pluginsDir = PluginManager.Instance.GetPluginsPath();
            Directory.CreateDirectory(pluginsDir);

            var items = new List<PluginStoreItem>();

            try
            {
                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ModTogetherUniversal", "1.0"));

                var url = $"https://api.github.com/repos/{REPO_OWNER}/{REPO_NAME}/releases";
                var response = await client.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);

                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var release in doc.RootElement.EnumerateArray())
                        {
                            string tag = release.TryGetProperty("tag_name", out var tagProp) ? tagProp.GetString() ?? "" : "";
                            string authorName = release.TryGetProperty("author", out var authorProp) && authorProp.TryGetProperty("login", out var loginProp)
                                ? loginProp.GetString() ?? "jirathxz"
                                : "jirathxz";

                            if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
                            {
                                foreach (var asset in assets.EnumerateArray())
                                {
                                    string assetName = asset.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? "" : "";
                                    string downloadUrl = asset.TryGetProperty("browser_download_url", out var uProp) ? uProp.GetString() ?? "" : "";
                                    long size = asset.TryGetProperty("size", out var sProp) ? sProp.GetInt64() : 0;

                                    // Filter ONLY actual DLL release assets
                                    if (assetName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                                    {
                                        string gameName = DeduceGameName(assetName);
                                        string gameId = DeduceGameId(assetName);

                                        items.Add(new PluginStoreItem
                                        {
                                            Name = assetName,
                                            GameId = gameId,
                                            TargetGame = gameName,
                                            Description = $"Official GitHub Release asset '{assetName}' ({tag}) for {gameName}.",
                                            Version = !string.IsNullOrEmpty(tag) ? tag : "v1.0.0",
                                            Author = authorName,
                                            DownloadUrl = downloadUrl,
                                            FileSizeBytes = size,
                                            DllFileName = assetName,
                                            Permissions = "📁 Disk Access, 🌐 Network P2P, ⚙️ Game API",
                                            TrustLevel = "Verified GitHub Release"
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"⚠️ GitHub Release API query warning: {ex.Message}");
            }

            // Verify local installation state and SHA-256 for all items
            foreach (var item in items)
            {
                string localFile = Path.Combine(pluginsDir, item.DllFileName);
                if (File.Exists(localFile))
                {
                    item.IsInstalled = true;
                    item.LocalSha256 = ComputeSha256(localFile);
                    item.IsUpdateAvailable = !string.IsNullOrEmpty(item.Sha256) && item.LocalSha256 != item.Sha256;
                }
                else
                {
                    item.IsInstalled = false;
                    item.IsUpdateAvailable = false;
                }
            }

            return items;
        }

        public async Task<(bool Success, string Message)> DownloadAndInstallPluginAsync(PluginStoreItem item)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(item.DownloadUrl))
                {
                    return (false, "Invalid download URL for release asset.");
                }

                var pluginsDir = PluginManager.Instance.GetPluginsPath();
                Directory.CreateDirectory(pluginsDir);
                string targetFile = Path.Combine(pluginsDir, item.DllFileName);

                using var client = new HttpClient();
                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("ModTogetherUniversal", "1.0"));

                MainWindow.Instance?.Log($"⬇️ Downloading real plugin DLL from GitHub Releases: {item.DownloadUrl}...");
                var response = await client.GetAsync(item.DownloadUrl);
                
                if (!response.IsSuccessStatusCode)
                {
                    return (false, $"ไม่พบไฟล์ปลั๊กอิน (.dll) ใน GitHub Release: HTTP {response.StatusCode}");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0)
                {
                    return (false, "Downloaded file was empty (0 bytes).");
                }

                await File.WriteAllBytesAsync(targetFile, bytes);
                string installedSha = ComputeSha256(targetFile);

                MainWindow.Instance?.Log($"✅ Successfully downloaded real plugin DLL '{item.DllFileName}' ({bytes.Length / 1024.0:F1} KB, SHA256: {installedSha[..8]}...)");
                PluginManager.Instance.LoadPlugins();

                return (true, $"Successfully installed real DLL '{item.DllFileName}'.");
            }
            catch (Exception ex)
            {
                return (false, $"Failed to download real plugin DLL: {ex.Message}");
            }
        }

        public async Task<int> UpdateAllPluginsAsync()
        {
            int updated = 0;
            var catalog = await FetchCatalogFromGitHubAsync();
            foreach (var item in catalog.Where(i => i.IsUpdateAvailable))
            {
                var result = await DownloadAndInstallPluginAsync(item);
                if (result.Success) updated++;
            }
            return updated;
        }

        private string DeduceGameName(string filename)
        {
            if (filename.Contains("MHWs", StringComparison.OrdinalIgnoreCase)) return "Monster Hunter Wilds";
            if (filename.Contains("MHW", StringComparison.OrdinalIgnoreCase)) return "Monster Hunter: World";
            if (filename.Contains("Elden", StringComparison.OrdinalIgnoreCase)) return "Elden Ring";
            if (filename.Contains("Pal", StringComparison.OrdinalIgnoreCase)) return "Palworld";
            return "Universal Game";
        }

        private string DeduceGameId(string filename)
        {
            if (filename.Contains("MHWs", StringComparison.OrdinalIgnoreCase)) return "MHWs";
            if (filename.Contains("MHW", StringComparison.OrdinalIgnoreCase)) return "MHW";
            if (filename.Contains("Elden", StringComparison.OrdinalIgnoreCase)) return "EldenRing";
            if (filename.Contains("Pal", StringComparison.OrdinalIgnoreCase)) return "Palworld";
            return Path.GetFileNameWithoutExtension(filename);
        }

        private string ComputeSha256(string filePath)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                byte[] hash = sha.ComputeHash(stream);
                return Convert.ToHexString(hash);
            }
            catch
            {
                return "";
            }
        }
    }
}
