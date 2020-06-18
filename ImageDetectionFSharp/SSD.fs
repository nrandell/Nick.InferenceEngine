module ImageDetection.SSD

open System
open System.Drawing

type BoundingBox =
    {
        Image: int
        Label: string
        X: int
        Y: int
        Height: int
        Width: int
        Confidence: float32
    }
    member x.Rect = Rectangle(x.X, x.Y, x.Width, x.Height)

let parseOutputs (outputs : inref<ReadOnlySpan<float32>>) threshold maxProposalCount objectSize width height =
    let results = ResizeArray()
    let mutable detection = 0
    let mutable finished = false
    while detection < maxProposalCount && not finished do
        let index n = (detection * objectSize) + n
        let imageId = int outputs.[index 0]
        if imageId >= 0 then
            let label = int outputs.[index 1]
            let confidence = outputs.[index 2]
            let xMin = int (outputs.[index 3] * width)
            let yMin = int (outputs.[index 4] * height)
            let xMax = int (outputs.[index 5] * width)
            let yMax = int (outputs.[index 6] * height)

            printfn "%d %d %d %f (%d,%d) - (%d,%d)" detection label imageId confidence xMin yMin xMax yMax
            let box = { Image=imageId; Label= "face"; X = xMin; Y = yMin; Width = xMax - xMin; Height = yMax - yMin; Confidence = confidence}
            results.Add box

        else
            finished <- true

        detection <- detection + 1
    results.ToArray()


            

        
    

