module ImageDetection.SSD

open System
open System.Drawing

type BoundingBox =
    {
        Label: string
        X: float32
        Y: float32
        Height: float32
        Width: float32
        Confidence: float32
    }
    member x.Rect = RectangleF(x.X, x.Y, x.Width, x.Height)

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
            let xMin = outputs.[index 3]
            let yMin = outputs.[index 4]
            let xMax = outputs.[index 5]
            let yMax = outputs.[index 6]

            printfn "%d %d %d %f (%f,%f) - (%f,%f) (%d,%d) - (%d,%d)" detection label imageId confidence xMin yMin xMax yMax (int (xMin * width)) (int (yMin * height)) (int (xMax * width)) (int (yMax * height))
        else
            finished <- true

        detection <- detection + 1

            

        
    

