using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace VideoDetection
{
    public class ImageMarkup
    {
        private readonly string OutputDirectory = @"c:\temp\ssdoutputs";
        private int imageId = 0;
        private const int maxImages = 32;

        public async Task MarkupImage(Bitmap bitmap, IEnumerable<SSDProcessor.BoundingBox> boxes, CancellationToken ct)
        {
            using var graphics = Graphics.FromImage(bitmap);
            using var pen = new Pen(Brushes.Red)
            {
                Width = 8,
            };

            var width = bitmap.Width;
            var height = bitmap.Height;

            foreach (var box in boxes)
            {
                var xMin = width * box.XMin;
                var yMin = height * box.YMin;
                var xMax = width * box.XMax;
                var yMax = height * box.YMax;

                var rect = new Rectangle((int)xMin, (int)yMin, (int)(xMax - xMin), (int)(yMax - yMin));
                graphics.DrawRectangle(pen, rect);
            }

            var fileName = Path.Combine(OutputDirectory, FormattableString.Invariant($"img-{imageId++:D4}.png"));
            if (!Directory.Exists(OutputDirectory))
            {
                Directory.CreateDirectory(OutputDirectory);
            }

            if (imageId >= maxImages)
            {
                imageId = 0;
            }
            using var ms = new MemoryStream();
            bitmap.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            using var fs = File.Create(fileName);
            await ms.CopyToAsync(fs, ct);
        }
    }
}
