using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Nick.FFMpeg.Net;
using Nick.InferenceEngine.Net;

namespace MultiVideoDetection
{
    public class Detector : IDisposable
    {
        public static async Task<Detector> LoadAsync(string networkName, CancellationToken ct)
        {
            var core = new InferenceEngineCore();
            try
            {
                return await LoadAsync(core, networkName, ct);
            }
            catch (Exception)
            {
                core.Dispose();
                throw;
            }
        }

        public static async Task<Detector> LoadAsync(InferenceEngineCore core, string networkName, CancellationToken ct)
        {
            var network = await InferenceEngineNetwork.LoadAsync(core, networkName, ct: ct);
            return new Detector(core, network);
        }

        private bool disposedValue;
        private InferenceEngineExecutableNetwork? _executableNetwork;
        private readonly InferenceEngineNetwork _network;
        private readonly SSDProcessor _processor = new SSDProcessor();
        private readonly ImageMarkup _markup = new ImageMarkup();

        public int FrameSize { get; }
        public int C { get; }
        public int H { get; }
        public int W { get; }

        private class ResultInfo
        {
            public State State { get; }
            public IReadOnlyCollection<SSDProcessor.BoundingBox> Boxes { get; }

            public ResultInfo(State state, IReadOnlyCollection<SSDProcessor.BoundingBox> boxes)
            {
                State = state;
                Boxes = boxes;
            }
        }

        public InferenceEngineCore Core { get; }

        public Detector(string networkName) : this(new InferenceEngineCore(), networkName) { }

        public Detector(InferenceEngineCore core, string networkName) : this(core, new InferenceEngineNetwork(core, networkName)) { }

        public Detector(InferenceEngineCore core, InferenceEngineNetwork network)
        {
            Core = core;
            _network = network;
            var mainInputName = network.GetInputName(0);
            var inputDimensions = network.GetInputDimensions(mainInputName);
            C = (int)inputDimensions[1];
            H = (int)inputDimensions[2];
            W = (int)inputDimensions[3];
            FrameSize = C * H * W;
        }

        private unsafe Blob ConvertAndInitialise(IntPtr memory, State state)
        {
            var pointer = memory.ToPointer();
            state.Decoder.Convert(state.Frame, (byte*)pointer);

            var dimensions = new dimensions_t(1, C, H, W);
            var tensor = new tensor_desc_t(layout_e.NHWC, dimensions, precision_e.U8);
            var blob = new Blob(tensor, new Span<byte>(pointer, FrameSize));
            return blob;
        }

        public void Initialise(string deviceName)
        {
            var network = _network;
            var mainInputName = network.GetInputName(0);
            network.SetInputResizeAlgorithm(mainInputName, resize_alg_e.NO_RESIZE);
            network.SetInputLayout(mainInputName, layout_e.NHWC);
            network.SetInputPrecision(mainInputName, precision_e.U8);
            network.SetColorFormat(mainInputName, colorformat_e.BGR);

            _executableNetwork = new InferenceEngineExecutableNetwork(network, deviceName);
        }

        public async Task ProcessAsync(RawFrameHandler[] handlers, CancellationToken ct)
        {
            var executableNetwork = _executableNetwork ?? throw new InvalidOperationException("Detector needs to be initialised");
            var channel = Channel.CreateBounded<ResultInfo>(new BoundedChannelOptions(handlers.Length) { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = true });

            var detectionThread = new Thread(() => DetectionThread(handlers, channel.Writer, ct));
            detectionThread.Start();

            try
            {
                await foreach (var result in channel.Reader.ReadAllAsync(ct))
                {
                    await HandleResult(result, ct);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in process async: {ex}");
            }
        }

        private ResultInfo WaitForARequestToFinish(State state)
        {
            var request = state.ActiveRequest ?? throw new InvalidOperationException("No active request");
            request.WaitForFinished();
            var boxes = CompleteRequest(state);
            return new ResultInfo(state, boxes);
        }

#pragma warning disable MA0051 // Method is too long
        private void DetectionThread(RawFrameHandler[] handlers, ChannelWriter<ResultInfo> writer, CancellationToken ct)
#pragma warning restore MA0051 // Method is too long
        {
            var executableNetwork = _executableNetwork ?? throw new InvalidOperationException("Detector needs to be initialised");
            var max = executableNetwork.OptimalNumberOfInferRequests;
            var states = new State?[handlers.Length];
            var requests = new Queue<State>(max);
            try
            {
                while (!ct.IsCancellationRequested)
                {
                    var available = false;
                    var handled = 0;

                    for (var i = 0; i < handlers.Length; i++)
                    {
                        if (requests.Count == max)
                        {
                            FinishNextRequest(writer, requests);
                        }

                        var handler = handlers[i];
                        if (handler.TryConsume(i, out var frame))
                        {
                            available = true;
                            var state = states[i];
                            if (state == null)
                            {
                                var decoder = new FrameDecoder(frame, W, H, FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_BGR24, FrameSize);
                                var memory = Marshal.AllocHGlobal(FrameSize);
                                state = new State(i, handler, frame, decoder, memory);
                                states[i] = state;
                            }

                            StartRequest(state);
                            requests.Enqueue(state);
                            handled++;
                        }
                        else
                        {
                            available = available || !handler.IsFinished;
                        }
                    }

                    if (!available)
                    {
                        while (requests.Count > 0)
                        {
                            FinishNextRequest(writer, requests);
                        }
                        writer.Complete();
                        return;
                    }

                    if (handled == 0)
                    {
                        if (requests.Count > 0)
                        {
                            FinishNextRequest(writer, requests);
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                }

                Console.WriteLine("Detection thread finishing");
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                Console.WriteLine($"Error in detection thread: {ex}");
            }
            finally
            {
                foreach (var state in states)
                {
                    if (state != null)
                    {
                        Marshal.FreeHGlobal(state.Buffer);
                    }
                }
            }
        }

        private void FinishNextRequest(ChannelWriter<ResultInfo> writer, Queue<State> requests)
        {
            var state = requests.Dequeue();
            var result = WaitForARequestToFinish(state);
            if (!writer.TryWrite(result))
            {
                Console.WriteLine("Failed to write result");
            }
        }

        private async Task HandleResult(ResultInfo result, CancellationToken ct)
        {
            var state = result.State;
            var boxes = result.Boxes;

            try
            {
                var filteredBoxes = boxes.Where(b => (b.Confidence > 0.6)).GroupBy(b => b.ImageId).ToList();
                if (filteredBoxes.Count > 0)
                {
                    foreach (var group in filteredBoxes)
                    {
                        var id = group.Key;
                        await MarkupImage(state.Index, state.Frame, group, ct);
                    }
                }
            }
            finally
            {
                state.RawFrameHandler.Consumed(state.Index, state.Frame);
            }
        }

        private void StartRequest(State state)
        {
            var executableNetwork = _executableNetwork ?? throw new InvalidOperationException("Detector needs to be initialised");
            if (state.ActiveRequest != null)
            {
                throw new InvalidOperationException("State already has a request");
            }

            var network = _network;
            var mainInputName = network.GetInputName(0);
            var request = new InferenceEngineRequest(executableNetwork);
            using var blob = ConvertAndInitialise(state.Buffer, state);
            request.SetBlob(mainInputName, blob);
            state.ActiveRequest = request;
            request.StartInfer();
        }

        private IReadOnlyCollection<SSDProcessor.BoundingBox> CompleteRequest(State state)
        {
            var network = _network;
            var mainInputName = network.GetInputName(0);
            var mainOutputName = network.GetOutputName(0);
            var request = state.ActiveRequest ?? throw new InvalidOperationException("No active request");
            state.ActiveRequest = null;

            request.GetBlob(mainInputName).Dispose();

            using var outputBlob = request.GetBlob(mainOutputName);
            var boxes = _processor.ProcessOutput(outputBlob);

            request.Dispose();
            return boxes;
        }

        private unsafe static Bitmap GetBitmap(DecodedFrame frame)
        {
            var ptr = new IntPtr(frame.Buffer);
            return new Bitmap(frame.Width, frame.Height, 3 * frame.Width, System.Drawing.Imaging.PixelFormat.Format24bppRgb, ptr);
        }

        private async Task MarkupImage(int frameIndex, RawFrame frame, IEnumerable<SSDProcessor.BoundingBox> boxes, CancellationToken ct)
        {
            var converter = new ImageConvert();
            using var decoded = converter.Convert(frame, frame.Width, frame.Height, FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_BGR24);
            using var bitmap = GetBitmap(decoded);
            await _markup.MarkupImage(frameIndex, bitmap, boxes, ct);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
                _executableNetwork?.Dispose();
                _network.Dispose();
                Core.Dispose();
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
