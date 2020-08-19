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
                DumpCoreInformation(detector.Core);

                var boundedOptions = new BoundedChannelOptions(MaxFrames) { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = true };
                var emptyChannel = Channel.CreateBounded<RawFrame>(boundedOptions);
                var populatedChannel = Channel.CreateBounded<RawFrame>(boundedOptions);

                var decoderThread = new Thread(() => DecoderThread(emptyChannel.Reader, populatedChannel.Writer, ct));
                decoderThread.Start();

                await detector.ProcessAsync(populatedChannel.Reader, emptyChannel.Writer, "GPU", ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
            }
        }

        private static void DumpCoreInformation(InferenceEngineCore core)
        {
            var devices = core.GetAvailableDevices();
            foreach (var device in devices)
            {
                Console.WriteLine($"Device: {device}");
                var versions = core.GetCoreVersions(device);
                foreach (var version in versions)
                {
                    Console.WriteLine(FormattableString.Invariant($"{version.DeviceName} \"{version.Description}\" {version.Major}.{version.Minor}.{version.BuildNumber}"));
                }
            }
        }

        private static void DecoderThread(ChannelReader<RawFrame> emptyFrames, ChannelWriter<RawFrame> populatedFrames, CancellationToken ct)
        {
            //using var decoder = new VideoDecoder("rtsp://rtsp:Network123@192.168.100.206", maxFrames: MaxFrames);
            //using var decoder = new VideoDecoder("rtsp://192.168.100.10:7447/N2jRqfXC7MaVh0Ni", maxFrames: MaxFrames);
            using var decoder = new VideoDecoder("rtsp://192.168.100.10:7447/bcli83pgTMLu5tUK");
            try
            {
                decoder.ProcessingLoop(emptyFrames, populatedFrames, ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in decoder thread: {ex}");
            }
        }
    }
}
