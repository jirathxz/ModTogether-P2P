using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;

namespace ModTogetherMHW.Services
{
    public class ModFileWatcher : IDisposable
    {
        private FileSystemWatcher? _watcher;
        private readonly ModClient _client;
        private string _localDir = "";
        
        public bool IsPaused { get; set; } = false;
        
        private readonly ConcurrentDictionary<string, DateTime> _recentlyProcessed = new();
        private Timer? _cleanupTimer;

        public ModFileWatcher(ModClient client)
        {
            _client = client;
            _cleanupTimer = new Timer(CleanupProcessedCache, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        public void Start(string path)
        {
            Stop();
            _localDir = path;
            Directory.CreateDirectory(path);

            _watcher = new FileSystemWatcher(path)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                IncludeSubdirectories = true,
                EnableRaisingEvents = true
            };

            _watcher.Created += OnCreated;
            _watcher.Deleted += OnDeleted;
            _watcher.Renamed += OnRenamed;
        }

        public void Stop()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Dispose();
                _watcher = null;
            }
        }

        private async void OnCreated(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (IsPaused || ShouldIgnore(e.FullPath)) return;

                string relPath = Path.GetRelativePath(_localDir, e.FullPath).Replace("\\", "/");
                if (relPath.StartsWith(".recycle_mods")) return;

                if (!Debounce(relPath)) return;

                // Wait non-blockingly to ensure file is fully written
                await Task.Delay(500);

                await _client.UploadModAsync(e.FullPath, relPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FileWatcher OnCreated error: {ex.Message}");
            }
        }

        private async void OnDeleted(object sender, FileSystemEventArgs e)
        {
            try
            {
                if (IsPaused || ShouldIgnore(e.FullPath)) return;

                string relPath = Path.GetRelativePath(_localDir, e.FullPath).Replace("\\", "/");
                if (relPath.StartsWith(".recycle_mods")) return;
                
                if (!Debounce(relPath + "_del")) return;

                await _client.DeleteModAsync(relPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FileWatcher OnDeleted error: {ex.Message}");
            }
        }

        private async void OnRenamed(object sender, RenamedEventArgs e)
        {
            try
            {
                if (IsPaused) return;

                // Handle old file deletion
                if (!ShouldIgnore(e.OldFullPath))
                {
                    string oldRel = Path.GetRelativePath(_localDir, e.OldFullPath).Replace("\\", "/");
                    if (!oldRel.StartsWith(".recycle_mods") && Debounce(oldRel + "_del"))
                    {
                        await _client.DeleteModAsync(oldRel);
                    }
                }

                // Handle new file creation
                if (!ShouldIgnore(e.FullPath))
                {
                    string newRel = Path.GetRelativePath(_localDir, e.FullPath).Replace("\\", "/");
                    if (!newRel.StartsWith(".recycle_mods") && Debounce(newRel))
                    {
                        await Task.Delay(500);
                        await _client.UploadModAsync(e.FullPath, newRel);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FileWatcher OnRenamed error: {ex.Message}");
            }
        }

        private bool ShouldIgnore(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase)) return true;
            if (Regex.IsMatch(fileName, @" \(\d+\)(\.[a-zA-Z0-9]+)?$")) return true; // Duplicate download

            return !(fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) ||
                     fileName.EndsWith(".rar", StringComparison.OrdinalIgnoreCase));
        }

        private bool Debounce(string key)
        {
            if (_recentlyProcessed.TryGetValue(key, out var time))
            {
                if ((DateTime.UtcNow - time).TotalSeconds < 2) return false;
            }
            _recentlyProcessed[key] = DateTime.UtcNow;
            return true;
        }

        private void CleanupProcessedCache(object? state)
        {
            var now = DateTime.UtcNow;
            foreach (var key in _recentlyProcessed.Keys)
            {
                if (_recentlyProcessed.TryGetValue(key, out var time) && (now - time).TotalMinutes > 1)
                {
                    _recentlyProcessed.TryRemove(key, out _);
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _cleanupTimer?.Dispose();
        }
    }
}
