using System;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class ImageConvert
    {
        internal static AVPixelFormat ConvertFormat(AVPixelFormat format)
        {
            return format switch
            {
                AVPixelFormat.AV_PIX_FMT_YUVJ411P => AVPixelFormat.AV_PIX_FMT_YUV411P,
                AVPixelFormat.AV_PIX_FMT_YUVJ420P => AVPixelFormat.AV_PIX_FMT_YUV420P,
                AVPixelFormat.AV_PIX_FMT_YUVJ422P => AVPixelFormat.AV_PIX_FMT_YUV422P,
                AVPixelFormat.AV_PIX_FMT_YUVJ440P => AVPixelFormat.AV_PIX_FMT_YUV440P,
                AVPixelFormat.AV_PIX_FMT_YUVJ444P => AVPixelFormat.AV_PIX_FMT_YUV444P,
                _ => format,
            };
        }

        public DecodedFrame Convert(RawFrame source, int targetWidth, int targetHeight, AVPixelFormat targetFormat, int algorithm = ffmpeg.SWS_BICUBIC, int align = 1)
        {
            var format = ConvertFormat(source.Format);
            var converterContext = ffmpeg.sws_getContext(source.Width, source.Height, format, targetWidth, targetHeight, targetFormat, algorithm, srcFilter: null, dstFilter: null, param: null);
            try
            {
                var targetBufferSize = ffmpeg.av_image_get_buffer_size(targetFormat, targetWidth, targetHeight, align);
                var targetBuffer = (byte*)ffmpeg.av_malloc((ulong)targetBufferSize);
                try
                {
                    var destData = new byte_ptrArray4();
                    var destLineSize = new int_array4();
                    var frame = source.Frame;
                    ffmpeg.av_image_fill_arrays(ref destData, ref destLineSize, targetBuffer, targetFormat, targetWidth, targetHeight, align).ThrowExceptionIfError(nameof(ffmpeg.av_image_fill_arrays));
                    ffmpeg.sws_scale(converterContext, frame->data, frame->linesize, 0, frame->height, destData, destLineSize).ThrowExceptionIfError(nameof(ffmpeg.sws_scale));
                    return new DecodedFrame(targetBuffer, targetBufferSize, targetWidth, targetHeight, targetFormat, destData, destLineSize, sharedBuffer: false);
                }
                catch (Exception)
                {
                    ffmpeg.av_free(targetBuffer);
                    throw;
                }
            }
            finally
            {
                ffmpeg.sws_freeContext(converterContext);
            }
        }
    }
}
