using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using ModTogether.Plugins.MHW.Services;

namespace ModTogether.Tests
{
    public static class TestMhwModInstaller
    {
        public static bool RunTest()
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine(" [TEST] MHW Mod Installer & Progress Event Verification");
            Console.WriteLine("=========================================================");

            string testRoot = Path.Combine(Path.GetTempPath(), "ModTogether_MhwTest_" + Guid.NewGuid().ToString().Substring(0, 6));
            string mockGameDir = Path.Combine(testRoot, "MHWGame");
            string mockModsCache = Path.Combine(testRoot, "GameMods");

            Directory.CreateDirectory(mockGameDir);
            Directory.CreateDirectory(mockModsCache);

            var installer = new ModInstaller(mockGameDir);
            var progressLogs = new List<double>();
            
            installer.OnLog += msg => Console.WriteLine($"[MHW Log] {msg}");
            installer.OnInstallProgress += pct => progressLogs.Add(pct);

            // Create a test zip file containing nativePC files
            string zipPath = Path.Combine(mockModsCache, "test_mhw_mod.zip");
            using (var zipArchive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zipArchive.CreateEntry("nativePC/sound/weapon/test.nbnw");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("MOCK SOUND DATA");
            }

            installer.InstallMod(zipPath, "test_mhw_mod.zip");

            bool isInstalled = installer.IsModInstalled("test_mhw_mod.zip");
            bool destFileExists = File.Exists(Path.Combine(mockGameDir, "nativePC", "sound", "weapon", "test.nbnw"));
            bool hasStartProgress = progressLogs.Contains(0);
            bool hasEndProgress = progressLogs.Contains(100);

            Console.WriteLine($"Progress events count: {progressLogs.Count}");
            Console.WriteLine($"[CHECKLIST 1] Mod registered as installed: {(isInstalled ? "PASSED ✅" : "FAILED ❌")}");
            Console.WriteLine($"[CHECKLIST 2] File extracted to nativePC: {(destFileExists ? "PASSED ✅" : "FAILED ❌")}");
            Console.WriteLine($"[CHECKLIST 3] OnInstallProgress fired 0%: {(hasStartProgress ? "PASSED ✅" : "FAILED ❌")}");
            Console.WriteLine($"[CHECKLIST 4] OnInstallProgress fired 100%: {(hasEndProgress ? "PASSED ✅" : "FAILED ❌")}");

            // Test Uninstallation
            Console.WriteLine("\n[ACTION] Testing Uninstallation...");
            installer.UninstallMod("test_mhw_mod.zip");

            bool isInstalledAfterUninstall = installer.IsModInstalled("test_mhw_mod.zip");
            bool destFileExistsAfterUninstall = File.Exists(Path.Combine(mockGameDir, "nativePC", "sound", "weapon", "test.nbnw"));

            Console.WriteLine($"[CHECKLIST 5] Mod unregistered from installed_mods.json: {(!isInstalledAfterUninstall ? "PASSED ✅" : "FAILED ❌")}");
            Console.WriteLine($"[CHECKLIST 6] File removed from nativePC: {(!destFileExistsAfterUninstall ? "PASSED ✅" : "FAILED ❌")}");

            try { Directory.Delete(testRoot, true); } catch { }

            bool passed = isInstalled && destFileExists && hasStartProgress && hasEndProgress && !isInstalledAfterUninstall && !destFileExistsAfterUninstall;
            Console.WriteLine("\n=========================================================");
            Console.WriteLine(passed ? " ✅ ALL MHW INSTALLER PROGRESS TESTS PASSED! " : " ❌ MHW INSTALLER TEST FAILED! ");
            Console.WriteLine("=========================================================");
            return passed;
        }
    }
}
