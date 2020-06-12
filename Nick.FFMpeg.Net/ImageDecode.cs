using FFmpeg.AutoGen;
namespace Nick.FFMpeg.Net
{
    public unsafe class ImageDecode
    {
        static ImageDecode() => Helper.Initialise();

        public (byte_ptrArray4 destData, int_array4 destLineSize) DecodeFile(string file, int targetWidth, int targetHeight, bool rgb = false)
        {
            AVFormatContext* inputContext = null;
            ffmpeg.avformat_open_input(&inputContext, file, null, null).ThrowExceptionIfError(nameof(ffmpeg.avformat_open_input));
            try
            {
                ffmpeg.avformat_find_stream_info(inputContext, null).ThrowExceptionIfError(nameof(ffmpeg.avformat_find_stream_info));

                AVCodec* decoder = null;
                var stream = ffmpeg.av_find_best_stream(inputContext, AVMediaType.AVMEDIA_TYPE_VIDEO, -1, -1, &decoder, 0).ThrowExceptionIfError(nameof(ffmpeg.av_find_best_stream));

                var decoderContext = ffmpeg.avcodec_alloc_context3(decoder);
                var video = inputContext->streams[stream];

                ffmpeg.avcodec_parameters_to_context(decoderContext, video->codecpar).ThrowExceptionIfError(nameof(ffmpeg.avcodec_parameters_to_context));

                ffmpeg.avcodec_open2(decoderContext, decoder, null).ThrowExceptionIfError(nameof(ffmpeg.avcodec_open2));

                var targetFormat = rgb ? AVPixelFormat.AV_PIX_FMT_RGB24 : AVPixelFormat.AV_PIX_FMT_BGR24;
                var converterContext = ffmpeg.sws_getContext(video->codec->width, video->codec->height, video->codec->pix_fmt, targetWidth, targetHeight, targetFormat, ffmpeg.SWS_BILINEAR, null, null, null);

                var targetBufferSize = ffmpeg.av_image_get_buffer_size(targetFormat, targetWidth, targetHeight, 1);
                var targetBuffer = (byte*)ffmpeg.av_malloc((ulong)targetBufferSize);

                var destData = new byte_ptrArray4();
                var destLineSize = new int_array4();

                ffmpeg.av_image_fill_arrays(ref destData, ref destLineSize, targetBuffer, targetFormat, targetWidth, targetHeight, 1).ThrowExceptionIfError(nameof(ffmpeg.av_image_fill_arrays));

                var packet = ffmpeg.av_packet_alloc();
                try
                {
                    ffmpeg.av_packet_unref(packet);

                    ffmpeg.av_read_frame(inputContext, packet).ThrowExceptionIfError(nameof(ffmpeg.av_read_frame));

                    ffmpeg.avcodec_send_packet(decoderContext, packet).ThrowExceptionIfError(nameof(ffmpeg.avcodec_send_packet));

                    var frame = ffmpeg.av_frame_alloc();
                    try
                    {
                        ffmpeg.av_frame_unref(frame);
                        ffmpeg.avcodec_receive_frame(decoderContext, frame);

                        ffmpeg.sws_scale(converterContext, frame->data, frame->linesize, 0, frame->height, destData, destLineSize).ThrowExceptionIfError(nameof(ffmpeg.sws_scale));
                        return (destData, destLineSize);
                    }
                    finally
                    {
                        ffmpeg.av_frame_free(&frame);
                    }
                }
                finally
                {
                    ffmpeg.av_packet_free(&packet);
                }
            }
            finally
            {
                ffmpeg.avformat_free_context(inputContext);
            }
        }
    }
}
