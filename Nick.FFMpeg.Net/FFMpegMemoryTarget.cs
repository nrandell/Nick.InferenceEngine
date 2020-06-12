using FFmpeg.AutoGen;

namespace Nick.FFMpeg.Net
{
    public unsafe interface IFFMpegMemoryTarget
    {
        AVFrame* Frame { get; }
        byte_ptrArray4 DestData { get; }
        int_array4 DestLineSize { get; }
    }
}
