open System
open Nick.InferenceEngine.Net

let stream = "rtsp://rtsp:Network123@192.168.100.206"



try
    printfn "API = %s" (InferenceEngineLibrary.GetApiVersion())

with
| e -> printfn "%s" (e.ToString())
