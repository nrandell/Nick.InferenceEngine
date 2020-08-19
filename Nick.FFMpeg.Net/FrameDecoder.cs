using System;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class FrameDecoder : IDisposable
    {
        private bool disposedValue;
        private readonly SwsContext* _converterContext;
        private readonly int _targetBufferSize;
        private readonly int _targetWidth;
        private readonly int _targetHeight;
        private readonly AVPixelFormat _targetFormat;
        private readonly int _align;

        public FrameDecoder(RawFrame sampleSource, int targetWidth, int targetHeight, AVPixelFormat targetFormat, int targetBufferSize, int algorithm = ffmpeg.SWS_BICUBIC, int align = 1)
        {
            var actualTargetBufferSize = ffmpeg.av_image_get_buffer_size(targetFormat, targetWidth, targetHeight, align);
            if (actualTargetBufferSize != targetBufferSize)
            {
                throw new ArgumentOutOfRangeException(nameof(targetBufferSize), FormattableString.Invariant($"Incorrect buffer size - expected {actualTargetBufferSize}"));
            }

            var format = ImageConvert.ConvertFormat(sampleSource.Format);
            var converterContext = ffmpeg.sws_getContext(sampleSource.Width, sampleSource.Height, format, targetWidth, targetHeight, targetFormat, algorithm, srcFilter: null, dstFilter: null, param: null);
            try
            {
                _converterContext = converterContext;
                _targetBufferSize = targetBufferSize;
                _targetWidth = targetWidth;
                _targetHeight = targetHeight;
                _targetFormat = targetFormat;
                _align = align;
            }
            catch (Exception)
            {
                ffmpeg.sws_freeContext(converterContext);
                throw;
            }
        }

        public DecodedFrame Convert(RawFrame source, byte* targetBuffer)
        {
            var destData = new byte_ptrArray4();
            var destLineSize = new int_array4();
            ffmpeg.av_image_fill_arrays(ref destData, ref destLineSize, targetBuffer, _targetFormat, _targetWidth, _targetHeight, _align).ThrowExceptionIfError(nameof(ffmpeg.av_image_fill_arrays));

            var frame = source.Frame;
            ffmpeg.sws_scale(_converterContext, frame->data, frame->linesize, 0, frame->height, destData, destLineSize).ThrowExceptionIfError(nameof(ffmpeg.sws_scale));
            return new DecodedFrame(targetBuffer, _targetBufferSize, _targetWidth, _targetHeight, _targetFormat, destData, destLineSize, sharedBuffer: true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                ffmpeg.sws_freeContext(_converterContext);
                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~FrameDecoder()
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
