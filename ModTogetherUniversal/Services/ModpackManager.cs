using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModTogetherUniversal.Services
{
    public class ModpackProfile
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> EnabledMods { get; set; } = new();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ModpackManager
    {
        private static ModpackManager? _instance;
        public static ModpackManager Instance => _instance ??= new ModpackManager();

        public string GetPresetsRootPath()
        {
            string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string dir = Path.Combine(docsPath, "ModTogether", "Presets");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public string GetAllModsPath()
        {
            string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string stashPath = Path.Combine(docsPath, "ModTogether", ".stash");
            string allModsPath = Path.Combine(docsPath, "ModTogether", "AllMods");
            
            if (Directory.Exists(stashPath) && !Directory.Exists(allModsPath))
            {
                try { Directory.Move(stashPath, allModsPath); } catch { }
            }
            return allModsPath;
        }

        public void SaveOriginalMods(string activeModsDir)
        {
            if (string.IsNullOrWhiteSpace(activeModsDir) || !Directory.Exists(activeModsDir)) return;
            string allModsDir = GetAllModsPath();
            ClearDirectory(allModsDir);
            Directory.CreateDirectory(allModsDir);
            CopyDirectory(activeModsDir, allModsDir);
        }

        public string GetPresetDirectory(string name)
        {
            string safeName = string.Join("_", name.Trim().Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(GetPresetsRootPath(), safeName);
        }

        public List<string> GetPresetNames()
        {
            var list = new List<string>();
            string root = GetPresetsRootPath();
            if (Directory.Exists(root))
            {
                foreach (var dir in Directory.GetDirectories(root))
                {
                    list.Add(Path.GetFileName(dir));
                }
            }
            return list.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        public List<ModpackProfile> GetProfiles()
        {
            return GetPresetNames().Select(n => new ModpackProfile { Name = n }).ToList();
        }

        public ModpackProfile? GetProfile(string name) =>
            GetProfiles().FirstOrDefault(profile => string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase));

        public void SavePreset(string name, string activeModsDir)
        {
            name = name?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("A preset name is required.", nameof(name));

            string presetDir = GetPresetDirectory(name);
            if (Directory.Exists(presetDir))
            {
                Directory.Delete(presetDir, true);
            }
            Directory.CreateDirectory(presetDir);

            if (Directory.Exists(activeModsDir))
            {
                CopyDirectory(activeModsDir, presetDir);
            }
        }

        public bool LoadPreset(string name, string activeModsDir)
        {
            string presetDir = GetPresetDirectory(name);
            if (!Directory.Exists(presetDir)) return false;

            if (string.IsNullOrWhiteSpace(activeModsDir)) return false;
            Directory.CreateDirectory(activeModsDir);

            ClearDirectory(activeModsDir);

            CopyDirectory(presetDir, activeModsDir);
            return true;
        }

        public bool RestoreOriginalMods(string activeModsDir)
        {
            string allModsDir = GetAllModsPath();
            if (!Directory.Exists(allModsDir)) return false;

            if (string.IsNullOrWhiteSpace(activeModsDir)) return false;
            Directory.CreateDirectory(activeModsDir);

            ClearDirectory(activeModsDir);

            CopyDirectory(allModsDir, activeModsDir);

            return true;
        }

        public void DeletePreset(string name)
        {
            string presetDir = GetPresetDirectory(name);
            if (Directory.Exists(presetDir))
            {
                Directory.Delete(presetDir, true);
            }
        }

        public void DeleteProfile(string name) => DeletePreset(name);

        public void SaveProfile(string name, string description, List<string> enabledMods)
        {
            string activeModsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ModTogether", "ActiveMods");
            SavePreset(name, activeModsDir);
        }

        public bool ApplyProfile(string profileName, string modsSourceDir, string activeTargetDir)
        {
            return LoadPreset(profileName, activeTargetDir);
        }

        public void CopyDirectory(string sourceDir, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                string relPath = Path.GetRelativePath(sourceDir, file);
                string destFile = Path.Combine(destinationDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
                File.Copy(file, destFile, true);
            }
        }

        private static void ClearDirectory(string dir)
        {
            if (!Directory.Exists(dir)) return;

            foreach (string file in Directory.GetFiles(dir))
            {
                try { File.Delete(file); } catch { }
            }

            foreach (string subDir in Directory.GetDirectories(dir))
            {
                try { Directory.Delete(subDir, true); } catch { }
            }
        }
    }
}
