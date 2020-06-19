using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using Nick.FFMpeg.Net;
using Nick.InferenceEngine.Net;

namespace VideoDetection
{
    public class Detector : IDisposable
    {
        private bool disposedValue;
        private readonly InferenceEngineNetwork _network;
        private readonly InferenceEngineCore _core;

        public Detector(string networkName)
        {
            var core = new InferenceEngineCore();
            try
            {
                _network = new InferenceEngineNetwork(core, networkName);
                _core = core;
            }
            catch (Exception)
            {
                core.Dispose();
                throw;
            }
        }

        private unsafe Blob Initialise(RawFrame frame)
        {
            Blob createBlob(int height, int lineWidth, int width, byte* data)
            {
                var dimensions = new dimensions_t(1, 1, height, lineWidth);
                var tensor = new tensor_desc_t(layout_e.NHWC, dimensions, precision_e.U8);
                using var basicBlob = new Blob(tensor, new Span<byte>(data, height * lineWidth));
                var roi = new roi_t(1, 0, 0, width, height);
                return new Blob(basicBlob, roi);
            }

            var width = frame.Width;
            var height = frame.Height;
            var avFrame = frame.Frame;
            var linesize = avFrame->linesize;
            var data = avFrame->data;

            using var yBlob = createBlob(height, linesize[0], width, data[0]);
            using var uBlob = createBlob(height / 2, linesize[1], width / 2, data[1]);
            using var vBlob = createBlob(height / 2, linesize[2], width / 2, data[2]);

            return new Blob(yBlob, uBlob, vBlob);
        }

        public async Task ProcessAsync(ChannelReader<RawFrame> populatedFrames, ChannelWriter<RawFrame> usedFrames, CancellationToken ct)
        {
            var network = _network;
            var mainInputName = network.GetInputName(0);
            var mainOutputName = network.GetOutputName(0);
            network.SetInputResizeAlgorithm(mainInputName, resize_alg_e.RESIZE_BILINEAR);
            network.SetInputLayout(mainInputName, layout_e.NCHW);
            network.SetInputPrecision(mainInputName, precision_e.U8);
            network.SetColorFormat(mainInputName, colorformat_e.I420);

            var processor = new SSDProcessor();
            var markup = new ImageMarkup();

            using var executableNetwork = new InferenceEngineExecutableNetwork(network, "MYRIAD");

            await foreach (var frame in populatedFrames.ReadAllAsync(ct))
            {
                try
                {
                    using var request = new InferenceEngineRequest(executableNetwork);
                    using var blob = Initialise(frame);
                    request.SetBlob(mainInputName, blob);
                    request.Infer();
                    using var outputBlob = request.GetBlob(mainOutputName);
                    var boundingBoxes = processor.ProcessOutput(outputBlob);
                    var filteredBoxes = boundingBoxes.Where(b => b.Confidence > 0.5).ToList();
                    if (filteredBoxes.Count > 0)
                    {
                        await MarkupImage(frame, markup, filteredBoxes, ct);
                    }
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    Console.WriteLine($"Error processing frame: {ex}");
                }
                usedFrames.TryWrite(frame);
            }
        }

        private unsafe Bitmap GetBitmap(DecodedFrame frame)
        {
            var ptr = new IntPtr(frame.Buffer);
            return new Bitmap(frame.Width, frame.Height, 3 * frame.Width, System.Drawing.Imaging.PixelFormat.Format24bppRgb, ptr);
        }

        private async Task MarkupImage(RawFrame frame, ImageMarkup markup, IEnumerable<SSDProcessor.BoundingBox> boxes, CancellationToken ct)
        {
            var converter = new ImageConvert();
            using var decoded = converter.Convert(frame, frame.Width, frame.Height, FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_BGR24);
            using var bitmap = GetBitmap(decoded);
            await markup.MarkupImage(bitmap, boxes, ct);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~Detector()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
