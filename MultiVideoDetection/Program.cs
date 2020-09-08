using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Nick.FFMpeg.Net;
using Nick.InferenceEngine.Net;

namespace MultiVideoDetection
{
    internal static class Program
    {
        private static readonly string[] SourceFeeds =
        {
            "rtsp://192.168.100.10:7447/bcli83pgTMLu5tUK",
            "rtsp://192.168.100.10:7447/1FghmNf47bBRlnfT",
            "rtsp://192.168.100.10:7447/WjKpkyJTFcSJNRHk",
            "rtsp://192.168.100.10:7447/wl1bY2T7FTW5jHm5",
            "rtsp://192.168.100.10:7447/36YUqeCdbN8ybQjO",
            "rtsp://192.168.100.10:7447/KFejks4B1kAbzR1N",
            "rtsp://192.168.100.10:7447/LBOwYWvFX05OgOwO",
        };

        public static async Task Main()
        {
            try
            {
                Console.WriteLine($"API = {InferenceEngineLibrary.GetApiVersion()}");

                using var cts = new CancellationTokenSource();
                var ct = cts.Token;

                using var detector = await Detector.LoadAsync(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\person-vehicle-bike-detection-crossroad-1016\FP16\person-vehicle-bike-detection-crossroad-1016.xml", ct);

                //using var detector = new Detector(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\person-vehicle-bike-detection-crossroad-1016\FP16\person-vehicle-bike-detection-crossroad-1016.xml");
                //using var detector = new Detector(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\face-detection-0104\FP16\face-detection-0104.xml");
                //using var detector = new Detector(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\face-detection-0104\FP32\face-detection-0104.xml");
                DumpCoreInformation(detector.Core);

                var source = SourceFeeds;
                detector.Initialise("GPU");

                var decoderThreads = source
                    .Select((source, index) =>
                    {
                        var handler = new RawFrameHandler(index, blocking: false);
                        var thread = new Thread(() => DecoderThread(index, handler, source, ct))
                        {
                            Priority = ThreadPriority.AboveNormal,
                        };
                        thread.Start();
                        return (thread, handler);
                    })
                    .ToArray();

                var handlers = decoderThreads.Select(v => v.handler).ToArray();
                await detector.ProcessAsync(handlers, ct);
                Console.WriteLine("Finished");
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

        private static void DecoderThread(int index, RawFrameHandler handler, string source, CancellationToken ct)
        {
            using var decoder = new RawVideoDecoder(source, handler, index);
            try
            {
                decoder.ProcessingLoop(ct);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error in decoder thread: {ex}");
            }
            finally
            {
                Console.WriteLine("Processing finished");
                handler.Finished(index);
            }
        }
    }
}
