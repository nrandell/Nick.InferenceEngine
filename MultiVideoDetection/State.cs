
using System;

using Nick.FFMpeg.Net;
using Nick.InferenceEngine.Net;

namespace MultiVideoDetection
{
    internal class State
    {
        public State(int index, RawFrameHandler rawFrameHandler, RawFrame frame, FrameDecoder decoder, IntPtr buffer)
        {
            Index = index;
            RawFrameHandler = rawFrameHandler;
            Frame = frame;
            Decoder = decoder;
            Buffer = buffer;
        }

        public int Index { get; }
        public RawFrameHandler RawFrameHandler { get; }
        public RawFrame Frame { get; }
        public FrameDecoder Decoder { get; }
        public IntPtr Buffer { get; }

        public InferenceEngineRequest? ActiveRequest { get; set; }
    }
}
