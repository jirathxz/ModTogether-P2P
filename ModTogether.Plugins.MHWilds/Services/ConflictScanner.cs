using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModTogether.Plugins.MHWilds.Services
{
    public class ConflictInfo
    {
        public string ModKey { get; set; } = string.Empty;
        public List<string> ConflictingMods { get; set; } = new();
        public List<string> ConflictingFiles { get; set; } = new();
    }

    public static class ConflictScanner
    {
        public static Dictionary<string, ConflictInfo> ScanConflicts(string gameModsDir)
        {
            var result = new Dictionary<string, ConflictInfo>(StringComparer.OrdinalIgnoreCase);

            if (!Directory.Exists(gameModsDir))
                return result;

            var modFileMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var fileToModsMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var entries = Directory.GetFileSystemEntries(gameModsDir);

            foreach (var entry in entries)
            {
                string modKey = Path.GetFileName(entry);
                bool isDir = Directory.Exists(entry);
                string ext = isDir ? "" : Path.GetExtension(entry).ToLower();

                List<string> fileList = new();

                try
                {
                    if (isDir)
                    {
                        var files = Directory.GetFiles(entry, "*.*", SearchOption.AllDirectories);
                        foreach (var f in files)
                        {
                            string rel = f.Substring(entry.Length).TrimStart('\\', '/').Replace('\\', '/');
                            int idx = rel.IndexOf("natives", StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                fileList.Add(rel.Substring(idx).ToLower());
                            }
                        }
                    }
                    else if (ext == ".zip" || ext == ".7z" || ext == ".rar")
                    {
                        var contents = ArchiveExtractor.GetArchiveContents(entry);
                        foreach (var rel in contents)
                        {
                            int idx = rel.IndexOf("natives", StringComparison.OrdinalIgnoreCase);
                            if (idx >= 0)
                            {
                                fileList.Add(rel.Substring(idx).ToLower());
                            }
                        }
                    }
                }
                catch
                {
                    // Ignore corrupted archives
                }

                fileList = fileList.Distinct().ToList();
                modFileMap[modKey] = fileList;

                foreach (var file in fileList)
                {
                    if (!fileToModsMap.ContainsKey(file))
                        fileToModsMap[file] = new List<string>();
                    fileToModsMap[file].Add(modKey);
                }
            }

            foreach (var (modKey, files) in modFileMap)
            {
                var conflictObj = new ConflictInfo { ModKey = modKey };
                var conflictingMods = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var conflictingFiles = new List<string>();

                foreach (var file in files)
                {
                    if (fileToModsMap.TryGetValue(file, out var mods) && mods.Count > 1)
                    {
                        conflictingFiles.Add(file);
                        foreach (var otherMod in mods)
                        {
                            if (!otherMod.Equals(modKey, StringComparison.OrdinalIgnoreCase))
                            {
                                conflictingMods.Add(otherMod);
                            }
                        }
                    }
                }

                if (conflictingMods.Count > 0)
                {
                    conflictObj.ConflictingMods = conflictingMods.ToList();
                    conflictObj.ConflictingFiles = conflictingFiles;
                    result[modKey] = conflictObj;
                }
            }

            return result;
        }
    }
}
