using System;
using System.Threading;
using System.Threading.Channels;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class VideoDecoder : IDisposable
    {
        private struct Initialised
        {
            public readonly AVFormatContext* inputContext;
            public readonly AVCodecContext* decoderContext;
            public readonly AVPacket* packet;
            public readonly AVFrame* frame;
            public readonly int streamIndex;

            public Initialised(AVFormatContext* inputContext, int streamIndex, AVCodecContext* decoderContext, AVPacket* packet, AVFrame* frame)
            {
                this.inputContext = inputContext;
                this.streamIndex = streamIndex;
                this.decoderContext = decoderContext;
                this.packet = packet;
                this.frame = frame;
            }
        }
        static VideoDecoder() => Helper.Initialise();
        private static int _nextId = 0;

        public int Id { get; } = Interlocked.Increment(ref _nextId);

        private bool disposedValue;
        private readonly Initialised _initialised;
        private readonly int _maxFrames;

        public VideoDecoder(string device, string? format = null, int maxFrames = 1)
        {
            _initialised = Initialise(device, format);
            _maxFrames = maxFrames;
        }

        private Initialised Initialise(string device, string? format)
        {
            AVInputFormat* inputFormat;

            if (!string.IsNullOrWhiteSpace(format))
            {
                inputFormat = ffmpeg.av_find_input_format(format);
                if (inputFormat == null)
                {
                    throw new ArgumentException(nameof(format));
                }
            }
            else
            {
                inputFormat = null;
            }

            AVFormatContext* inputContext = null;
            ffmpeg.avformat_open_input(&inputContext, device, inputFormat, null).ThrowExceptionIfError(nameof(ffmpeg.avformat_open_input));
            try
            {
                AVCodec* decoder = null;
                var streamIndex = ffmpeg.av_find_best_stream(inputContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0).ThrowExceptionIfError(nameof(ffmpeg.av_find_best_stream));
                var decoderContext = ffmpeg.avcodec_alloc_context3(decoder);
                try
                {
                    ffmpeg.avcodec_open2(decoderContext, decoder, null).ThrowExceptionIfError(nameof(ffmpeg.avcodec_open2));

                    var packet = ffmpeg.av_packet_alloc();
                    try
                    {
                        var frame = ffmpeg.av_frame_alloc();
                        return new Initialised(inputContext, streamIndex, decoderContext, packet, frame);
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


        public void ProcessingLoop(ChannelReader<RawFrame> emptyFrames, ChannelWriter<RawFrame> populatedFrames, CancellationToken ct)
        {
            void handleError(int error, string context)
            {
                if (error < 0)
                {
                    var exception = new FFMpegException(error, context);
                    populatedFrames.Complete(exception);
                    throw exception;
                }
            }

            var packet = _initialised.packet;
            var frame = _initialised.frame;
            var inputContext = _initialised.inputContext;
            var decoderContext = _initialised.decoderContext;
            var streamIndex = _initialised.streamIndex;
            var allocatedFrames = 0;

            RawFrame? tryGetNextFrame()
            {
                if (emptyFrames.TryRead(out var rawFrame))
                {
                    return rawFrame;
                }
                else
                {
                    if (allocatedFrames < _maxFrames)
                    {
                        var frame = ffmpeg.av_frame_alloc();
                        rawFrame = new RawFrame(frame);
                        allocatedFrames++;
                        return rawFrame;
                    }
                    else
                    {
                        return null;
                    }
                }
            }


            var rawFrame = tryGetNextFrame();

            while (!ct.IsCancellationRequested)
            {
                int error;
                do
                {
                    ffmpeg.av_packet_unref(packet);
                    error = ffmpeg.av_read_frame(inputContext, packet);
                    if (error == ffmpeg.AVERROR_EOF)
                    {
                        populatedFrames.Complete();
                        return;
                    }
                    handleError(error, nameof(ffmpeg.av_read_frame));
                } while (!ct.IsCancellationRequested && (packet->stream_index != streamIndex));

                ffmpeg.avcodec_send_packet(decoderContext, packet).ThrowExceptionIfError(nameof(ffmpeg.avcodec_send_packet));

                while (!ct.IsCancellationRequested)
                {
                    AVFrame* frameToUse;
                    if (rawFrame == null)
                    {
                        rawFrame = tryGetNextFrame();
                    }
                    if (rawFrame == null)
                    {
                        frameToUse = frame;
                    }
                    else
                    {
                        frameToUse = rawFrame.Frame;
                    }


                    ffmpeg.av_frame_unref(frameToUse);
                    error = ffmpeg.avcodec_receive_frame(decoderContext, frameToUse);
                    if (error == ffmpeg.AVERROR(ffmpeg.EAGAIN))
                    {
                        break;
                    }
                    else if (error == ffmpeg.AVERROR_EOF)
                    {
                        populatedFrames.Complete();
                        return;
                    }
                    handleError(error, nameof(ffmpeg.avcodec_receive_frame));
                    if (rawFrame != null)
                    {
                        if (populatedFrames.TryWrite(rawFrame))
                        {
                            rawFrame = null;
                        }
                        else
                        {
                            var exception = new ApplicationException("Failed to write raw frame");
                            populatedFrames.Complete(exception);
                            throw exception;
                        }
                    }
                }
            }
        }


        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                var packet = _initialised.packet;
                var frame = _initialised.frame;
                var inputContext = _initialised.inputContext;
                var decoderContext = _initialised.decoderContext;

                ffmpeg.av_packet_free(&packet);
                ffmpeg.av_frame_free(&frame);
                ffmpeg.avcodec_free_context(&decoderContext);
                ffmpeg.avformat_free_context(inputContext);

                disposedValue = true;
            }
        }

        ~VideoDecoder()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Console.WriteLine($"Finalizer for video decoder {Id}");
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
