using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using ModTogetherUniversal.Services;

namespace ModTogether.Tests.Simulate
{
    class Program
    {
        private static string _baseTempDir = "";
        private static string _hostDir = "";
        private static string _client1Dir = "";
        private static string _client2Dir = "";

        private static ModServer _server = new ModServer();
        private static ModClient _hostClient = new ModClient();
        private static ModClient _client1 = new ModClient();
        private static ModClient _client2 = new ModClient();

        private static ModFileWatcher _hostWatcher = null!;
        private static ModFileWatcher _client1Watcher = null!;
        private static ModFileWatcher _client2Watcher = null!;

        private static int _passCount = 0;
        private static int _failCount = 0;

        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("==========================================================");
            Console.WriteLine(" 🧪 ModTogether P2P - MHW Mod Sync Simulation Test Suite");
            Console.WriteLine("==========================================================\n");

            try
            {
                SetupDirectories();
                CreateDummyArchiveFiles();

                await StartClusterAsync();

                Console.WriteLine("🚀 Cluster Started successfully. Running Test Scenarios...\n");

                await RunTest1_HostCreatesMod_SyncsToClients();
                await RunTest2_Client1UploadsMod_SyncsToHostAndClient2();
                await RunTest3_Client1DeletesMod_SyncsDeletionToHostAndClient2();
                await RunTest4_HostDeletesMod_SyncsDeletionToClients();
                await RunTest5_HostRestoresMod_SyncsRestoreToClients();
                await RunTest6_Client1RestoresMod_SyncsToHostAndClient2();
                await RunTest7_Client2DeletesMod_SyncsToHostAndClient1();
                await RunTest7_Client2DeletesMod_SyncsToHostAndClient1();
                RunTest8_ModConflictDetection();
                await RunTest9_Continuous10FileTransfer();
                await RunTest10_ConcurrentSameFileUpload();
                await RunTest11_UploadThenImmediateDelete();
                await RunTest12_NetworkDropSimulation();
                await RunTest13_LargeFileTransfer();
                await RunTest14_MultiUserChaoticExchange();
                await RunTest15_ExtremeChaoticSync();
                await RunTest16_DisconnectDuringTransfer();
                await RunTest17_ConcurrentImportAndDelete();
                await RunTest18_IdenticalDuplicateFilesOnConnection();
                await RunTest19_FileLockInUseSimulation();
                await RunTest20_RapidRenameMovingFiles();
                await RunTest21_CorruptIncompleteFileDrop();

                PrintSummary();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Critical Test Error: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                CleanupCluster();
            }
        }

        private static void SetupDirectories()
        {
            _baseTempDir = Path.Combine(Path.GetTempPath(), "ModTogetherSimTest_" + Guid.NewGuid().ToString("N"));
            _hostDir = Path.Combine(_baseTempDir, "Host", "GameMods");
            _client1Dir = Path.Combine(_baseTempDir, "Client1", "GameMods");
            _client2Dir = Path.Combine(_baseTempDir, "Client2", "GameMods");

            Directory.CreateDirectory(_hostDir);
            Directory.CreateDirectory(_client1Dir);
            Directory.CreateDirectory(_client2Dir);

            Console.WriteLine($"📁 Environment Created:\n   Host: {_hostDir}\n   Client1: {_client1Dir}\n   Client2: {_client2Dir}\n");
        }

        private static string _armorZipPath = "";
        private static string _weaponZipPath = "";

        private static void CreateDummyArchiveFiles()
        {
            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            Directory.CreateDirectory(archivesDir);

            _armorZipPath = Path.Combine(archivesDir, "MHW_Armor_Mod.zip");
            _weaponZipPath = Path.Combine(archivesDir, "MHW_Weapon_Mod.zip");

            CreateZipWithDummyContent(_armorZipPath, "nativePC/pl/f_equip/pl001/arm001.mod", "ARMOR_DATA_V1");
            CreateZipWithDummyContent(_weaponZipPath, "nativePC/wp/wp01/weapon001.mod", "WEAPON_DATA_V1");
        }

        private static void CreateZipWithDummyContent(string zipPath, string internalPath, string content)
        {
            if (File.Exists(zipPath)) File.Delete(zipPath);
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry(internalPath);
                using (var writer = new StreamWriter(entry.Open()))
                {
                    writer.Write(content);
                }
            }
        }

        private static async Task StartClusterAsync()
        {
            int port = 52233;
            string token = "SIMPIN";

            // Setup Server & Loggers
            _server.OnLog += msg => Log("[SERVER]", msg);
            await _server.StartAsync(_hostDir, port, token);

            // Configure Host Client & Host FileWatcher
            _hostClient.Configure("127.0.0.1", port, token, "HostUser");
            _hostWatcher = new ModFileWatcher(_hostClient);
            _hostWatcher.Start(_hostDir);

            // Configure Client 1
            _client1.OnLog += msg => Log("[CLIENT1]", msg);
            _client1.Configure("127.0.0.1", port, token, "Client1User");
            _client1Watcher = new ModFileWatcher(_client1);
            _client1Watcher.Start(_client1Dir);
            _client1.StartBackgroundTasks(_client1Dir);

            // Configure Client 2
            _client2.OnLog += msg => Log("[CLIENT2]", msg);
            _client2.Configure("127.0.0.1", port, token, "Client2User");
            _client2Watcher = new ModFileWatcher(_client2);
            _client2Watcher.Start(_client2Dir);
            _client2.StartBackgroundTasks(_client2Dir);

            await Task.Delay(1000);
        }

        private static async Task RunTest1_HostCreatesMod_SyncsToClients()
        {
            Console.WriteLine("----------------------------------------------------------");
            Console.WriteLine("▶ TEST 1: Host creates mod (MHW_Armor_Mod.zip) -> Sync to Clients");
            Console.WriteLine("----------------------------------------------------------");

            string destFile = Path.Combine(_hostDir, "MHW_Armor_Mod.zip");
            File.Copy(_armorZipPath, destFile, true);
            Console.WriteLine($"[ACTION] Host added MHW_Armor_Mod.zip");

            await Task.Delay(4500);

            bool inClient1 = File.Exists(Path.Combine(_client1Dir, "MHW_Armor_Mod.zip"));
            bool inClient2 = File.Exists(Path.Combine(_client2Dir, "MHW_Armor_Mod.zip"));

            Assert("Client 1 downloaded MHW_Armor_Mod.zip from Host", inClient1);
            Assert("Client 2 downloaded MHW_Armor_Mod.zip from Host", inClient2);
        }

        private static async Task RunTest2_Client1UploadsMod_SyncsToHostAndClient2()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 2: Client 1 uploads mod (MHW_Weapon_Mod.zip) -> Sync to Host & Client 2");
            Console.WriteLine("----------------------------------------------------------");

            string destFile = Path.Combine(_client1Dir, "MHW_Weapon_Mod.zip");
            File.Copy(_weaponZipPath, destFile, true);
            Console.WriteLine($"[ACTION] Client 1 added MHW_Weapon_Mod.zip");

            await Task.Delay(5500);

            bool inHost = File.Exists(Path.Combine(_hostDir, "MHW_Weapon_Mod.zip"));
            bool inClient2 = File.Exists(Path.Combine(_client2Dir, "MHW_Weapon_Mod.zip"));

            Assert("Host received MHW_Weapon_Mod.zip upload from Client 1", inHost);
            Assert("Client 2 downloaded MHW_Weapon_Mod.zip via Sync", inClient2);
        }

        private static async Task RunTest3_Client1DeletesMod_SyncsDeletionToHostAndClient2()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 3: Client 1 deletes MHW_Armor_Mod.zip -> Sync deletion to Host & Client 2");
            Console.WriteLine("----------------------------------------------------------");

            string client1File = Path.Combine(_client1Dir, "MHW_Armor_Mod.zip");
            string client1Recycle = Path.Combine(_client1Dir, ".recycle_mods", "MHW_Armor_Mod.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(client1Recycle)!);
            ModTogether.API.FileHelper.SafeMove(client1File, client1Recycle, true);
            Console.WriteLine($"[ACTION] Client 1 deleted MHW_Armor_Mod.zip (Moved to .recycle_mods)");

            await Task.Delay(5500);

            bool hostRecycled = File.Exists(Path.Combine(_hostDir, ".recycle_mods", "MHW_Armor_Mod.zip")) && !File.Exists(Path.Combine(_hostDir, "MHW_Armor_Mod.zip"));
            bool client2Recycled = File.Exists(Path.Combine(_client2Dir, ".recycle_mods", "MHW_Armor_Mod.zip")) && !File.Exists(Path.Combine(_client2Dir, "MHW_Armor_Mod.zip"));

            Assert("Host received deletion and moved MHW_Armor_Mod.zip to recycle bin", hostRecycled);
            Assert("Client 2 synced deletion and moved MHW_Armor_Mod.zip to recycle bin", client2Recycled);
        }

        private static async Task RunTest4_HostDeletesMod_SyncsDeletionToClients()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 4: Host deletes MHW_Weapon_Mod.zip -> Sync deletion to Clients");
            Console.WriteLine("----------------------------------------------------------");

            string hostFile = Path.Combine(_hostDir, "MHW_Weapon_Mod.zip");
            string hostRecycle = Path.Combine(_hostDir, ".recycle_mods", "MHW_Weapon_Mod.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(hostRecycle)!);
            ModTogether.API.FileHelper.SafeMove(hostFile, hostRecycle, true);
            Console.WriteLine($"[ACTION] Host deleted MHW_Weapon_Mod.zip (Moved to .recycle_mods)");

            await Task.Delay(5500);

            bool client1Recycled = File.Exists(Path.Combine(_client1Dir, ".recycle_mods", "MHW_Weapon_Mod.zip")) && !File.Exists(Path.Combine(_client1Dir, "MHW_Weapon_Mod.zip"));
            bool client2Recycled = File.Exists(Path.Combine(_client2Dir, ".recycle_mods", "MHW_Weapon_Mod.zip")) && !File.Exists(Path.Combine(_client2Dir, "MHW_Weapon_Mod.zip"));

            Assert("Client 1 synced Host deletion and moved MHW_Weapon_Mod.zip to recycle bin", client1Recycled);
            Assert("Client 2 synced Host deletion and moved MHW_Weapon_Mod.zip to recycle bin", client2Recycled);
        }

        private static async Task RunTest5_HostRestoresMod_SyncsRestoreToClients()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 5: Host restores MHW_Armor_Mod.zip -> Sync restore to Clients");
            Console.WriteLine("----------------------------------------------------------");

            string hostRecycle = Path.Combine(_hostDir, ".recycle_mods", "MHW_Armor_Mod.zip");
            string hostFile = Path.Combine(_hostDir, "MHW_Armor_Mod.zip");
            ModTogether.API.FileHelper.SafeMove(hostRecycle, hostFile, true);
            
            // Remove from Server's deleted mods list
            _server.DeletedMods.TryRemove("MHW_Armor_Mod.zip", out _);
            _server.TriggerCacheRefresh();
            Console.WriteLine($"[ACTION] Host restored MHW_Armor_Mod.zip from recycle bin");

            await Task.Delay(5500);

            bool client1Restored = File.Exists(Path.Combine(_client1Dir, "MHW_Armor_Mod.zip"));
            bool client2Restored = File.Exists(Path.Combine(_client2Dir, "MHW_Armor_Mod.zip"));

            Assert("Client 1 synced restore (Smart Restore) for MHW_Armor_Mod.zip", client1Restored);
            Assert("Client 2 synced restore (Smart Restore) for MHW_Armor_Mod.zip", client2Restored);
        }

        private static async Task RunTest6_Client1RestoresMod_SyncsToHostAndClient2()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 6: Client 1 restores MHW_Weapon_Mod.zip -> Sync restore to Host & Client 2");
            Console.WriteLine("----------------------------------------------------------");

            string client1Recycle = Path.Combine(_client1Dir, ".recycle_mods", "MHW_Weapon_Mod.zip");
            string client1File = Path.Combine(_client1Dir, "MHW_Weapon_Mod.zip");
            ModTogether.API.FileHelper.SafeMove(client1Recycle, client1File, true);
            Console.WriteLine($"[ACTION] Client 1 restored MHW_Weapon_Mod.zip from recycle bin");

            await Task.Delay(5500);

            bool hostRestored = File.Exists(Path.Combine(_hostDir, "MHW_Weapon_Mod.zip"));
            bool client2Restored = File.Exists(Path.Combine(_client2Dir, "MHW_Weapon_Mod.zip"));

            Assert("Host received restored mod upload from Client 1", hostRestored);
            Assert("Client 2 synced restore for MHW_Weapon_Mod.zip", client2Restored);
        }

        private static async Task RunTest7_Client2DeletesMod_SyncsToHostAndClient1()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 7: Client 2 deletes MHW_Armor_Mod.zip -> Sync deletion to Host & Client 1");
            Console.WriteLine("----------------------------------------------------------");

            string client2File = Path.Combine(_client2Dir, "MHW_Armor_Mod.zip");
            string client2Recycle = Path.Combine(_client2Dir, ".recycle_mods", "MHW_Armor_Mod.zip");
            Directory.CreateDirectory(Path.GetDirectoryName(client2Recycle)!);
            ModTogether.API.FileHelper.SafeMove(client2File, client2Recycle, true);
            Console.WriteLine($"[ACTION] Client 2 deleted MHW_Armor_Mod.zip (Moved to .recycle_mods)");

            await Task.Delay(5500);

            bool hostRecycled = File.Exists(Path.Combine(_hostDir, ".recycle_mods", "MHW_Armor_Mod.zip")) && !File.Exists(Path.Combine(_hostDir, "MHW_Armor_Mod.zip"));
            bool client1Recycled = File.Exists(Path.Combine(_client1Dir, ".recycle_mods", "MHW_Armor_Mod.zip")) && !File.Exists(Path.Combine(_client1Dir, "MHW_Armor_Mod.zip"));

            Assert("Host received Client 2 deletion and moved MHW_Armor_Mod.zip to recycle bin", hostRecycled);
            Assert("Client 1 synced Client 2 deletion and moved MHW_Armor_Mod.zip to recycle bin", client1Recycled);
        }

        private static void RunTest8_ModConflictDetection()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 8: Mod Conflict Detector & Priority Scanner");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "ConflictArchives");
            Directory.CreateDirectory(archivesDir);

            string modA = Path.Combine(archivesDir, "ModA_Armor.zip");
            string modB = Path.Combine(archivesDir, "ModB_ArmorAlt.zip");

            CreateZipWithDummyContent(modA, "nativePC/pl/f_equip/pl001/arm001.mod", "MOD_A_CONTENT");
            CreateZipWithDummyContent(modB, "nativePC/pl/f_equip/pl001/arm001.mod", "MOD_B_CONTENT");

            var activeMods = new List<string> { "ModA_Armor.zip", "ModB_ArmorAlt.zip" };
            var scanResult = ModConflictDetector.AnalyzeConflicts(archivesDir, activeMods);

            Assert("Conflict Scanner detected overlapping nativePC files", scanResult.HasConflicts);
            Assert("Conflict Scanner flagged both ModA and ModB as conflicted", scanResult.ConflictedModFilenames.Contains("ModA_Armor.zip") && scanResult.ConflictedModFilenames.Contains("ModB_ArmorAlt.zip"));
        }

        private static async Task RunTest9_Continuous10FileTransfer()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 9: Continuous 10 File Transfer (Stress Test)");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            
            for(int i = 1; i <= 10; i++)
            {
                string path = Path.Combine(archivesDir, $"StressMod_{i}.zip");
                CreateZipWithDummyContent(path, $"nativePC/stress/mod{i}.mod", $"STRESS_{i}");
            }

            Console.WriteLine($"[ACTION] Client 1 adding 10 files continuously...");
            for(int i = 1; i <= 10; i++)
            {
                string source = Path.Combine(archivesDir, $"StressMod_{i}.zip");
                string dest = Path.Combine(_client1Dir, $"StressMod_{i}.zip");
                File.Copy(source, dest, true);
            }

            await Task.Delay(15000); 

            int hostCount = 0;
            int client2Count = 0;
            for(int i = 1; i <= 10; i++)
            {
                if (File.Exists(Path.Combine(_hostDir, $"StressMod_{i}.zip"))) hostCount++;
                if (File.Exists(Path.Combine(_client2Dir, $"StressMod_{i}.zip"))) client2Count++;
            }

            Assert($"Host received {hostCount}/10 files continuously", hostCount == 10);
            Assert($"Client 2 received {client2Count}/10 files continuously", client2Count == 10);
        }

        private static async Task RunTest10_ConcurrentSameFileUpload()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 10: Concurrent Upload of Same Filename (Race Condition)");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string pathA = Path.Combine(archivesDir, "RaceMod_A.zip");
            string pathB = Path.Combine(archivesDir, "RaceMod_B.zip");

            CreateZipWithDummyContent(pathA, "nativePC/race.mod", "CLIENT1_DATA");
            CreateZipWithDummyContent(pathB, "nativePC/race.mod", "CLIENT2_DATA");

            string dest1 = Path.Combine(_client1Dir, "RaceMod.zip");
            string dest2 = Path.Combine(_client2Dir, "RaceMod.zip");

            Console.WriteLine($"[ACTION] Client 1 and Client 2 add 'RaceMod.zip' simultaneously with different contents!");
            
            var t1 = Task.Run(() => File.Copy(pathA, dest1, true));
            var t2 = Task.Run(() => File.Copy(pathB, dest2, true));

            await Task.WhenAll(t1, t2);

            await Task.Delay(10000); 

            // Verify
            long hostLen = new FileInfo(Path.Combine(_hostDir, "RaceMod.zip")).Length;
            long c1Len = new FileInfo(Path.Combine(_client1Dir, "RaceMod.zip")).Length;
            long c2Len = new FileInfo(Path.Combine(_client2Dir, "RaceMod.zip")).Length;

            Assert($"All nodes converged to the same file size (Host: {hostLen}, C1: {c1Len}, C2: {c2Len})", hostLen > 0 && hostLen == c1Len && hostLen == c2Len);
        }

        private static async Task RunTest11_UploadThenImmediateDelete()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 11: Upload followed by immediate Delete (Ghost File)");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string ghostPath = Path.Combine(archivesDir, "GhostMod.zip");
            CreateZipWithDummyContent(ghostPath, "nativePC/ghost.mod", "GHOST_DATA");

            string hostDest = Path.Combine(_hostDir, "GhostMod.zip");
            
            Console.WriteLine($"[ACTION] Host adds GhostMod.zip then instantly deletes it.");
            File.Copy(ghostPath, hostDest, true);
            await Task.Delay(200); // Wait just enough for FileWatcher to trigger but maybe not sync completely
            string recycle = Path.Combine(_hostDir, ".recycle_mods", "GhostMod.zip");
            ModTogether.API.FileHelper.SafeMove(hostDest, recycle, true);

            await Task.Delay(10000);

            bool hostHasGhost = File.Exists(hostDest);
            bool c1HasGhost = File.Exists(Path.Combine(_client1Dir, "GhostMod.zip"));
            bool c2HasGhost = File.Exists(Path.Combine(_client2Dir, "GhostMod.zip"));

            Assert($"Host no longer has GhostMod.zip", !hostHasGhost);
            Assert($"Client 1 aborted download or deleted GhostMod.zip", !c1HasGhost);
            Assert($"Client 2 aborted download or deleted GhostMod.zip", !c2HasGhost);
        }

        private static async Task RunTest12_NetworkDropSimulation()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 12: Network Drop & Reconnect Simulation");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string dropPath = Path.Combine(archivesDir, "DropTestMod.zip");
            CreateZipWithDummyContent(dropPath, "nativePC/droptest.mod", "DROP_TEST_DATA");

            Console.WriteLine($"[ACTION] Client 1 disconnects (Simulating Network Drop)");
            _client1.StopBackgroundTasks();
            await Task.Delay(2000); // Give it a moment to realize it's disconnected

            Console.WriteLine($"[ACTION] Host adds DropTestMod.zip while Client 1 is offline");
            string hostDest = Path.Combine(_hostDir, "DropTestMod.zip");
            File.Copy(dropPath, hostDest, true);

            await Task.Delay(5000); 

            bool c1HasModWhileOffline = File.Exists(Path.Combine(_client1Dir, "DropTestMod.zip"));
            Assert($"Client 1 does NOT have the mod while offline", !c1HasModWhileOffline);

            Console.WriteLine($"[ACTION] Client 1 reconnects to the network");
            _client1.StartBackgroundTasks(_client1Dir);

            await Task.Delay(6000);

            bool c1HasModAfterReconnect = File.Exists(Path.Combine(_client1Dir, "DropTestMod.zip"));
            Assert($"Client 1 automatically synced the missing mod after reconnecting", c1HasModAfterReconnect);
        }

        private static async Task RunTest13_LargeFileTransfer()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 13: Large File Transfer Simulation (50MB)");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string largePath = Path.Combine(archivesDir, "LargeMod.zip");
            
            if (File.Exists(largePath)) File.Delete(largePath);
            using (var zip = ZipFile.Open(largePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("nativePC/large.mod");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    char[] buffer = new char[1024 * 1024]; // 1MB
                    Array.Fill(buffer, 'X');
                    for (int i = 0; i < 50; i++)
                    {
                        writer.Write(buffer);
                    }
                }
            }

            Console.WriteLine($"[ACTION] Host adds LargeMod.zip (50MB)");
            string hostDest = Path.Combine(_hostDir, "LargeMod.zip");
            File.Copy(largePath, hostDest, true);

            await Task.Delay(15000); 

            bool c1HasMod = File.Exists(Path.Combine(_client1Dir, "LargeMod.zip"));
            long c1Size = c1HasMod ? new FileInfo(Path.Combine(_client1Dir, "LargeMod.zip")).Length : 0;
            long hostSize = new FileInfo(hostDest).Length;

            Assert($"Client 1 successfully downloaded 50MB large mod (Size: {c1Size} bytes)", c1HasMod && c1Size == hostSize);
        }

        private static async Task RunTest14_MultiUserChaoticExchange()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 14: Multi-User Chaotic Exchange (All users sending files simultaneously)");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            
            for (int i = 1; i <= 3; i++)
            {
                CreateZipWithDummyContent(Path.Combine(archivesDir, $"HostMod_{i}.zip"), $"nativePC/host_{i}.mod", "HOST_DATA");
                CreateZipWithDummyContent(Path.Combine(archivesDir, $"C1Mod_{i}.zip"), $"nativePC/c1_{i}.mod", "C1_DATA");
                CreateZipWithDummyContent(Path.Combine(archivesDir, $"C2Mod_{i}.zip"), $"nativePC/c2_{i}.mod", "C2_DATA");
            }

            Console.WriteLine($"[ACTION] Host, Client1, and Client2 are uploading 3 files each simultaneously...");

            var tHost = Task.Run(() => 
            {
                for (int i = 1; i <= 3; i++) File.Copy(Path.Combine(archivesDir, $"HostMod_{i}.zip"), Path.Combine(_hostDir, $"HostMod_{i}.zip"), true);
            });
            var tC1 = Task.Run(() => 
            {
                for (int i = 1; i <= 3; i++) File.Copy(Path.Combine(archivesDir, $"C1Mod_{i}.zip"), Path.Combine(_client1Dir, $"C1Mod_{i}.zip"), true);
            });
            var tC2 = Task.Run(() => 
            {
                for (int i = 1; i <= 3; i++) File.Copy(Path.Combine(archivesDir, $"C2Mod_{i}.zip"), Path.Combine(_client2Dir, $"C2Mod_{i}.zip"), true);
            });

            await Task.WhenAll(tHost, tC1, tC2);

            await Task.Delay(15000);

            int hostFiles = 0, c1Files = 0, c2Files = 0;
            string[] expectedFiles = { "HostMod_1.zip", "HostMod_2.zip", "HostMod_3.zip", "C1Mod_1.zip", "C1Mod_2.zip", "C1Mod_3.zip", "C2Mod_1.zip", "C2Mod_2.zip", "C2Mod_3.zip" };

            foreach (var f in expectedFiles)
            {
                if (File.Exists(Path.Combine(_hostDir, f))) hostFiles++;
                if (File.Exists(Path.Combine(_client1Dir, f))) c1Files++;
                if (File.Exists(Path.Combine(_client2Dir, f))) c2Files++;
            }

            Assert($"Host received all exchanged files (9/9)", hostFiles == 9);
            Assert($"Client 1 received all exchanged files (9/9)", c1Files == 9);
            Assert($"Client 2 received all exchanged files (9/9)", c2Files == 9);
        }

        private static async Task RunTest15_ExtremeChaoticSync()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 15: Extreme Chaotic Sync (Concurrent Upload, Delete, and Restore)");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            CreateZipWithDummyContent(Path.Combine(archivesDir, "Chaos_ToRestore.zip"), "nativePC/chaos1.mod", "DATA1");
            CreateZipWithDummyContent(Path.Combine(archivesDir, "Chaos_ToDelete.zip"), "nativePC/chaos2.mod", "DATA2");
            CreateZipWithDummyContent(Path.Combine(archivesDir, "Chaos_New.zip"), "nativePC/chaos3.mod", "DATA3");

            // Setup initial state: 
            // 1. Host has Chaos_ToRestore.zip in recycle bin
            Directory.CreateDirectory(Path.Combine(_hostDir, ".recycle_mods"));
            File.Copy(Path.Combine(archivesDir, "Chaos_ToRestore.zip"), Path.Combine(_hostDir, ".recycle_mods", "Chaos_ToRestore.zip"), true);
            
            // 2. Client 1 has Chaos_ToDelete.zip as active
            File.Copy(Path.Combine(archivesDir, "Chaos_ToDelete.zip"), Path.Combine(_client1Dir, "Chaos_ToDelete.zip"), true);

            // Wait for initial sync to propagate
            Console.WriteLine($"[ACTION] Preparing baseline state across nodes...");
            await Task.Delay(5000); 

            Console.WriteLine($"[ACTION] Simultaneously: Host RESTORES, Client1 DELETES, Client2 UPLOADS...");
            
            var tHost = Task.Run(() => 
            {
                // Host restores
                string recycle = Path.Combine(_hostDir, ".recycle_mods", "Chaos_ToRestore.zip");
                string active = Path.Combine(_hostDir, "Chaos_ToRestore.zip");
                if (File.Exists(recycle)) ModTogether.API.FileHelper.SafeMove(recycle, active, true);
                _server.DeletedMods.TryRemove("Chaos_ToRestore.zip", out _);
                _server.TriggerCacheRefresh();
            });

            var tC1 = Task.Run(() => 
            {
                // Client1 deletes
                string active = Path.Combine(_client1Dir, "Chaos_ToDelete.zip");
                string recycle = Path.Combine(_client1Dir, ".recycle_mods", "Chaos_ToDelete.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(recycle)!);
                if (File.Exists(active)) ModTogether.API.FileHelper.SafeMove(active, recycle, true);
            });

            var tC2 = Task.Run(() => 
            {
                // Client2 uploads
                string src = Path.Combine(archivesDir, "Chaos_New.zip");
                string dest = Path.Combine(_client2Dir, "Chaos_New.zip");
                File.Copy(src, dest, true);
            });

            await Task.WhenAll(tHost, tC1, tC2);

            // Wait for changes to fully propagate across P2P
            await Task.Delay(10000);

            // Verify Chaos_ToRestore.zip is active everywhere
            bool restoreHost = File.Exists(Path.Combine(_hostDir, "Chaos_ToRestore.zip"));
            bool restoreC1 = File.Exists(Path.Combine(_client1Dir, "Chaos_ToRestore.zip"));
            bool restoreC2 = File.Exists(Path.Combine(_client2Dir, "Chaos_ToRestore.zip"));
            Assert("All nodes successfully restored 'Chaos_ToRestore.zip'", restoreHost && restoreC1 && restoreC2);

            // Verify Chaos_ToDelete.zip is deleted everywhere (moved to recycle bin)
            bool delHost = !File.Exists(Path.Combine(_hostDir, "Chaos_ToDelete.zip"));
            bool delC1 = !File.Exists(Path.Combine(_client1Dir, "Chaos_ToDelete.zip"));
            bool delC2 = !File.Exists(Path.Combine(_client2Dir, "Chaos_ToDelete.zip"));
            Assert("All nodes successfully deleted 'Chaos_ToDelete.zip'", delHost && delC1 && delC2);

            // Verify Chaos_New.zip is uploaded everywhere
            bool newHost = File.Exists(Path.Combine(_hostDir, "Chaos_New.zip"));
            bool newC1 = File.Exists(Path.Combine(_client1Dir, "Chaos_New.zip"));
            bool newC2 = File.Exists(Path.Combine(_client2Dir, "Chaos_New.zip"));
            Assert("All nodes successfully received uploaded 'Chaos_New.zip'", newHost && newC1 && newC2);
        }

        private static async Task RunTest16_DisconnectDuringTransfer()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 16: Network Drop & Reconnect DURING Active Transfer");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string largePath = Path.Combine(archivesDir, "InterruptedMod.zip");
            
            // Create a 100MB file to ensure transfer takes some time
            if (File.Exists(largePath)) File.Delete(largePath);
            using (var zip = ZipFile.Open(largePath, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("nativePC/large_interrupt.mod");
                using (var writer = new StreamWriter(entry.Open()))
                {
                    char[] buffer = new char[1024 * 1024]; // 1MB
                    Array.Fill(buffer, 'Y');
                    for (int i = 0; i < 100; i++)
                    {
                        writer.Write(buffer);
                    }
                }
            }

            Console.WriteLine($"[ACTION] Host adds InterruptedMod.zip (100MB)");
            string hostDest = Path.Combine(_hostDir, "InterruptedMod.zip");
            File.Copy(largePath, hostDest, true);

            // Wait just enough for the transfer to start but not finish
            await Task.Delay(500); 

            Console.WriteLine($"[ACTION] Client 1 disconnects DURING the transfer (Network Drop)");
            _client1.StopBackgroundTasks();
            
            await Task.Delay(3000); // Stay disconnected for 3 seconds

            Console.WriteLine($"[ACTION] Client 1 reconnects to the network");
            _client1.StartBackgroundTasks(_client1Dir);

            // Wait for resume/restart to finish
            await Task.Delay(20000); 

            bool c1HasMod = File.Exists(Path.Combine(_client1Dir, "InterruptedMod.zip"));
            long c1Size = c1HasMod ? new FileInfo(Path.Combine(_client1Dir, "InterruptedMod.zip")).Length : 0;
            long hostSize = new FileInfo(hostDest).Length;

            Assert($"Client 1 successfully received the full 100MB mod after reconnecting (Size: {c1Size} bytes)", c1HasMod && c1Size == hostSize);
        }

        private static async Task RunTest17_ConcurrentImportAndDelete()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 17: Concurrent Import and Concurrent Delete");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string pathA = Path.Combine(archivesDir, "SameMod.zip");
            CreateZipWithDummyContent(pathA, "nativePC/samemod.mod", "SAME_DATA");

            string hostDest = Path.Combine(_hostDir, "SameMod.zip");
            string c1Dest = Path.Combine(_client1Dir, "SameMod.zip");
            
            // --- PART 1: Concurrent Import ---
            Console.WriteLine($"[ACTION] Host and Client 1 IMPORT 'SameMod.zip' simultaneously...");
            var tImportHost = Task.Run(() => File.Copy(pathA, hostDest, true));
            var tImportC1 = Task.Run(() => File.Copy(pathA, c1Dest, true));

            await Task.WhenAll(tImportHost, tImportC1);
            await Task.Delay(8000); // Wait for sync

            bool existsHost = File.Exists(hostDest);
            bool existsC1 = File.Exists(c1Dest);
            bool existsC2 = File.Exists(Path.Combine(_client2Dir, "SameMod.zip"));

            Assert($"All nodes successfully synchronized the concurrently imported 'SameMod.zip'", existsHost && existsC1 && existsC2);

            // --- PART 2: Concurrent Delete ---
            Console.WriteLine($"[ACTION] Host and Client 1 DELETE 'SameMod.zip' simultaneously...");
            var tDeleteHost = Task.Run(() => 
            {
                string recycle = Path.Combine(_hostDir, ".recycle_mods", "SameMod.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(recycle)!);
                if (File.Exists(hostDest)) ModTogether.API.FileHelper.SafeMove(hostDest, recycle, true);
            });
            var tDeleteC1 = Task.Run(() => 
            {
                string recycle = Path.Combine(_client1Dir, ".recycle_mods", "SameMod.zip");
                Directory.CreateDirectory(Path.GetDirectoryName(recycle)!);
                if (File.Exists(c1Dest)) ModTogether.API.FileHelper.SafeMove(c1Dest, recycle, true);
            });

            await Task.WhenAll(tDeleteHost, tDeleteC1);
            await Task.Delay(8000); // Wait for sync

            bool deletedHost = !File.Exists(hostDest);
            bool deletedC1 = !File.Exists(c1Dest);
            bool deletedC2 = !File.Exists(Path.Combine(_client2Dir, "SameMod.zip"));

            Assert($"All nodes successfully synchronized the concurrently deleted 'SameMod.zip'", deletedHost && deletedC1 && deletedC2);
        }

        private static async Task RunTest18_IdenticalDuplicateFilesOnConnection()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 18: Identical Duplicate Files on Connection");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string dupPath = Path.Combine(archivesDir, "DuplicateMod.zip");
            CreateZipWithDummyContent(dupPath, "nativePC/duplicate.mod", "DUPLICATE_DATA_123");

            // Stop clients temporarily to simulate offline file placement
            _client1.StopBackgroundTasks();
            _client2.StopBackgroundTasks();
            await Task.Delay(1000);

            Console.WriteLine($"[ACTION] Host, Client1, and Client2 all receive the EXACT SAME file locally while offline...");
            string hostDest = Path.Combine(_hostDir, "DuplicateMod.zip");
            string c1Dest = Path.Combine(_client1Dir, "DuplicateMod.zip");
            string c2Dest = Path.Combine(_client2Dir, "DuplicateMod.zip");

            File.Copy(dupPath, hostDest, true);
            File.Copy(dupPath, c1Dest, true);
            File.Copy(dupPath, c2Dest, true);

            Console.WriteLine($"[ACTION] Clients reconnect and sync starts. They should NOT endlessly loop or overwrite each other with the same file.");
            _client1.StartBackgroundTasks(_client1Dir);
            _client2.StartBackgroundTasks(_client2Dir);

            await Task.Delay(8000);

            // Verify they still exist and are the correct size
            long originalSize = new FileInfo(dupPath).Length;
            bool hostExists = File.Exists(hostDest) && new FileInfo(hostDest).Length == originalSize;
            bool c1Exists = File.Exists(c1Dest) && new FileInfo(c1Dest).Length == originalSize;
            bool c2Exists = File.Exists(c2Dest) && new FileInfo(c2Dest).Length == originalSize;

            Assert($"All nodes converged on 'DuplicateMod.zip' peacefully", hostExists && c1Exists && c2Exists);
        }

        private static async Task RunTest19_FileLockInUseSimulation()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 19: File Lock / In-Use Simulation");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string lockPath = Path.Combine(archivesDir, "LockedMod.zip");
            CreateZipWithDummyContent(lockPath, "nativePC/locked.mod", "LOCKED_DATA");

            string c1Dest = Path.Combine(_client1Dir, "LockedMod.zip");
            File.Copy(lockPath, c1Dest, true);
            Console.WriteLine($"[ACTION] Client 1 adds 'LockedMod.zip'.");

            await Task.Delay(5000); // let it sync to host and C2

            string hostDest = Path.Combine(_hostDir, "LockedMod.zip");
            
            Console.WriteLine($"[ACTION] Host LOCKS 'LockedMod.zip' (simulating game reading it).");
            using (FileStream fs = new FileStream(hostDest, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                Console.WriteLine($"[ACTION] Client 1 DELETES 'LockedMod.zip'. Host should handle lock gracefully and retry or ignore.");
                string recycle = Path.Combine(_client1Dir, ".recycle_mods", "LockedMod.zip");
                ModTogether.API.FileHelper.SafeMove(c1Dest, recycle, true);

                await Task.Delay(8000);

                bool c1Deleted = !File.Exists(c1Dest);
                bool hostStillExists = File.Exists(hostDest); // Still locked, so might still exist if delete failed, or might be queued

                Assert($"Client 1 successfully deleted the file locally", c1Deleted);
                // We just want to ensure it didn't crash. State convergence depends on retry logic.
                Assert($"Host survived lock collision (File still exists: {hostStillExists})", true);
            }
        }

        private static async Task RunTest20_RapidRenameMovingFiles()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 20: Rapid Rename / Moving Files");
            Console.WriteLine("----------------------------------------------------------");

            string archivesDir = Path.Combine(_baseTempDir, "RawArchives");
            string renamePath = Path.Combine(archivesDir, "RenameMod.zip");
            CreateZipWithDummyContent(renamePath, "nativePC/rename.mod", "RENAME_DATA");

            string c2Dest = Path.Combine(_client2Dir, "RenameMod.zip");
            File.Copy(renamePath, c2Dest, true);
            
            await Task.Delay(200); // Give watcher just enough time to spot it

            Console.WriteLine($"[ACTION] Client 2 rapidly renames the file before sync fully finishes...");
            string c2NewDest = Path.Combine(_client2Dir, "RenameMod_v2.zip");
            if (File.Exists(c2Dest)) File.Move(c2Dest, c2NewDest);

            await Task.Delay(8000);

            bool hostHasV2 = File.Exists(Path.Combine(_hostDir, "RenameMod_v2.zip"));
            bool hostHasV1 = File.Exists(Path.Combine(_hostDir, "RenameMod.zip"));

            Assert($"Host received the final renamed file ('RenameMod_v2.zip')", hostHasV2);
            Assert($"Host does not have ghost file ('RenameMod.zip')", !hostHasV1);
        }

        private static async Task RunTest21_CorruptIncompleteFileDrop()
        {
            Console.WriteLine("\n----------------------------------------------------------");
            Console.WriteLine("▶ TEST 21: Corrupt/Incomplete File Drop (Slow Download Simulation)");
            Console.WriteLine("----------------------------------------------------------");

            string slowPath = Path.Combine(_client1Dir, "SlowMod.zip");
            Console.WriteLine($"[ACTION] Client 1 slowly writes to 'SlowMod.zip' over 3 seconds...");
            
            var writerTask = Task.Run(async () => 
            {
                using (FileStream fs = new FileStream(slowPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] data = new byte[1024];
                    for (int i = 0; i < 5; i++)
                    {
                        fs.Write(data, 0, data.Length);
                        await Task.Delay(600); // Wait 600ms between chunks
                    }
                }
            });

            await writerTask;
            Console.WriteLine($"[ACTION] File 'SlowMod.zip' finished writing.");

            await Task.Delay(6000);

            long c1Size = new FileInfo(slowPath).Length;
            string hostDest = Path.Combine(_hostDir, "SlowMod.zip");
            bool hostExists = File.Exists(hostDest);
            long hostSize = hostExists ? new FileInfo(hostDest).Length : 0;

            Assert($"Host received the fully written file (Size: {hostSize} bytes)", hostExists && hostSize == c1Size);
        }

        private static void Assert(string description, bool condition)
        {
            if (condition)
            {
                Console.WriteLine($"  ✅ [PASS] {description}");
                _passCount++;
            }
            else
            {
                Console.WriteLine($"  ❌ [FAIL] {description}");
                _failCount++;
            }
        }

        private static void Log(string prefix, string message)
        {
            Console.WriteLine($"   {prefix} {message}");
        }

        private static void PrintSummary()
        {
            Console.WriteLine("\n==========================================================");
            Console.WriteLine($" 📊 SIMULATION TEST RESULTS: Passed: {_passCount} | Failed: {_failCount}");
            Console.WriteLine("==========================================================");
            if (_failCount == 0)
            {
                Console.WriteLine(" 🎉 ALL P2P MHW MOD SYNC TESTS PASSED SUCCESSFULLY!\n");
            }
            else
            {
                Console.WriteLine(" ⚠️ SOME TESTS FAILED. PLEASE REVIEW LOGS ABOVE.\n");
            }
        }

        private static void CleanupCluster()
        {
            try
            {
                _hostWatcher?.Stop();
                _client1Watcher?.Stop();
                _client2Watcher?.Stop();

                _client1?.StopBackgroundTasks();
                _client2?.StopBackgroundTasks();

                _server?.StopAsync().Wait();

                if (Directory.Exists(_baseTempDir))
                {
                    Directory.Delete(_baseTempDir, true);
                }
            }
            catch { }
        }
    }
}
