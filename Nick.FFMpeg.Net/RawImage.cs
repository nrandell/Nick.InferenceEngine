using FFmpeg.AutoGen;

using Nick.Inference;

namespace Nick.FFMpeg.Net
{
    public class RawImage
    {
        public int Width { get; }
        public int Height { get; }
        public AVPixelFormat PixelFormat { get; }
        public NativeMemory Memory { get; }

        public RawImage(int width, int height, AVPixelFormat pixelFormat, NativeMemory memory)
        {
            Width = width;
            Height = height;
            PixelFormat = pixelFormat;
            Memory = memory;
        }
    }
}
