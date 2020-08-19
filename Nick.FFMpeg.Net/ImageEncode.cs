using System;
using System.IO;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class ImageEncode
    {
        public AVPixelFormat PixelFormat { get; }
        public int Width { get; }
        public int Height { get; }

        public ImageEncode(AVPixelFormat pixelFormat, int width, int height)
        {
            PixelFormat = pixelFormat;
            Width = width;
            Height = height;
        }

        public void Encode(IntPtr memory, AVPixelFormat pixelFormat, string file)
        {
            var codec = ffmpeg.avcodec_find_encoder(AVCodecID.AV_CODEC_ID_PNG);
            if (codec == null)
            {
                throw new ArgumentException("Failed to find codec", nameof(file));
            }

            var encoderContext = ffmpeg.avcodec_alloc_context3(codec);
            if (encoderContext == null)
            {
                throw new InvalidOperationException("Failed to allocate encoder context");
            }
            try
            {
                encoderContext->pix_fmt = PixelFormat;
                encoderContext->height = Height;
                encoderContext->width = Width;
                encoderContext->time_base = new AVRational { den = 1, num = 1 };

                ffmpeg.avcodec_open2(encoderContext, codec, options: null).ThrowExceptionIfError(nameof(ffmpeg.avcodec_open2));

                var packet = ffmpeg.av_packet_alloc();
                var frame = ffmpeg.av_frame_alloc();

                try
                {
                    ffmpeg.av_packet_unref(packet);
                    ffmpeg.av_frame_unref(frame);

                    frame->data[0] = (byte*)memory;
                    frame->width = Width;
                    frame->height = Height;
                    frame->linesize[0] = Width * 3;
                    frame->format = (int)pixelFormat;

                    ffmpeg.avcodec_send_frame(encoderContext, frame).ThrowExceptionIfError(nameof(ffmpeg.avcodec_send_frame));

                    ffmpeg.avcodec_receive_packet(encoderContext, packet);

                    using var output = File.Create(file);
                    var data = new ReadOnlySpan<byte>(packet->data, packet->size);
                    output.Write(data);
                }
                finally
                {
                    ffmpeg.av_frame_free(&frame);
                    ffmpeg.av_packet_free(&packet);
                }
            }
            finally
            {
                ffmpeg.avcodec_free_context(&encoderContext);
            }
        }
    }
}
