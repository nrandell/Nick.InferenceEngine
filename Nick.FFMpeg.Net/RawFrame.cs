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

        public int Width => (Frame == null) ? -1 : Frame->width;
        public int Height => (Frame == null) ? -1 : Frame->height;
        public AVPixelFormat Format => (Frame == null) ? AVPixelFormat.AV_PIX_FMT_NONE : (AVPixelFormat)Frame->format;
        public AVFrame* Frame { get; private set; }

        public RawFrame(AVFrame* frame)
        {
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
