#define DISABLE_HARDWARE_DECODING 
using System;
using System.Runtime.InteropServices;
using System.Threading;

using FFmpeg.AutoGen;

using Nick.Inference;

namespace Nick.FFMpeg.Net
{
    public class Decoder
    {
        static Decoder() => Helper.Initialise();

        private readonly int _targetWidth;
        private readonly int _targetHeight;
        private readonly AVPixelFormat _targetPixelFormat;

        public Decoder(int targetWidth, int targetHeight /*, bool rgb */)
        {
            _targetWidth = targetWidth;
            _targetHeight = targetHeight;
            //_targetPixelFormat = rgb ? AVPixelFormat.AV_PIX_FMT_RGB24 : AVPixelFormat.AV_PIX_FMT_BGR24;
            _targetPixelFormat = AVPixelFormat.AV_PIX_FMT_NV12;
        }

        // To get this info ffmpeg.exe -f dshow -list_options true -i video="USB Video Device"
        // or ffmpeg.exe  -i "rtsp://192.168.100.10:7447/nFmibiRKMk4POaKK"
#pragma warning disable MA0051 // Method is too long
        public unsafe void Decode<TTarget>(string? format, string device, string decodePixelFormat, int decodeWidth, int decodeHeight,
#pragma warning restore MA0051 // Method is too long
            BoundedWriter<TTarget> writer,
            CancellationToken ct)
            where TTarget : IFFMpegMemoryTarget
        {
            AVInputFormat* inputFormat;
            if (string.IsNullOrWhiteSpace(format))
            {
                inputFormat = null;
            }
            else
            {
                inputFormat = ffmpeg.av_find_input_format(format);
                if (inputFormat == null)
                {
                    throw new ArgumentOutOfRangeException(nameof(format), $"Failed to find input format for {format}");
                }
            }

            AVDictionary* options = null;
            ffmpeg.av_dict_set(&options, "video_size", FormattableString.Invariant($"{decodeWidth}x{decodeHeight}"), ffmpeg.AV_DICT_APPEND);
            ffmpeg.av_dict_set(&options, "pixel_format", decodePixelFormat, ffmpeg.AV_DICT_APPEND);

            AVFormatContext* inputContext = null;
            ffmpeg.avformat_open_input(&inputContext, device, inputFormat, &options).ThrowExceptionIfError(nameof(ffmpeg.avformat_open_input));
            ffmpeg.av_dict_free(&options);

            options = null;
            ffmpeg.avformat_find_stream_info(inputContext, &options).ThrowExceptionIfError(nameof(ffmpeg.avformat_find_stream_info));

            AVDictionaryEntry* tag = null;
            while ((tag = ffmpeg.av_dict_get(inputContext->metadata, string.Empty, tag, ffmpeg.AV_DICT_IGNORE_SUFFIX)) != null)
            {
                var key = Marshal.PtrToStringAnsi((IntPtr)tag->key);
                var value = Marshal.PtrToStringAnsi((IntPtr)tag->value);
                Console.WriteLine($"{key} = {value}");
            }

            AVCodec* decoder = null;
            var streamIndex = ffmpeg.av_find_best_stream(inputContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0).ThrowExceptionIfError(nameof(ffmpeg.av_find_best_stream));

            var decoderContext = ffmpeg.avcodec_alloc_context3(decoder);

#if !DISABLE_HARDWARE_DECODING
            var hardwareState = CreateHardwareState(decoder);
#endif

            var video = inputContext->streams[streamIndex];
            ffmpeg.avcodec_parameters_to_context(decoderContext, video->codecpar).ThrowExceptionIfError(nameof(ffmpeg.avcodec_parameters_to_context));

            AVPixelFormat sourceFormat;
#if DISABLE_HARDWARE_DECODING
            sourceFormat = video->codec->pix_fmt;
#else

            if (hardwareState != null)
            {
                decoderContext->get_format = hardwareState.GetFormat;
                decoderContext->hw_device_ctx = hardwareState.hwDeviceContext;
                sourceFormat = hardwareState.hwConfig->pix_fmt;
            }
            else
            {
                sourceFormat = video->codec->pix_fmt;
            }
#endif

            ffmpeg.avcodec_open2(decoderContext, decoder, options: null).ThrowExceptionIfError(nameof(ffmpeg.avcodec_open2));
            var converterContext = ffmpeg.sws_getContext(video->codec->width, video->codec->height, sourceFormat,
                _targetWidth, _targetHeight, _targetPixelFormat,
                ffmpeg.SWS_FAST_BILINEAR, srcFilter: null, dstFilter: null, param: null);

            var targetBufferSize = ffmpeg.av_image_get_buffer_size(_targetPixelFormat, _targetWidth, _targetHeight, 1);

            var packet = ffmpeg.av_packet_alloc();
            var dummyFrame = ffmpeg.av_frame_alloc();
#if !DISABLE_HARDWARE_DECODING
            var swFrame = (hardwareState != null) ? ffmpeg.av_frame_alloc() : null;
#endif
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    int error;
                    do
                    {
                        ffmpeg.av_packet_unref(packet);
                        error = ffmpeg.av_read_frame(inputContext, packet);
                        if (error == ffmpeg.AVERROR_EOF)
                        {
                            return;
                        }
                        error.ThrowExceptionIfError(nameof(ffmpeg.av_read_frame));
                    } while (!ct.IsCancellationRequested && (packet->stream_index != streamIndex));

                    ffmpeg.avcodec_send_packet(decoderContext, packet).ThrowExceptionIfError(nameof(ffmpeg.avcodec_send_packet));

                    while (!ct.IsCancellationRequested)
                    {
                        AVFrame* sourceFrame;
                        if (writer.TryGet(out var owner))
                        {
                            TTarget target = owner;
                            sourceFrame = target.Frame;
                        }
                        else
                        {
                            sourceFrame = dummyFrame;
                        }

                        try
                        {
                            ffmpeg.av_frame_unref(sourceFrame);

                            error = ffmpeg.avcodec_receive_frame(decoderContext, sourceFrame);
                            if (error == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                            {
                                break;
                            }

                            if (error == ffmpeg.AVERROR_EOF)
                            {
                                return;
                            }
                            error.ThrowExceptionIfError(nameof(ffmpeg.avcodec_receive_frame));

                            if (owner != null)
                            {
                                AVFrame* frameToConvert;
#if DISABLE_HARDWARE_DECODING
                                frameToConvert = sourceFrame;
#else
                            if (hardwareState != null)
                            {
                                ffmpeg.av_frame_unref(swFrame);
                                swFrame->format = (int)hardwareState.hwConfig->pix_fmt;
                                ffmpeg.av_hwframe_transfer_data(swFrame, frame, 0).ThrowExceptionIfError(nameof(ffmpeg.av_hwframe_transfer_data));
                                ffmpeg.av_frame_copy_props(swFrame, frame);
                                frameToConvert = swFrame;
                            }
                            else
                            {
                                frameToConvert = frame;
                            }
#endif

                                TTarget target = owner;
                                ffmpeg.sws_scale(converterContext, frameToConvert->data, frameToConvert->linesize, 0, frameToConvert->height, target.DestData, target.DestLineSize).ThrowExceptionIfError(nameof(ffmpeg.sws_scale));
                                writer.Send(owner);
                                owner = null;
                            }
                        }
                        finally
                        {
                            owner?.Dispose();
                        }
                    }
                }
            }
            finally
            {
#if !DISABLE_HARDWARE_DECODING
                if (swFrame != null)
                {
                    ffmpeg.av_frame_free(&swFrame);
                }
#endif
                ffmpeg.av_frame_free(&dummyFrame);
                ffmpeg.av_packet_free(&packet);
            }
        }

#if !DISABLE_HARDWARE_DECODING
        // Hardware decoding causes issues when trying to preserve the frame so disabled for now ...
        private static unsafe HardwareDecoderState? CreateHardwareState(AVCodec* decoder)
        {
            AVBufferRef* hwDeviceContext = null;
            HardwareDecoderState? state = null;

            for (var i = 0; ; i++)
            {
                var hwConfig = ffmpeg.avcodec_get_hw_config(decoder, i);
                if (hwConfig == null)
                {
                    break;
                }

                Console.WriteLine($"HW config: {hwConfig->methods} {hwConfig->pix_fmt} {hwConfig->device_type}");
                if ((hwConfig->methods & 0x02 /* AV_CODEC_HW_CONFIG_METHOD_HW_FRAMES_CTX */) != 0)
                {
                    ffmpeg.av_hwdevice_ctx_create(&hwDeviceContext, hwConfig->device_type, null, null, 0).ThrowExceptionIfError(nameof(ffmpeg.av_hwdevice_ctx_create));
                    try
                    {
                        AVPixelFormat found = AVPixelFormat.AV_PIX_FMT_NONE;
                        var constraints = ffmpeg.av_hwdevice_get_hwframe_constraints(hwDeviceContext, null);
                        if (constraints != null)
                        {
                            try
                            {
                                var first = AVPixelFormat.AV_PIX_FMT_NONE;
                                for (var p = constraints->valid_sw_formats; *p != AVPixelFormat.AV_PIX_FMT_NONE; p++)
                                {
                                    var current = *p;
                                    Console.WriteLine($"Try pixel format {current}");
                                    if (ffmpeg.sws_isSupportedInput(current) != 0)
                                    {
                                        if (current == AVPixelFormat.AV_PIX_FMT_NV12)
                                        {
                                            found = current;
                                            first = current;
                                        }
                                        else if (first == AVPixelFormat.AV_PIX_FMT_NONE)
                                        {
                                            first = current;
                                        }
                                    }
                                }

                                if (found == AVPixelFormat.AV_PIX_FMT_NONE)
                                {
                                    found = first;
                                }
                            }
                            finally
                            {
                                ffmpeg.av_hwframe_constraints_free(&constraints);
                            }
                        }

                        if (found != AVPixelFormat.AV_PIX_FMT_NONE)
                        {
                            return new HardwareDecoderState(hwConfig, found, hwDeviceContext);
                        }
                    }
                    finally
                    {
                        if (state == null)
                        {
                            ffmpeg.av_buffer_unref(&hwDeviceContext);
                        }
                    }
                }
            }

            return null;
        }

        private unsafe class HardwareDecoderState : IDisposable
        {
            public readonly AVCodecHWConfig* hwConfig;
            public readonly AVPixelFormat found;
            public AVBufferRef* hwDeviceContext;

            public HardwareDecoderState(AVCodecHWConfig* hwConfig, AVPixelFormat found, AVBufferRef* hwDeviceContext)
            {
                this.hwConfig = hwConfig;
                this.found = found;
                this.hwDeviceContext = hwDeviceContext;
            }

            public void Dispose()
            {
                var context = hwDeviceContext;
                hwDeviceContext = null;
                if (context != null)
                {
                    ffmpeg.av_buffer_unref(&context);
                }
            }

            private AVPixelFormat GetFormatFunction(AVCodecContext* context, AVPixelFormat* formats)
            {
                for (var pixelFormat = formats; *pixelFormat != AVPixelFormat.AV_PIX_FMT_NONE; pixelFormat++)
                {
                    var value = *pixelFormat;
                    if (value == found)
                    {
                        return value;
                    }
                }
                throw new FFMpegException($"Failed to get hardware pixel format {found}");
            }

            public AVCodecContext_get_format GetFormat => GetFormatFunction;
        }
#endif
    }
}
