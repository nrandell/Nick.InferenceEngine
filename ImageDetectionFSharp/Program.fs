#nowarn "9"
// Learn more about F# at http://docs.microsoft.com/dotnet/fsharp

open System
open SixLabors.ImageSharp
open SixLabors.ImageSharp.PixelFormats
open Nick.InferenceEngine.Net
open System.Runtime.InteropServices
open OpenCvSharp

//let networkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\yolo-v2-ava-0001\FP16\yolo-v2-ava-0001.xml"
//let networkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\person-detection-retail-0013\FP16\person-detection-retail-0013.xml"
let networkName = @"C:\Users\nickr\Documents\Intel\OpenVINO\openvino_models\intel\face-detection-0104\FP32\face-detection-0104.xml"
//let imageName = @"C:\Users\nickr\Source\Code\machinelearning-samples\samples\fsharp\getting-started\DeepLearning_ObjectDetection_Onnx\ObjectDetectionConsoleApp\assets\images\dog2.jpg"
let imageName = @"C:\Users\nickr\Downloads\IMG_20200510_085247.jpg"

let loadImage (imageFileName: string) =
    Cv2.ImRead imageFileName

let handlePixels width height (pixelSpan: inref<Span<byte>>) =
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

    let layout = layout_e.NHWC

    network.SetInputResizeAlgorithm(mainInputName, resize_alg_e.RESIZE_BILINEAR)
    network.SetInputLayout(mainInputName, layout)
    network.SetInputPrecision(mainInputName, precision_e.U8)

    use executableNetwork = new InferenceEngineExecutableNetwork(network, "CPU")

    use request = new InferenceEngineRequest(executableNetwork)

    let dimensions = dimensions_t(1, 3, height, width)
    let tensorDescription = tensor_desc_t(layout, &dimensions, precision_e.U8)

    use inputBlob = new SimpleBlob(&tensorDescription, pixelSpan)
    request.SetBlob(mainInputName, inputBlob)

    request.Infer()

    use outputBlob = request.GetBlob(mainOutputName)
    printfn "Output %d [%A %A] %A" outputBlob.Size outputBlob.Layout outputBlob.Precision outputBlob.Dimensions
    let span = outputBlob.AsSpan<float32>();

    let dims = outputBlob.Dimensions
    let maxProposalCount = int dims.[2]
    let objectSize = int dims.[3]

    ImageDetection.SSD.parseOutputs &span 0.5 maxProposalCount objectSize (float32 width) (float32 height)





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

let runner() =

    //use image = Image.Load<Bgr24>(imageName)
    //use bitmap: System.Drawing.Bitmap = downcast (System.Drawing.Image.FromFile imageName) 
    //bitmap.LockBits

    //let mutable pixelSpan = Span<Bgr24>()
    //match image.TryGetSinglePixelSpan(&pixelSpan) with
    //| false -> failwith "Failed to get pixel span"
    //| true ->
    //    handlePixels image &pixelSpan

    use image = loadImage imageName
    let dataSize = image.Channels() * image.Width * image.Height
    let memory =image.DataPointer
    let voidmemory = NativeInterop.NativePtr.toVoidPtr memory

    let pixels = Span<byte>(voidmemory, dataSize)
    handlePixels image.Width image.Height &pixels

try
    printfn "API = %s" (InferenceEngineLibrary.GetApiVersion())
    runner()

with
    | e -> printfn "%s" (e.ToString())

