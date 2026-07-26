using System;
using System.IO;
using System.Text.Json;
using ModTogether.Plugins.MHWilds.Models;

namespace ModTogether.Plugins.MHWilds.Services
{
    public static class BackupManager
    {
        public static bool CreateBackup(string mhwDir, string backupPath)
        {
            try
            {
                string stateFile = Path.Combine(mhwDir, "installed_mods.json");
                if (!File.Exists(stateFile))
                    return false;

                Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
                File.Copy(stateFile, backupPath, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool RestoreBackup(string mhwDir, string backupPath)
        {
            try
            {
                if (!File.Exists(backupPath))
                    return false;

                string stateFile = Path.Combine(mhwDir, "installed_mods.json");
                File.Copy(backupPath, stateFile, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
