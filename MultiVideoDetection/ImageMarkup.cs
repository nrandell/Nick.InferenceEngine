using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MultiVideoDetection
{
    public class ImageMarkup
    {
        private static readonly JsonSerializerOptions _options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };

        private const string OutputDirectory = @"c:\temp\multiplessdoutputs";
        private const int maxImages = 256;
        private class IntRef
        {
            public int Value { get; set; }
        }

        private readonly ConcurrentDictionary<int, IntRef> _imageIds = new ConcurrentDictionary<int, IntRef>();

        private int GetImageId(int frameIndex)
        {
            if (!_imageIds.TryGetValue(frameIndex, out var idRef))
            {
                idRef = new IntRef();
                _imageIds.TryAdd(frameIndex, idRef);
            }
            var nextId = idRef.Value;
            idRef.Value = (idRef.Value + 1) % maxImages;
            return nextId;
        }

        public async Task MarkupImage(int frameIndex, Bitmap bitmap, IEnumerable<SSDProcessor.BoundingBox> boxes, CancellationToken ct)
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

            var imageId = GetImageId(frameIndex);
            var fileName = Path.Combine(OutputDirectory, FormattableString.Invariant($"{frameIndex:D4}"), FormattableString.Invariant($"{imageId++:D4}.png"));
            var directory = Path.GetDirectoryName(fileName);
            if ((directory != null) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var ms = new MemoryStream())
            {
                bitmap.Save(ms, ImageFormat.Png);
                ms.Position = 0;
                using var fs = File.Create(fileName);
                await ms.CopyToAsync(fs, ct);
            }

            var infoFileName = Path.ChangeExtension(fileName, ".json");
            using var infoFs = File.Create(infoFileName);
            await JsonSerializer.SerializeAsync(infoFs, boxes, _options, ct);
        }
    }
}
