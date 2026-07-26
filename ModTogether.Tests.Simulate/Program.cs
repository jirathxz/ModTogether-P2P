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
                RunTest8_ModConflictDetection();

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
            File.Move(client1File, client1Recycle, true);
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
            File.Move(hostFile, hostRecycle, true);
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
            File.Move(hostRecycle, hostFile, true);
            
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
            File.Move(client1Recycle, client1File, true);
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
            File.Move(client2File, client2Recycle, true);
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
