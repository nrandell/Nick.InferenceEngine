using System;
using System.Runtime.InteropServices;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public class FFMpegException : Exception
    {
        private static unsafe string? AvStrerror(int error)
        {
            const int bufferSize = 1024;
            Span<byte> buffer = stackalloc byte[bufferSize];
            fixed (byte* bp = buffer)
            {
                ffmpeg.av_strerror(error, bp, (ulong)bufferSize);
                return Marshal.PtrToStringAnsi((IntPtr)bp);
            }
        }

        public int Errno { get; }
        public string? Context { get; }

        public FFMpegException(string message) : base(message)
        {
        }

        public FFMpegException(int errno, string context) : base(FormattableString.Invariant($"FFMPeg error {errno}: {AvStrerror(errno)}"))
        {
            Errno = errno;
            Context = context;
        }

        public FFMpegException() : base()
        {
        }

        public FFMpegException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
