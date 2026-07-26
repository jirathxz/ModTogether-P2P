using System;
using System.IO;
using System.Linq;
using ModTogether.Plugins.MHWilds.Services;

namespace ModTogether.Tests
{
    public static class TestMhWildsPakInstaller
    {
        public static void RunTest()
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine(" [TEST] MH Wilds RE Engine PAK Installer Verification");
            Console.WriteLine("=========================================================");

            string testRoot = Path.Combine(Path.GetTempPath(), "ModTogether_MhWildsTest_" + Guid.NewGuid().ToString().Substring(0, 6));
            string mockGameDir = Path.Combine(testRoot, "MHWildsGame");
            string mockModsCache = Path.Combine(testRoot, "GameMods");

            Directory.CreateDirectory(mockGameDir);
            Directory.CreateDirectory(mockModsCache);

            // Step 0: Setup initial game directory state with official patch_000.pak
            File.WriteAllText(Path.Combine(mockGameDir, "re_chunk_000.pak"), "BASE GAME PAK CONTENT");
            File.WriteAllText(Path.Combine(mockGameDir, "re_chunk_000.pak.patch_000.pak"), "OFFICIAL PATCH 000");

            var installer = new PakModInstaller(mockGameDir);
            installer.OnLog += msg => Console.WriteLine(msg);

            // ----------------------------------------------------
            // TEST 1: Install Pre-packed .pak Mod (ModA)
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 1: Installing Pre-packed .pak Mod (ModA) ---");
            string modAPath = Path.Combine(mockModsCache, "armor_mod.pak");
            File.WriteAllBytes(modAPath, new byte[] { (byte)'K', (byte)'P', (byte)'K', (byte)'A', 0, 0, 0, 0 });
            
            installer.InstallMod(modAPath, "armor_mod.pak");

            string installedPak1 = Path.Combine(mockGameDir, "re_chunk_000.pak.patch_001.pak");
            bool check1 = File.Exists(installedPak1);
            Console.WriteLine($"[CHECKLIST 1] patch_001.pak created: {(check1 ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // TEST 2: Install Loose 'natives/' Folder Mod (ModB)
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 2: Installing Loose 'natives/' Folder Mod (ModB) ---");
            string modBDir = Path.Combine(mockModsCache, "weapon_mod_dir");
            string nativesSubDir = Path.Combine(modBDir, "natives", "STM", "Art", "Weapon");
            Directory.CreateDirectory(nativesSubDir);
            File.WriteAllText(Path.Combine(nativesSubDir, "greatsword.mesh"), "MOCK MESH DATA 12345");
            File.WriteAllText(Path.Combine(nativesSubDir, "greatsword.tex"), "MOCK TEXTURE DATA 67890");

            installer.InstallMod(modBDir, "weapon_mod_dir");

            string installedPak2 = Path.Combine(mockGameDir, "re_chunk_000.pak.patch_002.pak");
            bool check2Exists = File.Exists(installedPak2);

            bool check2ValidHeader = false;
            if (check2Exists)
            {
                byte[] bytes = File.ReadAllBytes(installedPak2);
                if (bytes.Length >= 4 && bytes[0] == 'K' && bytes[1] == 'P' && bytes[2] == 'K' && bytes[3] == 'A')
                {
                    check2ValidHeader = true;
                }
            }

            Console.WriteLine($"[CHECKLIST 2A] patch_002.pak created from natives/: {(check2Exists ? "PASSED ✅" : "FAILED ❌")}");
            Console.WriteLine($"[CHECKLIST 2B] patch_002.pak has valid RE Engine 'KPKA' Header: {(check2ValidHeader ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // TEST 3: Install 3rd Mod (ModC) to verify sequential numbering
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 3: Installing 3rd Mod (ModC) ---");
            string modCPath = Path.Combine(mockModsCache, "ui_mod.pak");
            File.WriteAllBytes(modCPath, new byte[] { (byte)'K', (byte)'P', (byte)'K', (byte)'A', 0, 0, 0, 0 });
            installer.InstallMod(modCPath, "ui_mod.pak");

            string installedPak3 = Path.Combine(mockGameDir, "re_chunk_000.pak.patch_003.pak");
            bool check3 = File.Exists(installedPak3);
            Console.WriteLine($"[CHECKLIST 3] patch_003.pak created sequentially: {(check3 ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // TEST 4: Uninstall ModB (patch_002) & Verify Gap Closure Re-sequencing
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 4: Uninstall ModB & Verify Gap Closure Re-sequencing ---");
            installer.UninstallMod("weapon_mod_dir");

            // After uninstalling ModB, patch_003 (ModC) is renamed down to patch_002!
            bool check4ModCReMapped = installer.State.InstalledMods.ContainsKey("ui_mod.pak") && 
                                     installer.State.InstalledMods["ui_mod.pak"].Contains("re_chunk_000.pak.patch_002.pak");
            bool check4Pak3NoLongerExists = !File.Exists(installedPak3);

            Console.WriteLine($"[CHECKLIST 4A] ModC (ui_mod.pak) re-mapped to patch_002.pak in state: {(check4ModCReMapped ? "PASSED ✅" : "FAILED ❌")}");
            Console.WriteLine($"[CHECKLIST 4B] Gap closed (patch_003 moved down, patch_003 no longer exists): {(check4Pak3NoLongerExists ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // TEST 5: Priority Re-ordering (Move Priority Up / Down)
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 5: Priority Re-ordering ---");
            int p1Before = installer.GetModPriority("armor_mod.pak");
            int p2Before = installer.GetModPriority("ui_mod.pak");
            bool moveSuccess = installer.MoveModPriority("ui_mod.pak", -1);
            int p1After = installer.GetModPriority("armor_mod.pak");
            int p2After = installer.GetModPriority("ui_mod.pak");
            bool check5 = moveSuccess && p2After < p1After;
            Console.WriteLine($"[CHECKLIST 5] Priority swapped successfully (ui_mod now higher priority): {(check5 ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // TEST 6: Conflict Detection
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 6: Conflict Detection ---");
            string conflictDir1 = Path.Combine(mockModsCache, "mod_c1");
            string conflictDir2 = Path.Combine(mockModsCache, "mod_c2");
            Directory.CreateDirectory(Path.Combine(conflictDir1, "natives", "STM", "Art"));
            Directory.CreateDirectory(Path.Combine(conflictDir2, "natives", "STM", "Art"));
            File.WriteAllText(Path.Combine(conflictDir1, "natives", "STM", "Art", "shared.mesh"), "DATA A");
            File.WriteAllText(Path.Combine(conflictDir2, "natives", "STM", "Art", "shared.mesh"), "DATA B");

            var conflicts = ConflictScanner.ScanConflicts(mockModsCache);
            bool check6 = conflicts.ContainsKey("mod_c1") && conflicts["mod_c1"].ConflictingMods.Contains("mod_c2");
            Console.WriteLine($"[CHECKLIST 6] File path conflict detected between mod_c1 and mod_c2: {(check6 ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // TEST 7: Backup & Restore State
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 7: Backup & Restore State ---");
            string backupPath = Path.Combine(testRoot, "backup.json");
            bool backupResult = BackupManager.CreateBackup(mockGameDir, backupPath);
            bool restoreResult = BackupManager.RestoreBackup(mockGameDir, backupPath);
            bool check7 = backupResult && restoreResult && File.Exists(backupPath);
            Console.WriteLine($"[CHECKLIST 7] Backup & Restore state operations: {(check7 ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // TEST 8: Direct Zip Archive Mod Import & Install (Unextracted Zip)
            // ----------------------------------------------------
            Console.WriteLine("\n--- TEST 8: Direct Zip Archive Mod Import & Install (Unextracted Zip) ---");
            string zipModPath = Path.Combine(mockModsCache, "zip_mod.zip");
            using (var zipArchive = System.IO.Compression.ZipFile.Open(zipModPath, System.IO.Compression.ZipArchiveMode.Create))
            {
                var entry = zipArchive.CreateEntry("inner_archive.pak");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("MOCK PAK DATA");
            }
            // Verify zip exists in GameMods as a raw zip file (not extracted to directory)
            bool check8FileExists = File.Exists(zipModPath) && !Directory.Exists(Path.Combine(mockModsCache, "zip_mod"));
            installer.InstallMod(zipModPath, "zip_mod.zip");
            bool check8Installed = installer.IsModInstalled("zip_mod.zip");
            bool check8 = check8FileExists && check8Installed;
            Console.WriteLine($"[CHECKLIST 8] Direct Zip Mod Import & Install without pre-extraction: {(check8 ? "PASSED ✅" : "FAILED ❌")}");

            // ----------------------------------------------------
            // Clean up test environment
            // ----------------------------------------------------
            try { Directory.Delete(testRoot, true); } catch { }

            bool allPassed = check1 && check2Exists && check2ValidHeader && check3 && check4ModCReMapped && check4Pak3NoLongerExists && check5 && check6 && check7 && check8;
            Console.WriteLine("\n=========================================================");
            Console.WriteLine(allPassed ? " ✅ ALL MH WILDS INSTALLER TESTS PASSED SUCCESSFULLY! " : " ❌ SOME TESTS FAILED! ");
            Console.WriteLine("=========================================================");
        }
    }
}
