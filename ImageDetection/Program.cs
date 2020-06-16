using System;
using System.Runtime.InteropServices;

using Nick.InferenceEngine.Net;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace ImageDetection
{
    using static InferenceEngineLibrary;

    internal static class Program
    {
        //private const string NetworkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\yolo-v2-ava-0001\FP16\yolo-v2-ava-0001.xml";
        private const string NetworkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\person-detection-retail-0013\FP16\person-detection-retail-0013.xml";
        private const string ImageName = @"C:\Users\nickr\Downloads\dog.jpg";

        public static void Main()
        {
            try
            {
                //using var bitmap = (System.Drawing.Bitmap)System.Drawing.Image.FromFile(ImageName);
                using var image = Image.Load<Rgb24>(ImageName);
                Console.WriteLine($"image: {image}");
                if (image.TryGetSinglePixelSpan(out var pixelSpan))
                {
                    Console.WriteLine("Got span");

                    var apiVersion = GetApiVersion();
                    Console.WriteLine($"Using API version: {apiVersion}");

                    using var core = new InferenceEngineCore();

                    var devices = core.GetAvailableDevices();
                    foreach (var device in devices)
                    {
                        Console.WriteLine($"Device: {device}");
                        var versions = core.GetCoreVersions(device);
                        foreach (var version in versions)
                        {
                            Console.WriteLine($"{version.DeviceName} \"{version.Description}\" {version.Major}.{version.Minor}.{version.BuildNumber}");
                        }
                    }

                    using var network = new InferenceEngineNetwork(core, NetworkName);

                    Console.WriteLine($"Network name: {network.NetworkName}");

                    var inputShapes = network.GetInputShapes();
                    for (var i = 0; i < inputShapes.Length; i++)
                    {
                        var shape = inputShapes[i];
                        Console.WriteLine($"Input shape[{i}] = {shape.Name} {shape.Dimensions}");
                    }

                    var numberOfInputs = network.NumberOfInputs;
                    for (var i = 0; i < numberOfInputs; i++)
                    {
                        var name = network.GetInputName(i);
                        var precision = network.GetInputPrecision(name);
                        var layout = network.GetInputLayout(name);
                        var dimensions = network.GetInputDimensions(name);
                        var resizeAlgorithm = network.GetInputResizeAlgorithm(name);
                        Console.WriteLine($"Input[{i}] = {name} [{precision} {layout}] {dimensions}");
                    }

                    var numberOfOutputs = network.NumberOfOutputs;
                    for (var i = 0; i < numberOfOutputs; i++)
                    {
                        var name = network.GetOutputName(i);
                        var precision = network.GetOutputPrecision(name);
                        var layout = network.GetOutputLayout(name);
                        var dimensions = network.GetOutputDimensions(name);
                        Console.WriteLine($"Output[{i}] = {name} [{precision} {layout}] {dimensions}");
                    }

                    var mainInputName = network.GetInputName(0);
                    var mainOutputName = network.GetOutputName(0);

                    network.SetInputResizeAlgorithm(mainInputName, resize_alg_e.RESIZE_BILINEAR);
                    network.SetInputLayout(mainInputName, layout_e.NCHW);
                    network.SetInputPrecision(mainInputName, precision_e.U8);

                    Console.WriteLine("Create executable network");
                    using var executableNetwork = new InferenceEngineExecutableNetwork(network, "GPU");

                    Console.WriteLine("Create request");
                    using var request = new InferenceEngineRequest(executableNetwork);

                    var imageDimensions = new dimensions_t(1, 3, image.Height, image.Width);
                    var tensorDescription = new tensor_desc_t(layout_e.NHWC, imageDimensions, precision_e.U8);

                    Console.WriteLine("Create blob");
                    using var inputBlob = new SimpleBlob(tensorDescription, MemoryMarshal.Cast<Rgb24, byte>(pixelSpan));
                    request.SetBlob(mainInputName, inputBlob);

                    for (var i = 0; i < 10; i++)
                    {
                        Console.WriteLine($"Infer {i}");
                        request.Infer();
                        Console.WriteLine($"Infer {i} done");
                    }

                    using var outputBlob = request.GetBlob(mainOutputName);

                    Console.WriteLine($"Output blob. Sizes = {outputBlob.Size} {outputBlob.ByteSize}. [{outputBlob.Layout} {outputBlob.Precision}] {outputBlob.Dimensions}");

                }

            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
