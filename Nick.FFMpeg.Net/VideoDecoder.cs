using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe abstract class VideoDecoder : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        protected readonly struct Initialised
        {
            public readonly AVFormatContext* inputContext;
            public readonly AVCodecContext* decoderContext;
            public readonly AVPacket* packet;
            public readonly AVFrame* spareFrame;
            public readonly int streamIndex;

            public Initialised(AVFormatContext* inputContext, int streamIndex, AVCodecContext* decoderContext, AVPacket* packet, AVFrame* spareFrame)
            {
                this.inputContext = inputContext;
                this.streamIndex = streamIndex;
                this.decoderContext = decoderContext;
                this.packet = packet;
                this.spareFrame = spareFrame;
            }
        }
        static VideoDecoder() => Helper.Initialise();

        private bool disposedValue;
        protected readonly Initialised _initialised;

        protected VideoDecoder(string device, string? format = null)
        {
            _initialised = Initialise(device, format);
        }

        private Initialised Initialise(string device, string? format)
        {
            AVInputFormat* inputFormat;

            if (!string.IsNullOrWhiteSpace(format))
            {
                inputFormat = ffmpeg.av_find_input_format(format);
                if (inputFormat == null)
                {
                    throw new ArgumentException("Failed to find input format", nameof(format));
                }
            }
            else
            {
                inputFormat = null;
            }

            AVFormatContext* inputContext = null;
            ffmpeg.avformat_open_input(&inputContext, device, inputFormat, options: null).ThrowExceptionIfError(nameof(ffmpeg.avformat_open_input));
            try
            {
                AVCodec* decoder = null;
                var streamIndex = ffmpeg.av_find_best_stream(inputContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0).ThrowExceptionIfError(nameof(ffmpeg.av_find_best_stream));
                var decoderContext = ffmpeg.avcodec_alloc_context3(decoder);
                try
                {
                    var video = inputContext->streams[streamIndex];
                    ffmpeg.avcodec_parameters_to_context(decoderContext, video->codecpar).ThrowExceptionIfError(nameof(ffmpeg.avcodec_parameters_to_context));

                    ffmpeg.avcodec_open2(decoderContext, decoder, options: null).ThrowExceptionIfError(nameof(ffmpeg.avcodec_open2));

                    var packet = ffmpeg.av_packet_alloc();
                    try
                    {
                        var spareFrame = ffmpeg.av_frame_alloc();
                        return new Initialised(inputContext, streamIndex, decoderContext, packet, spareFrame);
                    }
                    catch (Exception)
                    {
                        ffmpeg.av_packet_free(&packet);
                        throw;
                    }
                }
                catch (Exception)
                {
                    ffmpeg.avcodec_free_context(&decoderContext);
                    throw;
                }
            }
            catch (Exception)
            {
                ffmpeg.avformat_free_context(inputContext);
                throw;
            }
        }

        public enum FrameHandledStatus { Again, Finished, Success };

        protected abstract bool TryGetNextFrame([NotNullWhen(true)] out RawFrame? frame);

        protected abstract bool TryWritePoulatedFrame(RawFrame frame);

        public bool TryHandleNextPacket(CancellationToken ct)
        {
            var packet = _initialised.packet;
            var inputContext = _initialised.inputContext;
            var streamIndex = _initialised.streamIndex;
            do
            {
                ffmpeg.av_packet_unref(packet);
                var error = ffmpeg.av_read_frame(inputContext, packet);
                if (error == ffmpeg.AVERROR_EOF)
                {
                    return false;
                }
                HandleError(error, nameof(ffmpeg.av_read_frame));
            } while (!ct.IsCancellationRequested && (packet->stream_index != streamIndex));

            ffmpeg.avcodec_send_packet(_initialised.decoderContext, packet).ThrowExceptionIfError(nameof(ffmpeg.avcodec_send_packet));
            return true;
        }

        protected AVFrame* GetFrameToUse(AVFrame* spareFrame, ref RawFrame? rawFrame)
        {
            AVFrame* frameToUse;
            if (rawFrame == null)
            {
                TryGetNextFrame(out rawFrame);
            }

            if (rawFrame == null)
            {
                frameToUse = spareFrame;
            }
            else
            {
                frameToUse = rawFrame.Frame;
            }
            ffmpeg.av_frame_unref(frameToUse);

            return frameToUse;
        }

        private FrameHandledStatus TryHandleNextFrame(ref RawFrame? rawFrame)
        {
            var spareFrame = _initialised.spareFrame;
            var frameToUse = GetFrameToUse(spareFrame, ref rawFrame);
            var decoderContext = _initialised.decoderContext;

            var error = ffmpeg.avcodec_receive_frame(decoderContext, frameToUse);
            if (error == ffmpeg.AVERROR(ffmpeg.EAGAIN))
            {
                return FrameHandledStatus.Again;
            }

            if (error == ffmpeg.AVERROR_EOF)
            {
                return FrameHandledStatus.Finished;
            }

            HandleError(error, nameof(ffmpeg.avcodec_receive_frame));

            if (rawFrame != null)
            {
                if (TryWritePoulatedFrame(rawFrame))
                {
                    rawFrame = null;
                    return FrameHandledStatus.Success;
                }

                var exception = new Exception("Failed to write raw frame");
                throw exception;
            }

            return FrameHandledStatus.Success;
        }

        public virtual void ProcessingLoop(CancellationToken ct)
        {
            TryGetNextFrame(out var rawFrame);

            while (!ct.IsCancellationRequested)
            {
                if (!TryHandleNextPacket(ct))
                {
                    return;
                }

                while (!ct.IsCancellationRequested)
                {
                    var status = TryHandleNextFrame(ref rawFrame);
                    if (status == FrameHandledStatus.Success)
                    {
                        break;
                    }
                    if (status == FrameHandledStatus.Finished)
                    {
                        return;
                    }
                    if (status == FrameHandledStatus.Again)
                    {
                        break;
                    }

                    break;
                }
            }
        }

        protected static void HandleError(int error, string context)
        {
            if (error < 0)
            {
                var exception = new FFMpegException(error, context);
                throw exception;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                var packet = _initialised.packet;
                var frame = _initialised.spareFrame;
                var inputContext = _initialised.inputContext;
                var decoderContext = _initialised.decoderContext;

                ffmpeg.av_packet_free(&packet);
                ffmpeg.av_frame_free(&frame);
                ffmpeg.avcodec_free_context(&decoderContext);
                ffmpeg.avformat_free_context(inputContext);

                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~VideoDecoder()
#pragma warning restore MA0055 // Do not use destructor
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
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
