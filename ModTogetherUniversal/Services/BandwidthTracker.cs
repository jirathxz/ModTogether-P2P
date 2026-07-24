using System;
using System.Threading;

namespace ModTogetherUniversal.Services
{
    public static class BandwidthTracker
    {
        private static long _bytesUploaded;
        private static long _bytesDownloaded;
        
        private static long _lastBytesUploaded;
        private static long _lastBytesDownloaded;
        
        public static long CurrentUploadSpeed { get; private set; }
        public static long CurrentDownloadSpeed { get; private set; }
        
        public static event Action<long, long>? OnSpeedUpdated;

        private static Timer? _timer;

        public static void Start()
        {
            if (_timer == null)
            {
                _timer = new Timer(UpdateSpeed, null, 1000, 1000);
            }
        }

        public static void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public static void AddUploadedBytes(long bytes)
        {
            Interlocked.Add(ref _bytesUploaded, bytes);
        }

        public static void AddDownloadedBytes(long bytes)
        {
            Interlocked.Add(ref _bytesDownloaded, bytes);
        }

        private static void UpdateSpeed(object? state)
        {
            long currentUp = Interlocked.Read(ref _bytesUploaded);
            long currentDown = Interlocked.Read(ref _bytesDownloaded);

            CurrentUploadSpeed = currentUp - _lastBytesUploaded;
            CurrentDownloadSpeed = currentDown - _lastBytesDownloaded;

            _lastBytesUploaded = currentUp;
            _lastBytesDownloaded = currentDown;

            OnSpeedUpdated?.Invoke(CurrentUploadSpeed, CurrentDownloadSpeed);
        }
    }
}
