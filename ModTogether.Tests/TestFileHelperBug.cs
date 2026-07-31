using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ModTogether.API;

namespace ModTogether.Tests
{
    public static class TestFileHelperBug
    {
        public static bool RunTest()
        {
            Console.WriteLine("=========================================================");
            Console.WriteLine(" [TEST] FileHelper Resource Locking & Path Replacement");
            Console.WriteLine("=========================================================");

            string testRoot = Path.Combine(Path.GetTempPath(), "ModTogether_FileHelperTest_" + Guid.NewGuid().ToString().Substring(0, 6));
            Directory.CreateDirectory(testRoot);

            bool check1 = false;
            bool check2 = false;

            try
            {
                Console.WriteLine("\n--- TEST 1: Path Replacement Bug ---");
                string sourceDir = Path.Combine(testRoot, "mod");
                string destDir = Path.Combine(testRoot, "backup");
                Directory.CreateDirectory(sourceDir);
                
                string fileName = "modern_ui.pak";
                File.WriteAllText(Path.Combine(sourceDir, fileName), "MOCK DATA");

                FileHelper.SafeMoveDirectory(sourceDir, destDir, maxRetries: 0);
                
                check1 = File.Exists(Path.Combine(destDir, "modern_ui.pak"));
                Console.WriteLine($"[CHECKLIST 1] Path corruption avoided (modern_ui.pak exists): {(check1 ? "PASSED ✅" : "FAILED ❌")}");

                Console.WriteLine("\n--- TEST 2: Resource Locking Retry (Concurrent Access) ---");
                string srcFile = Path.Combine(destDir, "modern_ui.pak");
                string dstFile = Path.Combine(testRoot, "moved_ui.pak");

                Task.Run(() => {
                    try
                    {
                        using (var fs = new FileStream(srcFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        {
                            Thread.Sleep(300);
                        }
                    }
                    catch { }
                });

                Thread.Sleep(50); 

                FileHelper.SafeMove(srcFile, dstFile, true, 3);
                
                check2 = File.Exists(dstFile);
                Console.WriteLine($"[CHECKLIST 2] SafeMove retried and succeeded after lock released: {(check2 ? "PASSED ✅" : "FAILED ❌")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Test Failed with Exception: {ex.Message}");
            }
            finally
            {
                try { Directory.Delete(testRoot, true); } catch { }
            }

            bool allPassed = check1 && check2;
            Console.WriteLine("\n=========================================================");
            Console.WriteLine(allPassed ? " ✅ ALL FILEHELPER TESTS PASSED SUCCESSFULLY! " : " ❌ SOME TESTS FAILED! ");
            Console.WriteLine("=========================================================");
            return allPassed;
        }
    }
}
