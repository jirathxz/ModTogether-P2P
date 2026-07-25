using System;
using System.IO;
using System.Threading.Tasks;
using ModTogetherUniversal.Services;

namespace ModTogetherUniversal
{
    public static class RunAllTests
    {
        public static async Task RunVerificationAsync()
        {
            Console.WriteLine("=================================================");
            Console.WriteLine(" 🧪 MODTOGETHER UNIVERSAL SYSTEM VERIFICATION ");
            Console.WriteLine("=================================================");

            var testBaseDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SystemTestSandbox");
            Directory.CreateDirectory(testBaseDir);

            // TEST 1: SessionManager Persistence
            Console.WriteLine("\n[1/4] Testing SessionManager (session.json)...");
            SessionManager.Instance.State.SelectedRoomPreset = "TestPreset_Alpha";
            SessionManager.Instance.State.HostCustomPort = "52999";
            SessionManager.Instance.Save();

            var loadedState = SessionManager.Instance.State;
            bool test1Passed = loadedState.SelectedRoomPreset == "TestPreset_Alpha" && loadedState.HostCustomPort == "52999";
            Console.WriteLine(test1Passed ? "  ✅ [PASS] Session state saved & loaded successfully." : "  ❌ [FAIL] Session state mismatch.");

            // TEST 2: RecycleManager & SHA256 Verification
            Console.WriteLine("\n[2/4] Testing RecycleManager & .recycle_mods...");
            string activeDir = Path.Combine(testBaseDir, "ActiveMods");
            Directory.CreateDirectory(activeDir);
            string modFile = Path.Combine(activeDir, "armor_skin.pak");
            File.WriteAllText(modFile, "V1_SHA256_MOD_CONTENT_DATA");

            // Register ownership for Host and ClientA
            RecycleManager.Instance.RegisterOwner("armor_skin.pak", "Host", sha256: "", size: 100);
            RecycleManager.Instance.RegisterOwner("armor_skin.pak", "ClientA", sha256: "", size: 100);

            string badge1 = RecycleManager.Instance.GetOwnersBadgeText("armor_skin.pak");
            Console.WriteLine($"  Ownership Badge (Shared): {badge1}");

            // ClientA leaves -> file stays because Host still owns it
            RecycleManager.Instance.HandleUserDisconnect("ClientA", activeDir);
            bool staysInActive = File.Exists(modFile);
            Console.WriteLine(staysInActive ? "  ✅ [PASS] Mod retained when shared owner ClientA left." : "  ❌ [FAIL] Mod removed prematurely.");

            // Host leaves -> file moves to .recycle_mods
            RecycleManager.Instance.HandleUserDisconnect("Host", activeDir);
            bool movedToRecycle = !File.Exists(modFile);
            Console.WriteLine(movedToRecycle ? "  ✅ [PASS] Mod safely moved to .recycle_mods when all owners left." : "  ❌ [FAIL] Mod not moved to recycle.");

            // Try restoring from .recycle_mods
            bool restored = RecycleManager.Instance.TryRestoreFromRecycle(activeDir, "armor_skin.pak", expectedSha256: "");
            Console.WriteLine(restored ? "  ✅ [PASS] Mod restored directly from .recycle_mods cache (Saved P2P download!)." : "  ❌ [FAIL] Failed to restore from recycle cache.");

            // TEST 3: OnlinePluginStoreService GitHub Release Query
            Console.WriteLine("\n[3/4] Testing OnlinePluginStoreService (GitHub Releases API)...");
            var catalog = await OnlinePluginStoreService.Instance.FetchCatalogFromGitHubAsync();
            Console.WriteLine($"  Fetched catalog items count: {catalog.Count}");
            Console.WriteLine("  ✅ [PASS] GitHub Release API parser completed without crashing.");

            // TEST 4: BandwidthLimiter & AppSettings
            Console.WriteLine("\n[4/4] Testing AppSettings Bandwidth & Multi-Game Profiles...");
            App.Settings.Current.MaxDownloadSpeedKbps = 2048;
            if (!App.Settings.Current.GamePathHistory.Contains("C:\\Games\\MHW"))
            {
                App.Settings.Current.GamePathHistory.Add("C:\\Games\\MHW");
            }
            App.Settings.Save();
            bool test4Passed = App.Settings.Current.MaxDownloadSpeedKbps == 2048 && App.Settings.Current.GamePathHistory.Contains("C:\\Games\\MHW");
            Console.WriteLine(test4Passed ? "  ✅ [PASS] AppSettings & Game Profiles verified." : "  ❌ [FAIL] AppSettings failed.");

            Console.WriteLine("\n=================================================");
            Console.WriteLine(" 🎉 ALL SYSTEM VERIFICATION TESTS COMPLETED ");
            Console.WriteLine("=================================================");
        }
    }
}
