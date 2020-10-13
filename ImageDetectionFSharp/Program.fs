open Microsoft.FSharp.NativeInterop

#nowarn "9"
#nowarn "51"
// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open Nick.InferenceEngine.Net
open System.Runtime.InteropServices
open OpenCvSharp
open Nick.FFMpeg.Net


//let networkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\yolo-v2-ava-0001\FP16\yolo-v2-ava-0001.xml"
//let networkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\person-detection-retail-0013\FP16\person-detection-retail-0013.xml"
let networkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\face-detection-0104\FP32\face-detection-0104.xml"
//let imageName = @"C:\Users\nickr\Source\Code\machinelearning-samples\samples\fsharp\getting-started\DeepLearning_ObjectDetection_Onnx\ObjectDetectionConsoleApp\assets\images\dog2.jpg"
//let imageName = @"C:\Users\nickr\Downloads\IMG_20200510_085247.jpg"
let imageName = @"c:\Users\nickr\Downloads\IMG_20191005_081128.jpg"

let loadImage (imageFileName: string) =
    Cv2.ImRead imageFileName

let saveImage (width: int) (height: int) (pixelSpan: inref<Span<byte>>) (results: ImageDetection.SSD.BoundingBox[]) =
    let first = &&pixelSpan.GetPinnableReference()
    
    let bitmap = new Drawing.Bitmap(width, height, 3 * width, Drawing.Imaging.PixelFormat.Format24bppRgb, first |> NativePtr.toNativeInt)
    let graphics = Drawing.Graphics.FromImage bitmap

    use pen = new Drawing.Pen(Drawing.Brushes.Red)
    pen.Width <- 8.0f


    for result in results do
        let rect = result.Rect
        graphics.DrawRectangle(pen, rect)
        
        
    bitmap.Save @"c:\temp\test.png"



let handlePixels width height layout (dimensions: dimensions_t) (pixelSpan: inref<Span<byte>>) =
    printfn "Got %d x %d of %O %O length %d" width height layout dimensions pixelSpan.Length
    for i = 0 to 31 do
        printf "%2X " pixelSpan.[i]
    printfn ""

    use core = new InferenceEngineCore()
    core.GetAvailableDevices()
    |> Seq.iter (fun device ->
        device
        |> core.GetCoreVersions
        |> Seq.iter (fun version ->
            printfn "%s \"%s\" %d.%d.%s" version.DeviceName version.DeviceName version.Major version.Minor version.BuildNumber
            )
        )

    use network = new InferenceEngineNetwork(core, networkName)
    printfn "Network %s" network.NetworkName

    let mainInputName = network.GetInputName 0
    let mainOutputName = network.GetOutputName 0
    printfn "Inputs: %s %O %O %O %O" mainInputName (network.GetInputResizeAlgorithm mainInputName) (network.GetInputLayout mainInputName) (network.GetInputPrecision mainInputName) (network.GetInputDimensions mainInputName)

    network.SetInputResizeAlgorithm(mainInputName, resize_alg_e.RESIZE_BILINEAR)
    network.SetInputLayout(mainInputName, layout)
    network.SetInputPrecision(mainInputName, precision_e.U8)

    use executableNetwork = new InferenceEngineExecutableNetwork(network, "CPU")

    use request = new InferenceEngineRequest(executableNetwork)

    //let dimensions = dimensions_t(1, 3, height, width)
    let tensorDescription = tensor_desc_t(layout, dimensions, precision_e.U8)

    use inputBlob = new Blob(&tensorDescription, pixelSpan)
    request.SetBlob(mainInputName, inputBlob)

    request.Infer()

    use outputBlob = request.GetBlob(mainOutputName)
    printfn "Output %d [%A %A] %A" outputBlob.Size outputBlob.Layout outputBlob.Precision outputBlob.Dimensions
    let span = outputBlob.AsReadOnlySpan<float32>();

    let dims = outputBlob.Dimensions
    let maxProposalCount = int dims.[2]
    let objectSize = int dims.[3]

    let results = ImageDetection.SSD.parseOutputs &span 0.5 maxProposalCount objectSize (float32 width) (float32 height)

    saveImage width height &pixelSpan results



    //let boundingBoxes = ImageDetection.parseOutputs 0.3f &span
    //printfn ".....The bounding boxes in the image are detected as below...."
    //boundingBoxes
    //|> Seq.iter
    //    (fun fbox ->
    //        printfn "%s and its Confidence score: %0.7f" fbox.Label fbox.Confidence
    //    )
    //printfn ""

    //let filteredBoxes = ImageDetection.nonMaxSuppress 5 0.5f boundingBoxes
    
    //printfn ".....The objects in the image are detected as below...."
    //filteredBoxes
    //|> Seq.iter
    //    (fun fbox ->
    //        printfn "%s and its Confidence score: %0.7f" fbox.Label fbox.Confidence
    //    )
    //printfn ""
    
    printfn "Done"

let imageSharpRunner() =
    use image = Image.Load<Bgr24>(imageName)
    
    let mutable pixelSpan = Span()
    match image.TryGetSinglePixelSpan(&pixelSpan) with
    | false -> failwith "Failed to get pixel span"
    | true ->
        let byteSpan = MemoryMarshal.Cast(pixelSpan)
        let first = System.Runtime.InteropServices.MemoryMarshal.GetReference(byteSpan)
        let dimensions = dimensions_t(1L, int64 image.Height, int64 image.Width, 3L)
        handlePixels image.Width image.Height layout_e.NHWC dimensions &byteSpan


    //use image = Image.Load<Bgr24>(imageName)
    //use bitmap: System.Drawing.Bitmap = downcast (System.Drawing.Image.FromFile imageName) 
    //bitmap.LockBits

    //let mutable pixelSpan = Span<Bgr24>()
    //match image.TryGetSinglePixelSpan(&pixelSpan) with
    //| false -> failwith "Failed to get pixel span"
    //| true ->
    //    handlePixels image &pixelSpan

let systemDrawingRunner() =
    use bitmap: System.Drawing.Bitmap = downcast (System.Drawing.Image.FromFile imageName)
    let rect = System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height)
    let data = bitmap.LockBits(rect, Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format24bppRgb)
    try
        let dataSize = data.Height * data.Width * 3
        let address = NativePtr.ofNativeInt<int> data.Scan0
        let ptr = NativePtr.toVoidPtr address
        let pixels = Span(ptr, dataSize)
        let dimensions = dimensions_t(1L, 3L, int64 data.Height, int64 data.Width)
        handlePixels data.Width data.Height layout_e.NHWC dimensions &pixels
    finally
        bitmap.UnlockBits(data)
    


let openCVRunner() =
    use image = loadImage imageName
    let dataSize = image.Channels() * image.Width * image.Height
    let memory =image.DataPointer
    let voidmemory = NativePtr.toVoidPtr memory

    let pixels = Span<byte>(voidmemory, dataSize)
    let dimensions = dimensions_t(1L, int64 (image.Channels()), int64 image.Rows, int64 image.Cols)
    handlePixels image.Width image.Height layout_e.NHWC dimensions &pixels



let ffmpegRunner() =
    let width = 3264
    let height = 2448
    use rawFrame = new RawFrame()
    ImageDecode.DecodeRaw(imageName, rawFrame)
    //let struct (destData, _destLineSize) = decoder.DecodeFile(imageName, width, height)
    let size = 3 * width * height
    let frame = NativePtr.get rawFrame.Frame 0
    let first = frame.data.[0u] |> NativePtr.toVoidPtr

    let pixels = Span(first, size)
    let dimensions = dimensions_t(1L, 3L, int64 height, int64 width)
    
    handlePixels width height layout_e.NHWC dimensions &pixels


let nv12FfmpegRunner() =
    let createBlob channels height width (data: nativeptr<byte>) =
        let dimensions = dimensions_t(1L, int64 channels, int64 height, int64 width)
        let tensor = tensor_desc_t(layout_e.NHWC, dimensions, precision_e.U8)
        let span = Span<byte>(data |> NativePtr.toVoidPtr, channels * height * width)
        new Blob(&tensor, span)

    use frame = ImageDecode.Decode(imageName,FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_NV12)
    let inputWidth = frame.Width
    let inputHeight = frame.Height
    let format = frame.Format

    printfn "Frame: %d x %d @ %O" inputWidth inputHeight format

    let yWidth = frame.DestLineSize.[0u]
    let uvWidth = frame.DestLineSize.[1u]
    printfn "Sizes: %d %d" yWidth uvWidth

    let yBlob = createBlob 1 inputHeight yWidth frame.DestData.[0u]
    let uvBlob = createBlob 2 (inputHeight / 2) (uvWidth / 2) frame.DestData.[1u]

    let nv12Blob = new Blob(yBlob, uvBlob);

    let layout = layout_e.NCHW

    use core = new InferenceEngineCore()
    use network = new InferenceEngineNetwork(core, networkName)
    printfn "Network %s" network.NetworkName

    let mainInputName = network.GetInputName 0
    let mainOutputName = network.GetOutputName 0
    printfn "Inputs: %s %O %O %O %O" mainInputName (network.GetInputResizeAlgorithm mainInputName) (network.GetInputLayout mainInputName) (network.GetInputPrecision mainInputName) (network.GetInputDimensions mainInputName)

    network.SetInputResizeAlgorithm(mainInputName, resize_alg_e.RESIZE_BILINEAR)
    network.SetInputLayout(mainInputName, layout)
    network.SetInputPrecision(mainInputName, precision_e.U8)
    network.SetColorFormat(mainInputName, colorformat_e.NV12);

    use executableNetwork = new InferenceEngineExecutableNetwork(network, "CPU")

    use request = new InferenceEngineRequest(executableNetwork)

    request.SetBlob(mainInputName, nv12Blob)

    request.Infer()

    use outputBlob = request.GetBlob(mainOutputName)
    printfn "Output %d [%A %A] %A" outputBlob.Size outputBlob.Layout outputBlob.Precision outputBlob.Dimensions
    let span = outputBlob.AsReadOnlySpan<float32>();

    let dims = outputBlob.Dimensions
    let maxProposalCount = int dims.[2]
    let objectSize = int dims.[3]

    ImageDetection.SSD.parseOutputs &span 0.5 maxProposalCount objectSize (float32 inputWidth) (float32 inputHeight) |> ignore
    //let converter = ImageConvert()
    //use decoded = converter.Convert(frame, inputWidth, inputHeight, FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_RGB24)
    //let span = decoded.AsSpan()
    //saveImage inputWidth inputHeight &span results

let i420FfmpegRunner() =
    let createBlob channels height lineWidth width (data: nativeptr<byte>) =
        let dimensions = dimensions_t(1L, int64 channels, int64 height, int64 lineWidth)
        let tensor = tensor_desc_t(layout_e.NHWC, dimensions, precision_e.U8)
        let span = Span<byte>(data |> NativePtr.toVoidPtr, channels * height * lineWidth)
        use basicBlob = new Blob(&tensor, span)
        let roi = new roi_t(1, 0, 0, width, height)
        new Blob(basicBlob, &roi)

    use rawFrame = new RawFrame()
    ImageDecode.DecodeRaw(imageName, rawFrame)
    let frame = NativePtr.get rawFrame.Frame 0
    let inputWidth = rawFrame.Width
    let inputHeight = rawFrame.Height
    let format = rawFrame.Format

    printfn "Frame: %d x %d @ %O" inputWidth inputHeight format

    let yWidth = frame.linesize.[0u]
    let uWidth = frame.linesize.[1u]
    let vWidth = frame.linesize.[2u]

    printfn "Sizes: %d %d %d" yWidth uWidth vWidth

    use yBlob = createBlob 1 inputHeight yWidth inputWidth frame.data.[0u]
    use uBlob = createBlob 1 (inputHeight / 2) uWidth (inputWidth / 2) frame.data.[1u]
    use vBlob = createBlob 1 (inputHeight / 2) vWidth (inputWidth / 2) frame.data.[2u]

    use i420Blob = new Blob(yBlob, uBlob, vBlob)

    let layout = layout_e.NCHW

    use core = new InferenceEngineCore()
    use network = new InferenceEngineNetwork(core, networkName)
    printfn "Network %s" network.NetworkName

    let mainInputName = network.GetInputName 0
    let mainOutputName = network.GetOutputName 0
    printfn "Inputs: %s %O %O %O %O" mainInputName (network.GetInputResizeAlgorithm mainInputName) (network.GetInputLayout mainInputName) (network.GetInputPrecision mainInputName) (network.GetInputDimensions mainInputName)

    network.SetInputResizeAlgorithm(mainInputName, resize_alg_e.RESIZE_BILINEAR)
    network.SetInputLayout(mainInputName, layout)
    network.SetInputPrecision(mainInputName, precision_e.U8)
    network.SetColorFormat(mainInputName, colorformat_e.I420);

    use executableNetwork = new InferenceEngineExecutableNetwork(network, "CPU")

    use request = new InferenceEngineRequest(executableNetwork)

    request.SetBlob(mainInputName, i420Blob)

    request.Infer()

    use outputBlob = request.GetBlob(mainOutputName)
    printfn "Output %d [%A %A] %A" outputBlob.Size outputBlob.Layout outputBlob.Precision outputBlob.Dimensions
    let span = outputBlob.AsReadOnlySpan<float32>();

    let dims = outputBlob.Dimensions
    let maxProposalCount = int dims.[2]
    let objectSize = int dims.[3]

    let results = ImageDetection.SSD.parseOutputs &span 0.5 maxProposalCount objectSize (float32 inputWidth) (float32 inputHeight)
    let converter = ImageConvert()
    use decoded = converter.Convert(rawFrame, inputWidth, inputHeight, FFmpeg.AutoGen.AVPixelFormat.AV_PIX_FMT_BGR24)
    let span = decoded.AsSpan()
    saveImage inputWidth inputHeight &span results


try
    printfn "API = %s" (InferenceEngineLibrary.GetApiVersion())
    //openCVRunner()
    //ffmpegRunner()
    //systemDrawingRunner()
    //nv12FfmpegRunner()
    i420FfmpegRunner();

    System.GC.Collect()
    System.GC.WaitForPendingFinalizers()

with
    | e -> printfn "%s" (e.ToString())

