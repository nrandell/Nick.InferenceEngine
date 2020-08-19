using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;

using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe class ChannelVideoDecoder : VideoDecoder
    {
        static ChannelVideoDecoder() => Helper.Initialise();

        private readonly int _maxFrames;
        private readonly ChannelReader<RawFrame> _emptyFrames;
        private readonly ChannelWriter<RawFrame> _populatedFrames;
        private int _allocatedFrames = 0;

        public ChannelVideoDecoder(string device, ChannelReader<RawFrame> emptyFrames, ChannelWriter<RawFrame> populatedFrames, string? format = null, int maxFrames = 1) : base(device, format)
        {
            _maxFrames = maxFrames;
            _emptyFrames = emptyFrames;
            _populatedFrames = populatedFrames;
        }

        public override void ProcessingLoop(CancellationToken ct)
        {
            try
            {
                base.ProcessingLoop(ct);
                _populatedFrames.Complete();
            }
            catch (Exception ex)
            {
                _populatedFrames.Complete(ex);
                throw;
            }
        }

        protected override bool TryGetNextFrame([NotNullWhen(true)] out RawFrame? frame)
        {
            if (_emptyFrames.TryRead(out frame))
            {
                return true;
            }

            if (_allocatedFrames < _maxFrames)
            {
                frame = new RawFrame(ffmpeg.av_frame_alloc());
                _allocatedFrames++;
                return true;
            }

            return false;
        }

        protected override bool TryWritePoulatedFrame(RawFrame frame)
        {
            return _populatedFrames.TryWrite(frame);
        }
    }
}
