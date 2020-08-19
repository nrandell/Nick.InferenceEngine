using System;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_infer_request_t = IntPtr;

    public class InferenceEngineRequest : IDisposable
    {
        private readonly InferenceEngineExecutableNetwork _executableNetwork;
        private ie_infer_request_t _inferRequest;

        public InferenceEngineRequest(InferenceEngineExecutableNetwork executableNetwork)
        {
            _executableNetwork = executableNetwork;
            ie_exec_network_create_infer_request(executableNetwork.ExecutableNetwork, out _inferRequest).Check(nameof(ie_exec_network_create_infer_request));
        }

        public void SetBlob(string name, Blob blob)
        {
            ie_infer_request_set_blob(_inferRequest, name, blob.NativeBlob).Check(nameof(ie_infer_request_set_blob));
        }

        public Blob GetBlob(string name)
        {
            ie_infer_request_get_blob(_inferRequest, name, out var blob).Check(nameof(ie_infer_request_get_blob));
            return new Blob(blob);
        }

        public void Infer()
        {
            ie_infer_request_infer(_inferRequest).Check(nameof(ie_infer_request_infer));
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                ie_infer_request_free(ref _inferRequest);

                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~InferenceEngineRequest()
#pragma warning restore MA0055 // Do not use destructor
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
