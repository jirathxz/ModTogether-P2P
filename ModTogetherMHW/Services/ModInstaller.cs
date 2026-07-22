using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using ModTogetherMHW.Models;

namespace ModTogetherMHW.Services
{
    public class ModInstaller
    {
        private readonly string _mhwDir;
        private readonly string _nativePcDir;
        private readonly string _stateFile;
        private ModState _state;
        public ModState State => _state;

        public event Action<string>? OnLog;
        public event Action<double>? OnInstallProgress;

        public ModInstaller(string mhwDir)
        {
            _mhwDir = mhwDir;
            _nativePcDir = Path.Combine(_mhwDir, "nativePC");
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

        public void InstallMod(string archivePath, string relativeKey, List<string>? selectedFiles = null)
        {
            try
            {
                OnLog?.Invoke($"Extracting {relativeKey}...");
                
                // Unzip to temp folder
                var tempDir = Path.Combine(Path.GetTempPath(), "ModTogether", Guid.NewGuid().ToString());
                ArchiveExtractor.ExtractArchive(archivePath, tempDir, pct => OnInstallProgress?.Invoke(pct));

                var installedFiles = new List<string>();
                Directory.CreateDirectory(_nativePcDir);

                foreach (var file in Directory.GetFiles(tempDir, "*.*", SearchOption.AllDirectories))
                {
                    var archiveKey = Path.GetRelativePath(tempDir, file).Replace('\\', '/');
                    
                    if (selectedFiles != null && selectedFiles.Count > 0)
                    {
                        bool isMatch = selectedFiles.Any(sf => 
                        {
                            string normSf = sf.Replace('\\', '/').TrimEnd('/');
                            return string.IsNullOrEmpty(normSf) ||
                                   archiveKey.Equals(normSf, StringComparison.OrdinalIgnoreCase) ||
                                   archiveKey.StartsWith(normSf + "/", StringComparison.OrdinalIgnoreCase);
                        });

                        if (!isMatch) continue; // Skip files that are not selected by user
                    }

                    // Determine destination by finding "nativePC" or a valid MHW folder in the path
                    string relDestPath = archiveKey;
                    string[] parts = archiveKey.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                    
                    int mhwFolderIndex = -1;
                    for (int i = 0; i < parts.Length; i++)
                    {
                        if (parts[i].Equals("nativePC", StringComparison.OrdinalIgnoreCase) || IsMhwFolder(parts[i]))
                        {
                            mhwFolderIndex = i;
                            break;
                        }
                    }

                    if (mhwFolderIndex < 0) continue; // Skip files that don't belong to the MHW folder structure

                    if (parts[mhwFolderIndex].Equals("nativePC", StringComparison.OrdinalIgnoreCase))
                    {
                        relDestPath = string.Join("/", parts.Skip(mhwFolderIndex + 1));
                    }
                    else
                    {
                        relDestPath = string.Join("/", parts.Skip(mhwFolderIndex));
                    }
                    
                    if (string.IsNullOrEmpty(relDestPath)) continue; // Directory entry itself

                    var destFile = Path.Combine(_nativePcDir, relDestPath.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                    File.Copy(file, destFile, true);
                    installedFiles.Add(relDestPath.Replace('\\', '/'));
                }

                _state.InstalledMods[relativeKey] = installedFiles;
                SaveState();

                Directory.Delete(tempDir, true);
                OnLog?.Invoke($"[✅] Installed {relativeKey}");
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"[❌] Failed to install {relativeKey}: {ex.Message}");
            }
        }

        public void UninstallMod(string relativeKey)
        {
            if (!_state.InstalledMods.ContainsKey(relativeKey)) return;

            var filesToRemove = _state.InstalledMods[relativeKey];
            foreach (var relFile in filesToRemove)
            {
                var fullPath = Path.Combine(_nativePcDir, relFile.Replace('/', '\\'));
                if (File.Exists(fullPath))
                {
                    try { File.Delete(fullPath); } catch { }
                }
            }

            _state.InstalledMods.Remove(relativeKey);
            SaveState();

            // Cleanup empty directories
            CleanupEmptyDirectories(_nativePcDir);

            OnLog?.Invoke($"[✅] Uninstalled {relativeKey}");
        }

        private void CleanupEmptyDirectories(string startLocation)
        {
            foreach (var directory in Directory.GetDirectories(startLocation))
            {
                CleanupEmptyDirectories(directory);
                if (Directory.GetFiles(directory).Length == 0 && Directory.GetDirectories(directory).Length == 0)
                {
                    try { Directory.Delete(directory, false); } catch { }
                }
            }
        }

        private static bool IsMhwFolder(string folderName)
        {
            string[] mhwFolders = { "sound", "wp", "vfx", "stage", "art", "ui", "pl", "hm", "em", "facility", "gimmick", "collision", "shader", "ot", "item", "bg", "quest", "ev", "common" };
            return mhwFolders.Any(f => f.Equals(folderName, StringComparison.OrdinalIgnoreCase));
        }

        public bool AutoDetectInstalledMods(string cacheDir)
        {
            if (!Directory.Exists(cacheDir) || !Directory.Exists(_nativePcDir)) return false;
            
            bool changes = false;
            var files = Directory.GetFiles(cacheDir);

            foreach (var file in files)
            {
                string ext = Path.GetExtension(file).ToLower();
                if (ext == ".zip" || ext == ".7z" || ext == ".rar")
                {
                    string filename = Path.GetFileName(file);
                    if (!_state.InstalledMods.ContainsKey(filename))
                    {
                        try
                        {
                            var contents = ArchiveExtractor.GetArchiveContents(file);
                            var nativeFiles = new List<string>();
                            bool allExist = true;

                            foreach (var rawEntry in contents)
                            {
                                string entry = rawEntry.Replace('\\', '/');
                                string[] parts = entry.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                                
                                int mhwFolderIndex = -1;
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (parts[i].Equals("nativePC", StringComparison.OrdinalIgnoreCase) || IsMhwFolder(parts[i]))
                                    {
                                        mhwFolderIndex = i;
                                        break;
                                    }
                                }

                                if (mhwFolderIndex < 0) continue;

                                string relPath;
                                if (parts[mhwFolderIndex].Equals("nativePC", StringComparison.OrdinalIgnoreCase))
                                {
                                    relPath = string.Join("/", parts.Skip(mhwFolderIndex + 1));
                                }
                                else
                                {
                                    relPath = string.Join("/", parts.Skip(mhwFolderIndex));
                                }
                                
                                if (string.IsNullOrEmpty(relPath)) continue;

                                string target = Path.Combine(_nativePcDir, relPath.Replace('/', Path.DirectorySeparatorChar));
                                if (!File.Exists(target))
                                {
                                    allExist = false;
                                    break;
                                }
                                nativeFiles.Add(relPath.Replace('\\', '/'));
                            }

                            if (allExist && nativeFiles.Count > 0)
                            {
                                _state.InstalledMods[filename] = nativeFiles;
                                changes = true;
                            }
                        }
                        catch
                        {
                            // Skip invalid or read-locked archives
                        }
                    }
                }
            }

            if (changes)
            {
                SaveState();
            }
            return changes;
        }
    }
}
