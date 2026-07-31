using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using ModTogether.Plugins.MHWilds.Models;

namespace ModTogether.Plugins.MHWilds.Services
{
    public class PakModInstaller
    {
        private readonly string _mhwDir;
        private readonly string _stateFile;
        private ModState _state;
        public ModState State => _state;

        public event Action<string>? OnLog;
        public event Action<double>? OnInstallProgress;

        public PakModInstaller(string mhwDir)
        {
            _mhwDir = mhwDir;
            _stateFile = Path.Combine(_mhwDir, "installed_mods.json");
            _state = LoadState();
        }

        private ModState LoadState()
        {
            if (File.Exists(_stateFile))
            {
                try
                {
                    var json = File.ReadAllText(_stateFile);
                    return JsonSerializer.Deserialize<ModState>(json) ?? new ModState();
                }
                catch
                {
                    return new ModState();
                }
            }
            return new ModState();
        }

        private void SaveState()
        {
            try
            {
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFile, json);
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[Error] Could not save state: {ex.Message}");
            }
        }

        public bool IsModInstalled(string archivePath)
        {
            return _state.InstalledMods.ContainsKey(archivePath);
        }

        private int GetNextPatchNumber()
        {
            int maxNumber = 0;
            if (Directory.Exists(_mhwDir))
            {
                var files = Directory.GetFiles(_mhwDir, "*.patch_*.pak");
                var regex = new Regex(@"patch_(\d{3})\.pak$", RegexOptions.IgnoreCase);
                foreach (var file in files)
                {
                    var match = regex.Match(Path.GetFileName(file));
                    if (match.Success)
                    {
                        if (int.TryParse(match.Groups[1].Value, out int num))
                        {
                            if (num > maxNumber) maxNumber = num;
                        }
                    }
                }
            }
            int next = maxNumber + 1;
            OnLog?.Invoke($"[DEBUG] Last patch number in game dir: patch_{maxNumber:D3}.pak | Next patch number: patch_{next:D3}.pak");
            return next;
        }

        private string ResolveBaseSearchDir(string rootDir, string subFolderPath)
        {
            if (string.IsNullOrEmpty(subFolderPath)) return rootDir;

            string path1 = Path.Combine(rootDir, subFolderPath.Replace('/', Path.DirectorySeparatorChar));
            if (Directory.Exists(path1)) return path1;

            string normalizedSub = subFolderPath.Trim('/', '\\').Replace('\\', '/');
            string[] subParts = normalizedSub.Split('/');
            string targetFolderName = subParts.Last();

            var matches = Directory.GetDirectories(rootDir, targetFolderName, SearchOption.AllDirectories);
            if (matches.Length > 0)
            {
                return matches[0];
            }

            var allDirs = Directory.GetDirectories(rootDir, "*", SearchOption.AllDirectories);
            foreach (var dir in allDirs)
            {
                string rel = dir.Substring(rootDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                if (rel.Equals(normalizedSub, StringComparison.OrdinalIgnoreCase) || rel.EndsWith("/" + targetFolderName, StringComparison.OrdinalIgnoreCase))
                {
                    return dir;
                }
            }

            return path1;
        }

        public void InstallMod(string archivePath, string relativeKey, string subFolderPath = "")
        {
            try
            {
                OnLog?.Invoke($"[INFO] Starting installation for mod key: {relativeKey}");
                OnInstallProgress?.Invoke(0);
                
                string ext = Path.GetExtension(archivePath).ToLower();
                var tempDir = Path.Combine(Path.GetTempPath(), "ModTogether", Guid.NewGuid().ToString());
                string baseSearchDir = archivePath;

                if (Directory.Exists(archivePath))
                {
                    baseSearchDir = ResolveBaseSearchDir(archivePath, subFolderPath);
                    OnLog?.Invoke($"[INFO] Mod source is directory: {baseSearchDir}");
                }
                else if (ext == ".zip" || ext == ".rar" || ext == ".7z")
                {
                    OnLog?.Invoke($"[INFO] Extracting archive '{Path.GetFileName(archivePath)}' to temp folder...");
                    ArchiveExtractor.ExtractArchive(archivePath, tempDir, pct => OnInstallProgress?.Invoke(pct * 0.5));
                    baseSearchDir = ResolveBaseSearchDir(tempDir, subFolderPath);
                    OnLog?.Invoke($"[INFO] Resolved target sub-folder dir: {baseSearchDir}");
                }
                else if (ext != ".pak")
                {
                    OnLog?.Invoke($"[❌] Unsupported file type for MHWilds mod: {archivePath}");
                    return;
                }

                var installedFiles = new List<string>();

                if (ext == ".pak")
                {
                    int nextNum = GetNextPatchNumber();
                    string newPakName = $"re_chunk_000.pak.patch_{nextNum:D3}.pak";
                    string destPak = Path.Combine(_mhwDir, newPakName);
                    OnLog?.Invoke($"[DEBUG] Copying .pak mod directly to: {newPakName}");
                    File.Copy(archivePath, destPak, true);
                    installedFiles.Add(newPakName);
                }
                else
                {
                    if (!Directory.Exists(baseSearchDir))
                    {
                        OnLog?.Invoke($"[❌] Sub-folder not found: {subFolderPath}");
                        if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                        return;
                    }

                    // 1. Process .pak files inside archive/folder
                    var paks = Directory.GetFiles(baseSearchDir, "*.pak", SearchOption.AllDirectories);
                    if (paks.Length > 0)
                    {
                        OnLog?.Invoke($"[INFO] Found {paks.Length} .pak file(s) inside mod package.");
                        for (int i = 0; i < paks.Length; i++)
                        {
                            int nextNum = GetNextPatchNumber();
                            string newPakName = $"re_chunk_000.pak.patch_{nextNum:D3}.pak";
                            string destPak = Path.Combine(_mhwDir, newPakName);
                            OnLog?.Invoke($"[INFO] Copying inner pak[{i}] '{Path.GetFileName(paks[i])}' -> '{newPakName}'");
                            File.Copy(paks[i], destPak, true);
                            installedFiles.Add(newPakName);
                        }
                    }
                    else
                    {
                        // 2. Process raw natives files by packing them into RE Engine .pak format
                        var allFiles = Directory.GetFiles(baseSearchDir, "*.*", SearchOption.AllDirectories);
                        bool hasNatives = allFiles.Any(f => f.IndexOf("natives", StringComparison.OrdinalIgnoreCase) >= 0);

                        if (hasNatives)
                        {
                            int nextNum = GetNextPatchNumber();
                            string newPakName = $"re_chunk_000.pak.patch_{nextNum:D3}.pak";
                            string destPak = Path.Combine(_mhwDir, newPakName);

                            OnLog?.Invoke($"[INFO] Raw 'natives' folder detected! Packing {allFiles.Length} file(s) into RE Engine .pak format -> '{newPakName}'");
                            RePakPacker.CreatePakFromDirectory(baseSearchDir, destPak);
                            installedFiles.Add(newPakName);
                        }
                        else
                        {
                            OnLog?.Invoke($"[❌] No .pak files or 'natives' directory structure found in {baseSearchDir}");
                        }
                    }
                }

                if (installedFiles.Count == 0)
                {
                    OnLog?.Invoke($"[❌] No .pak or natives files found in {relativeKey}");
                    if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                    return;
                }

                _state.InstalledMods[relativeKey] = installedFiles;
                SaveState();

                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
                
                OnInstallProgress?.Invoke(100);
                OnLog?.Invoke($"[✅ SUCCESS] Installed '{relativeKey}' as -> [{string.Join(", ", installedFiles)}]");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[❌] Failed to install {relativeKey}: {ex.Message}");
            }
        }

        public void UninstallMod(string relativeKey)
        {
            if (!_state.InstalledMods.ContainsKey(relativeKey)) return;

            OnLog?.Invoke($"[DEBUG] Uninstalling mod: {relativeKey}");
            var filesToRemove = _state.InstalledMods[relativeKey];
            
            foreach (var pakName in filesToRemove)
            {
                var fullPath = Path.Combine(_mhwDir, pakName);
                if (File.Exists(fullPath))
                {
                    try 
                    { 
                        File.Delete(fullPath); 
                        OnLog?.Invoke($"[DEBUG] Removed patch file: {pakName}");
                    } 
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[DEBUG] Failed to delete file {pakName}: {ex.Message}");
                    }
                }
            }

            _state.InstalledMods.Remove(relativeKey);
            
            // Re-sequence
            OnLog?.Invoke($"[DEBUG] Triggering gap-closure re-sequencing for patch files...");
            ResequencePatchFiles();
            SaveState();

            OnLog?.Invoke($"[✅ SUCCESS] Uninstalled '{relativeKey}'");
        }
        
        private void ResequencePatchFiles()
        {
            var files = Directory.GetFiles(_mhwDir, "re_chunk_000.pak.patch_*.pak");
            var regex = new Regex(@"re_chunk_000\.pak\.patch_(\d{3})\.pak", RegexOptions.IgnoreCase);
            
            var patchFiles = new List<(string Path, int Number, string Name)>();
            
            foreach (var file in files)
            {
                var match = regex.Match(Path.GetFileName(file));
                if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                {
                    patchFiles.Add((file, num, Path.GetFileName(file)));
                }
            }
            
            patchFiles = patchFiles.OrderBy(p => p.Number).ToList();
            if (patchFiles.Count == 0) return;
            
            // If patch_000.pak exists and is NOT in _state.InstalledMods, it's an official game file (keep at 0).
            bool hasOfficialPatch0 = patchFiles.Any(p => p.Number == 0) && !_state.InstalledMods.Values.Any(list => list.Contains("re_chunk_000.pak.patch_000.pak"));
            
            int currentNum = hasOfficialPatch0 ? 0 : 1;
            
            foreach (var pf in patchFiles)
            {
                if (pf.Number == 0 && hasOfficialPatch0)
                {
                    currentNum = 1;
                    continue;
                }

                if (pf.Number != currentNum)
                {
                    string oldName = pf.Name;
                    string newName = $"re_chunk_000.pak.patch_{currentNum:D3}.pak";
                    string newPath = Path.Combine(_mhwDir, newName);
                    
                    try 
                    {
                        ModTogether.API.FileHelper.SafeMove(pf.Path, newPath);
                        OnLog?.Invoke($"[DEBUG] Re-sequenced gap: Renamed '{oldName}' -> '{newName}'");
                        
                        foreach (var kvp in _state.InstalledMods)
                        {
                            for (int i = 0; i < kvp.Value.Count; i++)
                            {
                                if (kvp.Value[i] == oldName)
                                {
                                    kvp.Value[i] = newName;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        OnLog?.Invoke($"[Error] Failed to rename {oldName} to {newName}: {ex.Message}");
                    }
                }
                currentNum++;
            }
        }

        public int GetModPriority(string relativeKey)
        {
            if (!_state.InstalledMods.TryGetValue(relativeKey, out var files) || files.Count == 0)
                return -1;

            var regex = new Regex(@"patch_(\d{3})\.pak$", RegexOptions.IgnoreCase);
            int minNum = int.MaxValue;
            foreach (var f in files)
            {
                var match = regex.Match(f);
                if (match.Success && int.TryParse(match.Groups[1].Value, out int num))
                {
                    if (num < minNum) minNum = num;
                }
            }

            return minNum == int.MaxValue ? -1 : minNum;
        }

        public bool MoveModPriority(string relativeKey, int direction) // -1 for Up (lower patch num), +1 for Down (higher patch num)
        {
            if (!_state.InstalledMods.ContainsKey(relativeKey)) return false;

            var installedList = _state.InstalledMods.Keys.ToList();
            var sortedByPriority = installedList
                .Select(k => (Key: k, Priority: GetModPriority(k)))
                .Where(p => p.Priority >= 0)
                .OrderBy(p => p.Priority)
                .ToList();

            int idx = sortedByPriority.FindIndex(p => p.Key == relativeKey);
            if (idx == -1) return false;

            int targetIdx = idx + direction;
            if (targetIdx < 0 || targetIdx >= sortedByPriority.Count) return false;

            var currentMod = sortedByPriority[idx];
            var targetMod = sortedByPriority[targetIdx];

            // Swap patch files between currentMod and targetMod
            var currentFiles = _state.InstalledMods[currentMod.Key].ToList();
            var targetFiles = _state.InstalledMods[targetMod.Key].ToList();

            // Temporary rename current files to avoid name collisions
            var tempCurrentFiles = new List<string>();
            foreach (var file in currentFiles)
            {
                string oldPath = Path.Combine(_mhwDir, file);
                string tempName = file + ".tmp";
                string tempPath = Path.Combine(_mhwDir, tempName);
                if (File.Exists(oldPath))
                {
                    ModTogether.API.FileHelper.SafeMove(oldPath, tempPath);
                    tempCurrentFiles.Add(tempName);
                }
            }

            // Move target files to current files' names
            var newTargetFiles = new List<string>();
            for (int i = 0; i < targetFiles.Count && i < currentFiles.Count; i++)
            {
                string targetPath = Path.Combine(_mhwDir, targetFiles[i]);
                string newPath = Path.Combine(_mhwDir, currentFiles[i]);
                if (File.Exists(targetPath))
                {
                    ModTogether.API.FileHelper.SafeMove(targetPath, newPath);
                    newTargetFiles.Add(currentFiles[i]);
                }
            }

            // Move temp current files to target files' names
            var newCurrentFiles = new List<string>();
            for (int i = 0; i < tempCurrentFiles.Count && i < targetFiles.Count; i++)
            {
                string tempPath = Path.Combine(_mhwDir, tempCurrentFiles[i]);
                string newPath = Path.Combine(_mhwDir, targetFiles[i]);
                if (File.Exists(tempPath))
                {
                    ModTogether.API.FileHelper.SafeMove(tempPath, newPath);
                    newCurrentFiles.Add(targetFiles[i]);
                }
            }

            _state.InstalledMods[currentMod.Key] = newCurrentFiles;
            _state.InstalledMods[targetMod.Key] = newTargetFiles;

            SaveState();
            OnLog?.Invoke($"[INFO] Swapped load priority between '{currentMod.Key}' and '{targetMod.Key}'");
            return true;
        }

        public bool AutoDetectInstalledMods(string cacheDir)
        {
            return false;
        }
    }
}
