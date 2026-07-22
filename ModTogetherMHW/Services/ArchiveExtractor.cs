using SharpCompress.Archives;
using SharpCompress.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;

namespace ModTogetherMHW.Services
{
    public class ArchiveExtractor
    {
        private static string? Get7ZipPath()
        {
            string[] paths = {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe"
            };
            return paths.FirstOrDefault(File.Exists);
        }

        private static string? GetWinRARPath()
        {
            string[] paths = {
                @"C:\Program Files\WinRAR\UnRAR.exe",
                @"C:\Program Files\WinRAR\Rar.exe",
                @"C:\Program Files\WinRAR\WinRAR.exe",
                @"C:\Program Files (x86)\WinRAR\UnRAR.exe",
                @"C:\Program Files (x86)\WinRAR\Rar.exe",
                @"C:\Program Files (x86)\WinRAR\WinRAR.exe"
            };
            return paths.FirstOrDefault(File.Exists);
        }

        private static System.Text.Encoding GetBestZipEncoding(string archivePath)
        {
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            
            // Try UTF-8 first
            try
            {
                using (var archive = ZipFile.OpenRead(archivePath))
                {
                    bool hasReplacementChar = false;
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.Contains('\uFFFD'))
                        {
                            hasReplacementChar = true;
                            break;
                        }
                    }
                    if (!hasReplacementChar) return System.Text.Encoding.UTF8;
                }
            }
            catch {}

            // Try Shift_JIS (Japanese MHW mods)
            try
            {
                var sjis = System.Text.Encoding.GetEncoding("Shift_JIS");
                using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Read, sjis))
                {
                    bool hasReplacementChar = false;
                    foreach (var entry in archive.Entries)
                    {
                        if (entry.FullName.Contains('\uFFFD'))
                        {
                            hasReplacementChar = true;
                            break;
                        }
                    }
                    if (!hasReplacementChar) return sjis;
                }
            }
            catch {}

            // Fallback to System Default CodePage
            return System.Text.Encoding.Default;
        }

        public static List<string> ExtractArchive(string archivePath, string destinationDirectory, Action<double>? progressCallback = null)
        {
            Directory.CreateDirectory(destinationDirectory);
            string ext = Path.GetExtension(archivePath).ToLower();

            // Try Native Zip
            if (ext == ".zip")
            {
                try
                {
                    return ExtractUsingNativeZip(archivePath, destinationDirectory, progressCallback);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Native Zip failed: " + ex.Message);
                }
            }

            // Try 7-Zip
            string? sevenZip = Get7ZipPath();
            if (sevenZip != null)
            {
                try
                {
                    return ExtractUsing7Zip(sevenZip, archivePath, destinationDirectory, progressCallback);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("7-Zip extraction failed: " + ex.Message);
                }
            }

            // Try WinRAR for .rar files
            string? winRar = GetWinRARPath();
            if (winRar != null && ext == ".rar")
            {
                try
                {
                    return ExtractUsingWinRAR(winRar, archivePath, destinationDirectory, progressCallback);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("WinRAR extraction failed: " + ex.Message);
                }
            }

            // Fallback to SharpCompress
            return ExtractUsingSharpCompress(archivePath, destinationDirectory, progressCallback);
        }

        public static List<string> GetArchiveContents(string archivePath)
        {
            string ext = Path.GetExtension(archivePath).ToLower();

            if (ext == ".zip")
            {
                try { return GetContentsUsingNativeZip(archivePath); } catch {}
            }

            string? sevenZip = Get7ZipPath();
            if (sevenZip != null)
            {
                try { return GetContentsUsing7Zip(sevenZip, archivePath); } catch {}
            }

            string? winRar = GetWinRARPath();
            if (winRar != null && ext == ".rar")
            {
                try { return GetContentsUsingWinRAR(winRar, archivePath); } catch {}
            }

            var contents = new List<string>();
            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                var options = new SharpCompress.Readers.ReaderOptions
                {
                    ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding
                    {
                        Default = System.Text.Encoding.GetEncoding("Shift_JIS")
                    }
                };
                
                if (ext == ".7z")
                {
                    using (var archive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(archivePath, options))
                    {
                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            if (entry.Key != null) contents.Add(entry.Key.Replace('\\', '/'));
                        }
                    }
                }
                else
                {
                    using (var archive = ArchiveFactory.Open(archivePath, options))
                    {
                        foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                        {
                            if (entry.Key != null) contents.Add(entry.Key.Replace('\\', '/'));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                contents.Add($"[Error reading archive: {ex.Message}]");
            }
            return contents;
        }

        private static List<string> ExtractUsingNativeZip(string archivePath, string destinationDirectory, Action<double>? progressCallback)
        {
            var extractedFiles = new List<string>();
            var encoding = GetBestZipEncoding(archivePath);

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Read, encoding))
            {
                int total = archive.Entries.Count;
                int current = 0;
                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) { current++; continue; } // Directory
                    
                    var entryKey = entry.FullName.Replace('\\', '/');
                    var fullDestPath = Path.GetFullPath(Path.Combine(destinationDirectory, entryKey));
                    if (!fullDestPath.StartsWith(Path.GetFullPath(destinationDirectory))) { current++; continue; }

                    Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath)!);
                    entry.ExtractToFile(fullDestPath, true);
                    extractedFiles.Add(fullDestPath);
                    current++;
                    
                    if (progressCallback != null && total > 0)
                        progressCallback.Invoke((double)current / total * 100);
                }
            }
            return extractedFiles;
        }

        private static List<string> ExtractUsing7Zip(string exePath, string archivePath, string destinationDirectory, Action<double>? progressCallback)
        {
            var p = new Process();
            p.StartInfo.FileName = exePath;
            p.StartInfo.Arguments = $"x -scsUTF-8 \"{archivePath}\" -o\"{destinationDirectory}\" -y -bsp1";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            p.StartInfo.CreateNoWindow = true;

            p.OutputDataReceived += (s, e) =>
            {
                if (e.Data != null && progressCallback != null)
                {
                    var match = Regex.Match(e.Data, @"(\d+)%");
                    if (match.Success && double.TryParse(match.Groups[1].Value, out double pct))
                    {
                        progressCallback.Invoke(pct);
                    }
                }
            };

            p.Start();
            p.BeginOutputReadLine();
            p.WaitForExit();

            if (p.ExitCode != 0) throw new Exception($"7z exited with code {p.ExitCode}");

            return Directory.GetFiles(destinationDirectory, "*.*", SearchOption.AllDirectories).ToList();
        }

        private static List<string> ExtractUsingWinRAR(string exePath, string archivePath, string destinationDirectory, Action<double>? progressCallback)
        {
            var p = new Process();
            p.StartInfo.FileName = exePath;
            p.StartInfo.Arguments = $"x -y \"{archivePath}\" * \"{destinationDirectory}\\\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            
            p.Start();
            p.WaitForExit();

            if (p.ExitCode != 0) throw new Exception($"WinRAR exited with code {p.ExitCode}");
            if (progressCallback != null) progressCallback.Invoke(100.0);

            return Directory.GetFiles(destinationDirectory, "*.*", SearchOption.AllDirectories).ToList();
        }

        private static List<string> ExtractUsingSharpCompress(string archivePath, string destinationDirectory, Action<double>? progressCallback)
        {
            var extractedFiles = new List<string>();
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            var options = new SharpCompress.Readers.ReaderOptions
            {
                ArchiveEncoding = new SharpCompress.Common.ArchiveEncoding
                {
                    Default = System.Text.Encoding.GetEncoding("Shift_JIS")
                }
            };
            
            string ext = Path.GetExtension(archivePath).ToLower();
            
            try 
            {
                if (ext == ".7z")
                {
                    using (var archive = SharpCompress.Archives.SevenZip.SevenZipArchive.Open(archivePath, options))
                    {
                        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                        int total = entries.Count;
                        int current = 0;
                        
                        foreach (var entry in entries)
                        {
                            var entryKey = entry.Key?.Replace('\\', '/') ?? "unknown";
                            var fullDestPath = Path.GetFullPath(Path.Combine(destinationDirectory, entryKey));
                            if (!fullDestPath.StartsWith(Path.GetFullPath(destinationDirectory))) { current++; continue; }

                            Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath)!);

                            using (var entryStream = entry.OpenEntryStream())
                            using (var fileStream = File.Create(fullDestPath))
                            {
                                entryStream.CopyTo(fileStream);
                            }

                            extractedFiles.Add(fullDestPath);
                            current++;
                            
                            if (progressCallback != null && total > 0)
                                progressCallback.Invoke((double)current / total * 100);
                        }
                    }
                }
                else
                {
                    using (var archive = ArchiveFactory.Open(archivePath, options))
                    {
                        var entries = archive.Entries.Where(e => !e.IsDirectory).ToList();
                        int total = entries.Count;
                        int current = 0;
                        
                        foreach (var entry in entries)
                        {
                            var entryKey = entry.Key?.Replace('\\', '/') ?? "unknown";
                            var fullDestPath = Path.GetFullPath(Path.Combine(destinationDirectory, entryKey));
                            if (!fullDestPath.StartsWith(Path.GetFullPath(destinationDirectory))) { current++; continue; }

                            Directory.CreateDirectory(Path.GetDirectoryName(fullDestPath)!);

                            using (var entryStream = entry.OpenEntryStream())
                            using (var fileStream = File.Create(fullDestPath))
                            {
                                entryStream.CopyTo(fileStream);
                            }

                            extractedFiles.Add(fullDestPath);
                            current++;
                            
                            if (progressCallback != null && total > 0)
                                progressCallback.Invoke((double)current / total * 100);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SharpCompress extraction failed: {ex.Message}");
            }
            return extractedFiles;
        }

        private static List<string> GetContentsUsingNativeZip(string archivePath)
        {
            var contents = new List<string>();
            var encoding = GetBestZipEncoding(archivePath);

            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Read, encoding))
            {
                foreach (var entry in archive.Entries)
                {
                    if (!string.IsNullOrEmpty(entry.Name))
                        contents.Add(entry.FullName.Replace('\\', '/'));
                }
            }
            return contents;
        }

        private static List<string> GetContentsUsing7Zip(string exePath, string archivePath)
        {
            var contents = new List<string>();
            var p = new Process();
            p.StartInfo.FileName = exePath;
            p.StartInfo.Arguments = $"l -slt -scsUTF-8 \"{archivePath}\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.StandardOutputEncoding = System.Text.Encoding.UTF8;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0) throw new Exception("7z failed");

            bool startedParsingFiles = false;
            string? currentPath = null;
            bool isDirectory = false;

            foreach (string line in output.Split('\n'))
            {
                string t = line.Trim();
                
                if (!startedParsingFiles)
                {
                    if (t.StartsWith("----------"))
                    {
                        startedParsingFiles = true;
                    }
                    continue;
                }

                if (t.StartsWith("Path = "))
                {
                    // Save previous entry if valid
                    if (currentPath != null && !isDirectory)
                    {
                        contents.Add(currentPath);
                    }
                    
                    currentPath = t.Substring(7).Replace('\\', '/');
                    isDirectory = false;
                }
                else if (t.StartsWith("Folder = +") || t.StartsWith("Attributes = D"))
                {
                    isDirectory = true;
                }
            }
            
            // Don't forget the last entry
            if (currentPath != null && !isDirectory)
            {
                contents.Add(currentPath);
            }

            return contents;
        }

        private static List<string> GetContentsUsingWinRAR(string exePath, string archivePath)
        {
            var contents = new List<string>();
            var p = new Process();
            p.StartInfo.FileName = exePath;
            p.StartInfo.Arguments = $"lb \"{archivePath}\"";
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.StandardOutputEncoding = System.Text.Encoding.Default;
            p.StartInfo.CreateNoWindow = true;
            p.Start();

            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            if (p.ExitCode != 0) throw new Exception("WinRAR failed");

            foreach (string line in output.Split('\n'))
            {
                string t = line.Trim().Replace('\\', '/');
                if (!string.IsNullOrEmpty(t) && !t.EndsWith("/"))
                {
                    contents.Add(t);
                }
            }

            // Remove entries that are directories (prefixes of other entries)
            contents.Sort(StringComparer.OrdinalIgnoreCase);
            var filtered = new List<string>();
            for (int i = 0; i < contents.Count; i++)
            {
                if (i < contents.Count - 1 && contents[i + 1].StartsWith(contents[i] + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // It's a directory
                }
                filtered.Add(contents[i]);
            }

            return filtered;
        }
    }
}
