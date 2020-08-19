using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Nick.FFMpeg.Net;
using Nick.InferenceEngine.Net;

namespace MultiVideoDetection
{
    internal static class Program
    {
        private static readonly string[] SourceFiles =
        {
            @"C:\Users\nickr\Downloads\House Front - 8-18-2020, 1.16.45pm.mp4",
            @"C:\Users\nickr\Downloads\Bedroom 1 - 8-18-2020, 8.30.04am.mp4",
        };

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

                using var detector = new Detector(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\person-vehicle-bike-detection-crossroad-1016\FP16\person-vehicle-bike-detection-crossroad-1016.xml");
                //using var detector = new Detector(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\face-detection-0104\FP16\face-detection-0104.xml");
                //using var detector = new Detector(@"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\face-detection-0104\FP32\face-detection-0104.xml");
                DumpCoreInformation(detector.Core);

                var source = SourceFiles;
                detector.Initialise("CPU");


                var decoderThreads = source
                    .Select((source, index) =>
                    {
                        var handler = new RawFrameHandler(index, blocking: true);
                        var thread = new Thread(() => DecoderThread(index, handler, source, ct));
                        thread.Start();
                        return (thread, handler);
                    })
                    .ToArray();

                var handlers = decoderThreads.Select(v => v.handler).ToArray();

                //var decoderThread = new Thread(() => DecoderThread(handler, SourceFeeds[0], ct));
                //var decoderThread = new Thread(() => DecoderThread(handler, SourceFiles[0], ct));
                //var decoderThread = new Thread(() => ImageDecoderThread(handler, @"c:\temp\samples", ct));
                //decoderThread.Start();

                //var handlers = new[] { handler };

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

        private static void ImageDecoderThread(int index, RawFrameHandler handler, string source, CancellationToken ct)
        {
            try
            {
                var decoder = new ImageDecode();
                foreach (var image in Directory.EnumerateFiles(source, "*.jpg"))
                {
                    if (handler.TryProduce(index, out var frame))
                    {
                        decoder.DecodeRaw(image, frame);
                        handler.Produced(index, frame);
                    }
                    else
                    {
                        Console.WriteLine("Lost frame");
                    }
                }
            }
            finally
            {
                handler.Finished(index);
            }
        }

        private static void DecoderThread(int index, RawFrameHandler handler, string source, CancellationToken ct)
        {
            //using var decoder = new VideoDecoder("rtsp://rtsp:Network123@192.168.100.206", maxFrames: MaxFrames);
            //using var decoder = new VideoDecoder("rtsp://192.168.100.10:7447/N2jRqfXC7MaVh0Ni", maxFrames: MaxFrames);
            //using var decoder = new VideoDecoder("rtsp://192.168.100.10:7447/bcli83pgTMLu5tUK");
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
