using System;
using System.Threading;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class RawFrame : IDisposable
    {
        private static int _nextId = 0;

        public int Id { get; } = Interlocked.Increment(ref _nextId);
        private bool disposedValue;

        public int Width { get; }
        public int Height { get; }
        public AVPixelFormat Format { get; }
        public AVFrame* Frame { get; private set; }

        public RawFrame(int width, int height, AVPixelFormat format, AVFrame* frame)
        {
            Width = width;
            Height = height;
            Format = format;
            Frame = frame;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }
                var frame = Frame;
                Frame = null;
                ffmpeg.av_frame_free(&frame);

                disposedValue = true;
            }
        }

        ~RawFrame()
        {
            Console.WriteLine($"Finalizer for raw frame {Id}");
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
