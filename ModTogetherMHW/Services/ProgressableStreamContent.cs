using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace ModTogetherMHW.Services
{
    public class ProgressableStreamContent : HttpContent
    {
        private readonly Stream _content;
        private readonly int _bufferSize;
        private readonly Action<int> _progress;

        public ProgressableStreamContent(Stream content, Action<int> progress, int bufferSize = 8192)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _progress = progress ?? throw new ArgumentNullException(nameof(progress));
            _bufferSize = bufferSize;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
        {
            var buffer = new byte[_bufferSize];
            long totalBytes = _content.Length;
            long uploadedBytes = 0;

            int bytesRead;
            while ((bytesRead = await _content.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await stream.WriteAsync(buffer, 0, bytesRead);
                uploadedBytes += bytesRead;
                if (totalBytes > 0)
                {
                    _progress?.Invoke((int)((uploadedBytes * 100) / totalBytes));
                }
            }
            _progress?.Invoke(100);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = _content.Length;
            return true;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _content.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
