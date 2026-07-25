using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ModTogetherUniversal
{
    public static class SnapshotHelper
    {
        public static void TakeSnapshot(Window window, string filePath)
        {
            var renderTargetBitmap = new RenderTargetBitmap(
                (int)window.ActualWidth,
                (int)window.ActualHeight,
                96, 96, PixelFormats.Pbgra32);
            renderTargetBitmap.Render(window);
            var pngImage = new PngBitmapEncoder();
            pngImage.Frames.Add(BitmapFrame.Create(renderTargetBitmap));
            using (var fileStream = File.Create(filePath))
            {
                pngImage.Save(fileStream);
            }
        }
    }
}
