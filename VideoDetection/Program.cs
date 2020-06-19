using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Nick.FFMpeg.Net;
using Nick.InferenceEngine.Net;

namespace VideoDetection
{
    internal static class Program
    {
        private const int MaxFrames = 5;

        public static async Task Main()
        {
            try
            {
                Console.WriteLine($"API = {InferenceEngineLibrary.GetApiVersion()}");

                using var cts = new CancellationTokenSource();
                var ct = cts.Token;

                using var detector = new Detector(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\face-detection-0104\FP16\face-detection-0104.xml");

                var boundedOptions = new BoundedChannelOptions(MaxFrames) { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = true };
                var emptyChannel = Channel.CreateBounded<RawFrame>(boundedOptions);
                var populatedChannel = Channel.CreateBounded<RawFrame>(boundedOptions);

                var decoderThread = new Thread(() => DecoderThread(emptyChannel.Reader, populatedChannel.Writer, ct));
                decoderThread.Start();

                await detector.ProcessAsync(populatedChannel.Reader, emptyChannel.Writer, ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
        }

        private static void DecoderThread(ChannelReader<RawFrame> emptyFrames, ChannelWriter<RawFrame> populatedFrames, CancellationToken ct)
        {
            using var decoder = new VideoDecoder("rtsp://rtsp:Network123@192.168.100.206", maxFrames: MaxFrames);
            try
            {
                decoder.ProcessingLoop(emptyFrames, populatedFrames, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in decoder thread: {ex}");
            }
        }

        private static async Task ProcessingTask(ChannelReader<RawFrame> populatedFrames, ChannelWriter<RawFrame> emptyFrames, CancellationToken ct)
        {
            await foreach (var frame in populatedFrames.ReadAllAsync(ct))
            {
                Console.WriteLine($"Got frame {frame.Width}x{frame.Height} of {frame.Format}");
                await Task.Delay(1000, ct);
                await emptyFrames.WriteAsync(frame, ct);
            }
        }
    }
}
