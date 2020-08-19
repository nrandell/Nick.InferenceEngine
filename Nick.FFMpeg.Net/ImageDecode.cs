
using System;

using FFmpeg.AutoGen;
namespace Nick.FFMpeg.Net
{
    public unsafe class ImageDecode
    {
        static ImageDecode() => Helper.Initialise();

        public void DecodeRaw(string file, RawFrame rawFrame)
        {
            AVFormatContext* inputContext = null;
            ffmpeg.avformat_open_input(&inputContext, file, fmt: null, options: null).ThrowExceptionIfError(nameof(ffmpeg.avformat_open_input));
            try
            {
                ffmpeg.avformat_find_stream_info(inputContext, options: null).ThrowExceptionIfError(nameof(ffmpeg.avformat_find_stream_info));

                AVCodec* decoder = null;
                var stream = ffmpeg.av_find_best_stream(inputContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0).ThrowExceptionIfError(nameof(ffmpeg.av_find_best_stream));

                var decoderContext = ffmpeg.avcodec_alloc_context3(decoder);
                try
                {
                    var video = inputContext->streams[stream];

                    ffmpeg.avcodec_parameters_to_context(decoderContext, video->codecpar).ThrowExceptionIfError(nameof(ffmpeg.avcodec_parameters_to_context));

                    ffmpeg.avcodec_open2(decoderContext, decoder, options: null).ThrowExceptionIfError(nameof(ffmpeg.avcodec_open2));

                    var width = video->codec->width;
                    var height = video->codec->height;
                    var format = video->codec->pix_fmt;

                    var packet = ffmpeg.av_packet_alloc();
                    try
                    {
                        ffmpeg.av_packet_unref(packet);

                        ffmpeg.av_read_frame(inputContext, packet).ThrowExceptionIfError(nameof(ffmpeg.av_read_frame));

                        ffmpeg.avcodec_send_packet(decoderContext, packet).ThrowExceptionIfError(nameof(ffmpeg.avcodec_send_packet));

                        var frame = rawFrame.Frame;
                        ffmpeg.av_frame_unref(frame);
                        ffmpeg.avcodec_receive_frame(decoderContext, frame);
                        return;
                    }
                    finally
                    {
                        ffmpeg.av_packet_free(&packet);
                    }
                }
                finally
                {
                    ffmpeg.avcodec_free_context(&decoderContext);
                }
            }
            finally
            {
                ffmpeg.avformat_free_context(inputContext);
            }
        }

        public DecodedFrame Decode(string file, AVPixelFormat targetFormat)
        {
            AVFormatContext* inputContext = null;
            ffmpeg.avformat_open_input(&inputContext, file, fmt: null, options: null).ThrowExceptionIfError(nameof(ffmpeg.avformat_open_input));
            try
            {
                ffmpeg.avformat_find_stream_info(inputContext, options: null).ThrowExceptionIfError(nameof(ffmpeg.avformat_find_stream_info));

                AVCodec* decoder = null;
                var stream = ffmpeg.av_find_best_stream(inputContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0).ThrowExceptionIfError(nameof(ffmpeg.av_find_best_stream));

                var decoderContext = ffmpeg.avcodec_alloc_context3(decoder);
                try
                {
                    var video = inputContext->streams[stream];

                    ffmpeg.avcodec_parameters_to_context(decoderContext, video->codecpar).ThrowExceptionIfError(nameof(ffmpeg.avcodec_parameters_to_context));

                    ffmpeg.avcodec_open2(decoderContext, decoder, options: null).ThrowExceptionIfError(nameof(ffmpeg.avcodec_open2));

                    var width = video->codec->width;
                    var height = video->codec->height;
                    var sourceFormat = video->codec->pix_fmt;
                    var converterContext = ffmpeg.sws_getContext(width, height, sourceFormat, width, height, targetFormat, ffmpeg.SWS_FAST_BILINEAR, srcFilter: null, dstFilter: null, param: null);
                    try
                    {
                        var targetBufferSize = ffmpeg.av_image_get_buffer_size(targetFormat, width, height, 1);
                        var targetBuffer = (byte*)ffmpeg.av_malloc((ulong)targetBufferSize);
                        try
                        {
                            var destData = new byte_ptrArray4();
                            var destLineSize = new int_array4();

                            ffmpeg.av_image_fill_arrays(ref destData, ref destLineSize, targetBuffer, targetFormat, width, height, 1).ThrowExceptionIfError(nameof(ffmpeg.av_image_fill_arrays));

                            return HandlePacket(targetFormat, inputContext, decoderContext, width, height, converterContext, targetBufferSize, targetBuffer, destData, destLineSize);
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
                finally
                {
                    ffmpeg.avcodec_free_context(&decoderContext);
                }
            }
            finally
            {
                ffmpeg.avformat_free_context(inputContext);
            }
        }

        private static DecodedFrame HandlePacket(AVPixelFormat targetFormat, AVFormatContext* inputContext, AVCodecContext* decoderContext, int width, int height, SwsContext* converterContext, int targetBufferSize, byte* targetBuffer, byte_ptrArray4 destData, int_array4 destLineSize)
        {
            var packet = ffmpeg.av_packet_alloc();
            try
            {
                ffmpeg.av_packet_unref(packet);
                ffmpeg.av_read_frame(inputContext, packet).ThrowExceptionIfError(nameof(ffmpeg.av_read_frame));
                ffmpeg.avcodec_send_packet(decoderContext, packet).ThrowExceptionIfError(nameof(ffmpeg.avcodec_send_packet));

                return DecodeFrame(targetFormat, decoderContext, width, height, converterContext, targetBufferSize, targetBuffer, destData, destLineSize);
            }
            finally
            {
                ffmpeg.av_packet_free(&packet);
            }
        }

        private static DecodedFrame DecodeFrame(AVPixelFormat targetFormat, AVCodecContext* decoderContext, int width, int height, SwsContext* converterContext, int targetBufferSize, byte* targetBuffer, byte_ptrArray4 destData, int_array4 destLineSize)
        {
            var frame = ffmpeg.av_frame_alloc();
            try
            {
                ffmpeg.av_frame_unref(frame);
                ffmpeg.avcodec_receive_frame(decoderContext, frame);
                ffmpeg.sws_scale(converterContext, frame->data, frame->linesize, 0, frame->height, destData, destLineSize).ThrowExceptionIfError(nameof(ffmpeg.sws_scale));
                return new DecodedFrame(targetBuffer, targetBufferSize, width, height, targetFormat, destData, destLineSize, sharedBuffer: false);
            }
            finally
            {
                ffmpeg.av_frame_free(&frame);
            }
        }
    }
}
