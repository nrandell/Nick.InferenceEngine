using System.Diagnostics.CodeAnalysis;

namespace Nick.FFMpeg.Net
{
    public interface IRawFrameHandler
    {
        bool TryConsume(int id, [NotNullWhen(true)] out RawFrame? frame);
        bool TryProduce(int id, [NotNullWhen(true)] out RawFrame? frame);
        void Produced(int id, RawFrame producedFrame);
        void Consumed(int id, RawFrame consumedFrame);
        void Finished(int id);
        bool IsFinished { get; }
    }
}