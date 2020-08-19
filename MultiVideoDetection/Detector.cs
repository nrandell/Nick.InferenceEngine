using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

using Nick.FFMpeg.Net;
using Nick.InferenceEngine.Net;

namespace MultiVideoDetection
{
    public class Detector : IDisposable
    {
        private bool disposedValue;
        private InferenceEngineExecutableNetwork? _executableNetwork;
        private readonly InferenceEngineNetwork _network;
        private readonly SSDProcessor _processor = new SSDProcessor();
        private readonly ImageMarkup _markup = new ImageMarkup();

        public int FrameSize { get; }
        public int C { get; }
        public int H { get; }
        public int W { get; }

        public InferenceEngineCore Core { get; }
        public Detector(string networkName)
        {
            var core = new InferenceEngineCore();
            try
            {
                var network = _network = new InferenceEngineNetwork(core, networkName);
                var mainInputName = network.GetInputName(0);
                var inputDimensions = network.GetInputDimensions(mainInputName);
                C = (int)inputDimensions[1];
                H = (int)inputDimensions[2];
                W = (int)inputDimensions[3];
                FrameSize = C * H * W;
                Core = core;
            }
            catch (Exception)
            {
                core.Dispose();
                throw;
            }
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

#pragma warning disable MA0051 // Method is too long
        public async Task ProcessAsync(RawFrameHandler[] handlers, CancellationToken ct)
#pragma warning restore MA0051 // Method is too long
        {
            var states = new State?[handlers.Length];
            var frameIndex = 0;

            try
            {
                while (!ct.IsCancellationRequested)
                {
                    try
                    {
                        var available = false;
                        var handled = false;
                        for (var offset = 0; offset < handlers.Length; offset++)
                        {
                            var index = (frameIndex + offset) % handlers.Length;
                            var handler = handlers[index];

                            if (handler.TryConsume(index, out var frame))
                            {
                                available = true;
                                var state = states[index];
                                if (state == null)
                                {
                                    var decoder = new FrameDecoder(frame, W, H, FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_BGR24, FrameSize);
                                    var memory = Marshal.AllocHGlobal(FrameSize);
                                    state = new State(index, handler, frame, decoder, memory);
                                    states[index] = state;
                                }
                                frameIndex = index;
                                handled = true;
                                await Handle(state, ct);
                                break;
                            }

                            available = available || !handler.IsFinished;
                        }

                        if (!available)
                        {
                            return;
                        }
                        if (!handled)
                        {
                            {
                                await Task.Delay(100, ct);
                                continue;
                            }
                        }
                    }
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        Console.WriteLine($"Error processing: {ex}");
                    }
                }
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

        private async Task Handle(State state, CancellationToken ct)
        {
            Console.WriteLine(FormattableString.Invariant($"Handle index {state.Index}"));
            try
            {
                var executableNetwork = _executableNetwork;
                if (executableNetwork == null)
                {
                    throw new InvalidOperationException("Detector needs to be initialised");
                }

                var network = _network;
                var mainInputName = network.GetInputName(0);
                var mainOutputName = network.GetOutputName(0);

                using var request = new InferenceEngineRequest(executableNetwork);
                using var blob = ConvertAndInitialise(state.Buffer, state);
                request.SetBlob(mainInputName, blob);
                request.Infer();
                using var outputBlob = request.GetBlob(mainOutputName);
                var boundingBoxes = _processor.ProcessOutput(outputBlob);
                var filteredBoxes = boundingBoxes.Where(b => (b.Confidence > 0.3)).GroupBy(b => b.ImageId).ToList();
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

        private unsafe Bitmap GetBitmap(DecodedFrame frame)
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
