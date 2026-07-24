using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ModTogetherUniversal.Services
{
    public class ModServer
    {
        private WebApplication? _app;
        private CancellationTokenSource? _cts;
        
        public string HostDir { get; private set; } = string.Empty;
        public string RoomToken { get; private set; } = string.Empty;
        public string HostUsername { get; set; } = "Host";
        public bool IsRunning => _app != null;

        public ConcurrentDictionary<string, UserHealthState> ActiveUsers { get; } = new();
        public ConcurrentBag<string> KickedUsers { get; } = new();
        public ConcurrentDictionary<string, string> DeletedMods { get; } = new();

        public event Action<string>? OnLog;
        public event Action? OnCacheRefreshRequested;
        public event Action<string>? OnModDownloaded;
        public event Action<string>? OnModDeleted;
        
        public void TriggerCacheRefresh() => OnCacheRefreshRequested?.Invoke();

        public async Task StartAsync(string hostDir, int port, string roomToken)
        {
            HostDir = hostDir;
            RoomToken = roomToken;
            ActiveUsers.Clear();
            KickedUsers.Clear();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
            builder.Logging.ClearProviders(); // Disable default logging for cleaner output

            _app = builder.Build();

            // Middleware for Authentication
            _app.Use(async (context, next) =>
            {
                if (context.Request.Path != "/docs")
                {
                    if (!context.Request.Headers.TryGetValue("x-app-type", out var appType) || appType != "MHW_SPECIAL")
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsJsonAsync(new { detail = "App Version Mismatch" });
                        return;
                    }

                    if (!context.Request.Headers.TryGetValue("x-room-token", out var token) || token != RoomToken)
                    {
                        context.Response.StatusCode = 401;
                        await context.Response.WriteAsJsonAsync(new { detail = "Unauthorized" });
                        return;
                    }
                }
                await next(context);
            });

            // Endpoints
            _app.MapPost("/heartbeat", (string username, bool? isSynced, int? syncProgress, string? currentActivity) =>
            {
                if (KickedUsers.Contains(username)) return Results.Json(new { status = "kicked" });
                
                var state = ActiveUsers.GetOrAdd(username, _ => new UserHealthState());
                state.LastSeen = DateTime.UtcNow;
                if (isSynced.HasValue) state.IsSynced = isSynced.Value;
                if (syncProgress.HasValue) state.SyncProgress = syncProgress.Value;
                if (currentActivity != null) state.CurrentActivity = currentActivity;
                
                return Results.Json(new { status = "ok" });
            });

            _app.MapPost("/kick", (HttpRequest request) =>
            {
                string target = request.Form["target"].ToString();
                if (!string.IsNullOrEmpty(target))
                {
                    KickedUsers.Add(target);
                    ActiveUsers.TryRemove(target, out _);
                    OnLog?.Invoke($"🚫 Kicked user: {target}");
                }
                return Results.Json(new { status = "ok" });
            });

            _app.MapGet("/mods", () =>
            {
                var mods = new Dictionary<string, long>();
                if (Directory.Exists(HostDir))
                {
                    var files = Directory.GetFiles(HostDir, "*.*", SearchOption.AllDirectories)
                        .Where(f => f.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".7z", StringComparison.OrdinalIgnoreCase) || 
                                    f.EndsWith(".rar", StringComparison.OrdinalIgnoreCase));
                                    
                    foreach (var file in files)
                    {
                        var relPath = Path.GetRelativePath(HostDir, file).Replace("\\", "/");
                        if (!relPath.StartsWith(".recycle_mods") && !file.EndsWith(".tmp") && !IsDuplicateDownload(file))
                        {
                            mods[relPath] = new FileInfo(file).Length;
                        }
                    }
                }

                var activeUsersList = ActiveUsers.Select(kvp => new UserSyncState 
                { 
                    Username = kvp.Key, 
                    IsSynced = kvp.Value.IsSynced, 
                    SyncProgress = kvp.Value.SyncProgress,
                    CurrentActivity = kvp.Value.CurrentActivity
                }).ToList();
                
                activeUsersList.Add(new UserSyncState { Username = $"{HostUsername} (Host)", IsSynced = true, SyncProgress = 100, CurrentActivity = "" });

                return Results.Json(new 
                { 
                    mods, 
                    deleted_mods = DeletedMods.ToDictionary(kvp => kvp.Key, kvp => kvp.Value), 
                    active_users = activeUsersList 
                });
            });

            _app.MapPost("/delete", (HttpRequest request) =>
            {
                string relPath = request.Form["rel_path"].ToString();
                string username = request.Form["username"].ToString() ?? "Someone";

                var filePath = GetSafePath(HostDir, relPath);
                if (File.Exists(filePath))
                {
                    var recyclePath = Path.Combine(HostDir, ".recycle_mods", relPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(recyclePath)!);
                    try
                    {
                        File.Move(filePath, recyclePath, true);
                    }
                    catch { /* Ignore errors */ }
                }

                DeletedMods.TryAdd(relPath, username);
                OnLog?.Invoke($"[🗑️] {username} deleted mod: {relPath}");
                OnModDeleted?.Invoke(relPath);
                OnCacheRefreshRequested?.Invoke();

                return Results.Json(new { status = "success" });
            });

            _app.MapPost("/upload", async (HttpRequest request) =>
            {
                if (!request.HasFormContentType || request.Form.Files.Count == 0)
                    return Results.BadRequest("No file uploaded");

                var file = request.Form.Files[0];
                string relPath = request.Form["rel_path"].ToString();
                string username = request.Form["username"].ToString() ?? "Someone";

                var filePath = GetSafePath(HostDir, relPath);
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

                using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite))
                {
                    using var trackingStream = new TrackingStream(stream, BandwidthTracker.AddDownloadedBytes);
                    await file.CopyToAsync(trackingStream);
                }

                // Remove from deleted mods if it was there
                DeletedMods.TryRemove(relPath, out _);

                OnLog?.Invoke($"[📥] Mod restored/uploaded by {username}: {relPath}");
                OnModDownloaded?.Invoke(relPath);
                OnCacheRefreshRequested?.Invoke();

                return Results.Json(new { status = "success" });
            });

            _app.MapGet("/download/{*filepath}", (string filepath) =>
            {
                var filePath = GetSafePath(HostDir, filepath);
                if (!File.Exists(filePath)) return Results.NotFound();
                
                var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var trackingStream = new TrackingStream(fileStream, BandwidthTracker.AddUploadedBytes);
                return Results.Stream(trackingStream, "application/octet-stream", Path.GetFileName(filePath));
            });

            _cts = new CancellationTokenSource();
            await _app.StartAsync(_cts.Token);
            OnLog?.Invoke($"Host Server started on port {port}");
        }

        public async Task StopAsync()
        {
            if (_app != null && _cts != null)
            {
                _cts.Cancel();
                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
            }
        }

        private bool IsDuplicateDownload(string filename)
        {
            return System.Text.RegularExpressions.Regex.IsMatch(filename, @" \(\d+\)(\.[a-zA-Z0-9]+)?$");
        }

        private string GetSafePath(string baseDir, string relPath)
        {
            var cleanRelPath = relPath.TrimStart('/', '\\');
            var fullPath = Path.GetFullPath(Path.Combine(baseDir, cleanRelPath));
            var absBaseDir = Path.GetFullPath(baseDir);
            
            if (!fullPath.StartsWith(absBaseDir))
            {
                throw new Exception("Invalid path traversal detected.");
            }
            return fullPath;
        }
    }

    public class UserHealthState
    {
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public bool IsSynced { get; set; } = false;
        public int SyncProgress { get; set; } = 0;
        public string CurrentActivity { get; set; } = "";
    }
}
