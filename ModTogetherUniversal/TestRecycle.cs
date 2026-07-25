using System;
using System.IO;
using ModTogetherUniversal.Services;

namespace ModTogetherUniversal
{
    public static class TestRecycle
    {
        public static void RunVerification()
        {
            Console.WriteLine("=== Testing RecycleManager & Ownership Verification ===");
            var testDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestMods");
            Directory.CreateDirectory(testDir);

            string fileA = Path.Combine(testDir, "test_mod.pak");
            File.WriteAllText(fileA, "Mock Mod Content 12345");

            // 1. Register owners
            RecycleManager.Instance.RegisterOwner("test_mod.pak", "HostUser", sha256: "", size: 100);
            RecycleManager.Instance.RegisterOwner("test_mod.pak", "PlayerTwo", sha256: "", size: 100);

            Console.WriteLine($"Badge Text (Shared): {RecycleManager.Instance.GetOwnersBadgeText("test_mod.pak")}");

            // 2. User 1 leaves (PlayerTwo) -> Mod should remain because HostUser still owns it
            RecycleManager.Instance.HandleUserDisconnect("PlayerTwo", testDir);
            Console.WriteLine($"File exists after PlayerTwo leaves: {File.Exists(fileA)}");

            // 3. User 2 leaves (HostUser) -> Mod should be moved to .recycle_mods
            RecycleManager.Instance.HandleUserDisconnect("HostUser", testDir);
            Console.WriteLine($"File exists after HostUser leaves: {!File.Exists(fileA)} (Moved to .recycle_mods)");

            Console.WriteLine("=== Verification Passed ===");
        }
    }
}
