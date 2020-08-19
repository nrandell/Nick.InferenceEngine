using System.Diagnostics.CodeAnalysis;

namespace Nick.FFMpeg.Net
{
    public unsafe class RawVideoDecoder : VideoDecoder
    {
        static RawVideoDecoder() => Helper.Initialise();

        private readonly IRawFrameHandler _handler;
        private readonly int _id;

        public RawVideoDecoder(string device, IRawFrameHandler handler, int id, string? format = null) : base(device, format)
        {
            _handler = handler;
            _id = id;
        }

        protected override bool TryGetNextFrame([NotNullWhen(true)] out RawFrame? frame)
            => _handler.TryProduce(_id, out frame);

        protected override bool TryWritePoulatedFrame(RawFrame frame)
        {
            _handler.Produced(_id, frame);
            return true;
        }
    }
}
