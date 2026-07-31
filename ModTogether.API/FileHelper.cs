using System;
using System.IO;

namespace ModTogether.API
{
    public static class FileHelper
    {
        public static void SafeMove(string source, string destination, bool overwrite = true, int maxRetries = 3)
        {
            if (!File.Exists(source)) return;

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    if (overwrite && File.Exists(destination))
                    {
                        File.Delete(destination);
                    }

                    File.Move(source, destination);
                    return; // Success
                }
                catch (IOException ex)
                {
                    int hResult = ex.HResult & 0xFFFF;
                    // ERROR_SHARING_VIOLATION (32) or ERROR_LOCK_VIOLATION (33)
                    if (hResult == 32 || hResult == 33)
                    {
                        if (i == maxRetries) throw;
                        System.Threading.Thread.Sleep(100 * (i + 1));
                        continue;
                    }

                    // Cross-drive move fallback
                    try
                    {
                        File.Copy(source, destination, overwrite);
                        File.Delete(source);
                        return; // Success
                    }
                    catch (IOException innerEx)
                    {
                        int innerHResult = innerEx.HResult & 0xFFFF;
                        if (innerHResult == 32 || innerHResult == 33)
                        {
                            if (i == maxRetries) throw;
                            System.Threading.Thread.Sleep(100 * (i + 1));
                            continue;
                        }
                        throw;
                    }
                }
            }
        }

        public static void SafeMoveDirectory(string source, string destination, int maxRetries = 3)
        {
            if (!Directory.Exists(source)) return;

            for (int i = 0; i <= maxRetries; i++)
            {
                try
                {
                    Directory.Move(source, destination);
                    return; // Success
                }
                catch (IOException ex)
                {
                    int hResult = ex.HResult & 0xFFFF;
                    if (hResult == 32 || hResult == 33)
                    {
                        if (i == maxRetries) throw;
                        System.Threading.Thread.Sleep(100 * (i + 1));
                        continue;
                    }

                    // Cross-drive directory move fallback
                    try
                    {
                        Directory.CreateDirectory(destination);
                        foreach (string dirPath in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
                        {
                            string relativePath = dirPath.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            Directory.CreateDirectory(Path.Combine(destination, relativePath));
                        }
                        foreach (string newPath in Directory.GetFiles(source, "*.*", SearchOption.AllDirectories))
                        {
                            string relativePath = newPath.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                            File.Copy(newPath, Path.Combine(destination, relativePath), true);
                        }
                        Directory.Delete(source, true);
                        return; // Success
                    }
                    catch (IOException innerEx)
                    {
                        int innerHResult = innerEx.HResult & 0xFFFF;
                        if (innerHResult == 32 || innerHResult == 33)
                        {
                            if (i == maxRetries) throw;
                            System.Threading.Thread.Sleep(100 * (i + 1));
                            continue;
                        }
                        throw;
                    }
                }
            }
        }
    }
}
