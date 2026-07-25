using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;

namespace ModTogetherUniversal.Services
{
    public class ModOwnerMetadata
    {
        public string RelativePath { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public long FileSizeBytes { get; set; }
        public List<string> Owners { get; set; } = new();
    }

    public class RecycleManager
    {
        private static RecycleManager? _instance;
        public static RecycleManager Instance => _instance ??= new RecycleManager();

        private readonly string _metadataFilePath;
        public Dictionary<string, ModOwnerMetadata> Registry { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public RecycleManager()
        {
            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var modDir = Path.Combine(docsPath, "ModTogether");
            Directory.CreateDirectory(modDir);
            _metadataFilePath = Path.Combine(modDir, "mod_owners.json");
            LoadRegistry();
        }

        private void LoadRegistry()
        {
            try
            {
                if (File.Exists(_metadataFilePath))
                {
                    var json = File.ReadAllText(_metadataFilePath);
                    var items = JsonSerializer.Deserialize<List<ModOwnerMetadata>>(json);
                    if (items != null)
                    {
                        Registry = items.ToDictionary(i => i.RelativePath, i => i, StringComparer.OrdinalIgnoreCase);
                    }
                }
            }
            catch { }
        }

        public void SaveRegistry()
        {
            try
            {
                var list = Registry.Values.ToList();
                var json = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_metadataFilePath, json);
            }
            catch { }
        }

        public void RegisterOwner(string relPath, string username, string sha256 = "", long size = 0)
        {
            if (string.IsNullOrWhiteSpace(relPath) || string.IsNullOrWhiteSpace(username)) return;

            if (!Registry.TryGetValue(relPath, out var meta))
            {
                meta = new ModOwnerMetadata
                {
                    RelativePath = relPath,
                    Sha256 = sha256,
                    FileSizeBytes = size
                };
                Registry[relPath] = meta;
            }

            if (!meta.Owners.Contains(username, StringComparer.OrdinalIgnoreCase))
            {
                meta.Owners.Add(username);
            }
            if (!string.IsNullOrEmpty(sha256)) meta.Sha256 = sha256;
            if (size > 0) meta.FileSizeBytes = size;

            SaveRegistry();
        }

        public List<string> GetOwners(string relPath)
        {
            if (Registry.TryGetValue(relPath, out var meta))
            {
                return meta.Owners;
            }
            return new List<string>();
        }

        public string GetOwnersBadgeText(string relPath)
        {
            var owners = GetOwners(relPath);
            if (owners.Count == 0) return "";
            if (owners.Count == 1) return $"👤 {owners[0]}";
            return $"👥 Shared ({string.Join(", ", owners)})";
        }

        public bool TryRestoreFromRecycle(string activeModsDir, string relPath, string expectedSha256)
        {
            try
            {
                var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                var recycleDir = Path.Combine(docsPath, "ModTogether", ".recycle_mods");
                string recycledFile = Path.Combine(recycleDir, relPath);

                if (!File.Exists(recycledFile)) return false;

                string localSha = ComputeSha256(recycledFile);
                if (!string.IsNullOrEmpty(expectedSha256) && !string.Equals(localSha, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    MainWindow.Instance?.Log($"⚠️ Recycled file '{relPath}' SHA mismatch (Corrupted/Modified). Skipping restore.");
                    return false;
                }

                string targetPath = Path.Combine(activeModsDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                File.Copy(recycledFile, targetPath, true);
                MainWindow.Instance?.Log($"♻️ Restored '{relPath}' directly from .recycle_mods cache (SHA256 verified, saved P2P bandwidth!)");
                return true;
            }
            catch (Exception ex)
            {
                MainWindow.Instance?.Log($"⚠️ Recycle restore error: {ex.Message}");
                return false;
            }
        }

        public void HandleUserDisconnect(string username, string activeModsDir)
        {
            if (string.IsNullOrWhiteSpace(username)) return;

            var docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            var recycleDir = Path.Combine(docsPath, "ModTogether", ".recycle_mods");
            Directory.CreateDirectory(recycleDir);

            int recycledCount = 0;
            var toRemove = new List<string>();

            foreach (var kvp in Registry.ToList())
            {
                var meta = kvp.Value;
                meta.Owners.RemoveAll(o => string.Equals(o, username, StringComparison.OrdinalIgnoreCase));

                if (meta.Owners.Count == 0)
                {
                    string activeFile = Path.Combine(activeModsDir, meta.RelativePath);
                    if (File.Exists(activeFile))
                    {
                        try
                        {
                            string targetRecyclePath = Path.Combine(recycleDir, meta.RelativePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(targetRecyclePath)!);
                            
                            if (File.Exists(targetRecyclePath)) File.Delete(targetRecyclePath);
                            File.Move(activeFile, targetRecyclePath);
                            recycledCount++;
                        }
                        catch { }
                    }
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var key in toRemove)
            {
                Registry.Remove(key);
            }

            SaveRegistry();
            if (recycledCount > 0)
            {
                MainWindow.Instance?.Log($"♻️ User '{username}' left session. Moved {recycledCount} unreferenced mod file(s) to .recycle_mods.");
            }
        }

        private string ComputeSha256(string filePath)
        {
            try
            {
                using var sha = SHA256.Create();
                using var stream = File.OpenRead(filePath);
                return Convert.ToHexString(sha.ComputeHash(stream));
            }
            catch { return ""; }
        }
    }
}
