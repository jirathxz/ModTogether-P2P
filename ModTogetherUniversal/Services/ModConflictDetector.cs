using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace ModTogetherUniversal.Services
{
    public class ConflictItem
    {
        public string InternalFilePath { get; set; } = string.Empty;
        public List<string> ConflictingModFiles { get; set; } = new();
        public string WinningModFile { get; set; } = string.Empty;
    }

    public class ConflictScanResult
    {
        public bool HasConflicts => ConflictList.Count > 0;
        public List<ConflictItem> ConflictList { get; set; } = new();
        public HashSet<string> ConflictedModFilenames { get; set; } = new();
        public Dictionary<string, int> ModPriorities { get; set; } = new();
    }

    public static class ModConflictDetector
    {
        private static readonly Dictionary<string, List<string>> _archiveContentsCache = new();

        public static List<string> GetArchiveInternalFiles(string archivePath)
        {
            if (_archiveContentsCache.TryGetValue(archivePath, out var cached))
            {
                if (File.Exists(archivePath))
                {
                    var lastWrite = File.GetLastWriteTime(archivePath);
                    return cached;
                }
            }

            var result = new List<string>();
            try
            {
                string ext = Path.GetExtension(archivePath).ToLowerInvariant();
                if (ext == ".zip")
                {
                    using (var archive = ZipFile.OpenRead(archivePath))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            if (string.IsNullOrEmpty(entry.Name)) continue; // Directory
                            string norm = entry.FullName.Replace('\\', '/').TrimStart('/');
                            result.Add(norm);
                        }
                    }
                }
                else
                {
                    // Fallback to SharpCompress or basic reading
                    using (var archive = SharpCompress.Archives.ArchiveFactory.Open(archivePath))
                    {
                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            if (!string.IsNullOrEmpty(entry.Key))
                            {
                                string norm = entry.Key.Replace('\\', '/').TrimStart('/');
                                result.Add(norm);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ModConflictDetector Error] Reading {archivePath}: {ex.Message}");
            }

            _archiveContentsCache[archivePath] = result;
            return result;
        }

        public static ConflictScanResult AnalyzeConflicts(string modsDirectory, List<string> activeModFilenames, Dictionary<string, int>? priorities = null)
        {
            priorities ??= LoadPriorities(modsDirectory);

            var result = new ConflictScanResult
            {
                ModPriorities = priorities
            };

            // Map: NormalizedInternalPath -> List of (ModFilename, Priority)
            var fileOwners = new Dictionary<string, List<(string ModFile, int Priority)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var modFile in activeModFilenames)
            {
                string fullPath = Path.Combine(modsDirectory, modFile);
                if (!File.Exists(fullPath)) continue;

                int priority = priorities.TryGetValue(modFile, out var p) ? p : 0;
                var internalFiles = GetArchiveInternalFiles(fullPath);

                foreach (var relFile in internalFiles)
                {
                    // Clean path for standard MHW or generic mod structure
                    string cleanPath = NormalizeModInternalPath(relFile);
                    if (string.IsNullOrEmpty(cleanPath)) continue;

                    if (!fileOwners.TryGetValue(cleanPath, out var owners))
                    {
                        owners = new List<(string ModFile, int Priority)>();
                        fileOwners[cleanPath] = owners;
                    }
                    owners.Add((modFile, priority));
                }
            }

            // Identify conflicts where > 1 mod owns the same file
            foreach (var kvp in fileOwners)
            {
                if (kvp.Value.Count > 1)
                {
                    var conflictingMods = kvp.Value.Select(x => x.ModFile).Distinct().ToList();
                    
                    // Winner is the mod with highest priority score (or last in list if tied)
                    var winner = kvp.Value.OrderByDescending(x => x.Priority).ThenBy(x => x.ModFile).First().ModFile;

                    result.ConflictList.Add(new ConflictItem
                    {
                        InternalFilePath = kvp.Key,
                        ConflictingModFiles = conflictingMods,
                        WinningModFile = winner
                    });

                    foreach (var mod in conflictingMods)
                    {
                        result.ConflictedModFilenames.Add(mod);
                    }
                }
            }

            return result;
        }

        private static string NormalizeModInternalPath(string rawPath)
        {
            string norm = rawPath.Replace('\\', '/');
            int idx = norm.IndexOf("nativePC/", StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                return norm.Substring(idx);
            }
            return norm;
        }

        public static Dictionary<string, int> LoadPriorities(string modsDirectory)
        {
            string path = Path.Combine(modsDirectory, "mod_priorities.json");
            if (File.Exists(path))
            {
                try
                {
                    string json = File.ReadAllText(path);
                    return JsonSerializer.Deserialize<Dictionary<string, int>>(json) ?? new();
                }
                catch { }
            }
            return new Dictionary<string, int>();
        }

        public static void SavePriorities(string modsDirectory, Dictionary<string, int> priorities)
        {
            try
            {
                Directory.CreateDirectory(modsDirectory);
                string path = Path.Combine(modsDirectory, "mod_priorities.json");
                string json = JsonSerializer.Serialize(priorities, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(path, json);
            }
            catch { }
        }
    }
}
