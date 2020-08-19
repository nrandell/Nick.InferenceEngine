using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

using Nick.InferenceEngine.Net;

namespace MultiVideoDetection
{
    public class SSDProcessor
    {
        private static readonly string[] _labels = ReadLabels();

        private static string[] ReadLabels()
        {
            var asm = Assembly.GetExecutingAssembly();
            var names = asm.GetManifestResourceNames();
            using var stream = asm.GetManifestResourceStream(names.Single(n => n.Contains("labels.txt", StringComparison.OrdinalIgnoreCase)));
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                var lines = new List<string>();
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    lines.Add(line);
                }
                return lines.ToArray();
            }

            return Array.Empty<string>();
        }

        public class BoundingBox
        {
            public int ImageId { get; }
            public float Confidence { get; }
            public int Label { get; }
            public float XMin { get; }
            public float YMin { get; }
            public float XMax { get; }
            public float YMax { get; }
            public string TextLabel
            {
                get
                {
                    var index = Label - 1;
                    if (index < _labels.Length)
                    {
                        return _labels[index];
                    }

                    return "unknown";
                }
            }

            public BoundingBox(int imageId, float confidence, int label, float xMin, float yMin, float xMax, float yMax)
            {
                ImageId = imageId;
                Confidence = confidence;
                Label = label;
                XMin = xMin;
                YMin = yMin;
                XMax = xMax;
                YMax = yMax;
            }

            public override string ToString() => FormattableString.Invariant($"{ImageId} @ {Confidence} [{Label} \"{TextLabel}\"] {XMin},{YMin} to {XMax},{YMax}");
        }

        public IReadOnlyCollection<BoundingBox> ProcessOutput(Blob outputBlob)
        {
            var span = outputBlob.AsSpan<float>();
            var dims = outputBlob.Dimensions;
            var maxProposalCount = (int)dims[2];
            var objectSize = (int)dims[3];

            var results = new List<BoundingBox>(maxProposalCount);

            for (var i = 0; i < maxProposalCount; i++)
            {
                var offset = i * objectSize;
                var imageId = (int)span[offset];
                if (imageId < 0)
                {
                    break;
                }

                results.Add(new BoundingBox(
                        imageId,
                        span[offset + 2],
                        (int)span[offset + 1],
                        span[offset + 3],
                        span[offset + 4],
                        span[offset + 5],
                        span[offset + 6]
                    ));
            }

            return results;
        }
    }
}
