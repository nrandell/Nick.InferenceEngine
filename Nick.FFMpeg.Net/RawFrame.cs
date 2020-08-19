using System;
using System.Threading;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class RawFrame : IDisposable
    {
        private bool disposedValue;

        public int Width => (Frame == null) ? -1 : Frame->width;
        public int Height => (Frame == null) ? -1 : Frame->height;
        public AVPixelFormat Format => (Frame == null) ? AVPixelFormat.AV_PIX_FMT_NONE : (AVPixelFormat)Frame->format;
        public AVFrame* Frame { get; private set; }

        public RawFrame() : this(ffmpeg.av_frame_alloc()) { }

        public RawFrame(AVFrame* frame)
        {
            Frame = frame;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                var frame = Frame;
                Frame = null;
                ffmpeg.av_frame_free(&frame);

                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~RawFrame()
#pragma warning restore MA0055 // Do not use destructor
        {
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
