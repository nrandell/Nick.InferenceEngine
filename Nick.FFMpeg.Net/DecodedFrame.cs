using System;
using System.Threading;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class DecodedFrame : IDisposable
    {
        private static int _nextId = 0;

        public int Id { get; } = Interlocked.Increment(ref _nextId);

        private bool disposedValue;

        public byte_ptrArray4 DestData { get; }
        public int_array4 DestLineSize { get; }
        public byte* Buffer { get; }
        public int BufferSize { get; }
        public int Width { get; }
        public int Height { get; }
        public AVPixelFormat Format { get; }

        public Span<byte> AsSpan() => new Span<byte>(Buffer, BufferSize);

        public DecodedFrame(byte* buffer, int bufferSize, int width, int height, AVPixelFormat format, byte_ptrArray4 destData, int_array4 destLineSize)
        {
            Buffer = buffer;
            BufferSize = bufferSize;
            Width = width;
            Height = height;
            Format = format;
            DestData = destData;
            DestLineSize = destLineSize;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                ffmpeg.av_free(Buffer);
                disposedValue = true;
            }
        }

        ~DecodedFrame()
        {
            Console.WriteLine($"Finalizer for decoded frame {Id}");
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
