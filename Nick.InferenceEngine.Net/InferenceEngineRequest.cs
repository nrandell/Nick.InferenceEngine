using System;
using System.Collections.Generic;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;
    public class InferenceEngineRequest : IDisposable
    {
        private readonly InferenceEngineNetwork _network;

        private IntPtr _inferRequest;

        public IReadOnlyDictionary<string, Blob> BlobMappings { get; }

        public InferenceEngineRequest(InferenceEngineNetwork network, IReadOnlyDictionary<string, Blob> blobMappings)
        {
            _network = network;
            var exeNetwork = network.ExeNetwork ?? throw new InvalidOperationException("Network needs loading");
            ie_exec_network_create_infer_request(exeNetwork, out var inferRequest).Check(nameof(ie_exec_network_create_infer_request));

            foreach (var (blobName, blob) in blobMappings)
            {
                ie_infer_request_set_blob(inferRequest, blobName, blob).Check(nameof(ie_infer_request_set_blob));
            }

            _inferRequest = inferRequest;
            BlobMappings = blobMappings;
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
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }

                ie_infer_request_free(ref _inferRequest);
                foreach (var blob in BlobMappings.Values)
                {
                    blob.Dispose();
                }

                disposedValue = true;
            }
        }

        ~InferenceEngineRequest()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
