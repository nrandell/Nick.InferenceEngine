using System;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public static class Helper
    {
        public static void Initialise() { }

        static Helper()
        {
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                ffmpeg.RootPath = @"c:\utils\ffmpeg\bin";
            }
            ffmpeg.avdevice_register_all();
            //ffmpeg.av_log_set_level(255);
        }

        public static int ThrowExceptionIfError(this int error, string context)
        {
            if (error < 0)
            {
                throw new FFMpegException(error, context);
            }
            return error;
        }
    }
}
