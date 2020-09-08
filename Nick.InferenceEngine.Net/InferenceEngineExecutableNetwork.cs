using System;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_executable_network_t = IntPtr;

    public class InferenceEngineExecutableNetwork : IDisposable
    {
        private ie_executable_network_t _executable_network;
        internal ie_executable_network_t ExecutableNetwork => _executable_network;

        internal readonly InferenceEngineNetwork _network;

        public int OptimalNumberOfInferRequests
        {
            get
            {
                var result = new ie_param_t();
                ie_exec_network_get_metric(_executable_network, "OPTIMAL_NUMBER_OF_INFER_REQUESTS", ref result).Check(nameof(ie_exec_network_get_metric));
                return result.number;
            }
        }

        public (int min, int max, int step) RangeForAsyncInferRequests
        {
            get
            {
                var result = new ie_param_t();
                ie_exec_network_get_metric(_executable_network, "RANGE_FOR_ASYNC_INFER_REQUESTS", ref result).Check(nameof(ie_exec_network_get_metric));
                unsafe
                {
                    return (result.range_for_async_infer_requests[0], result.range_for_async_infer_requests[1], result.range_for_async_infer_requests[2]);
                }
            }
        }

        public (int min, int max) RangeForStreams
        {
            get
            {
                var result = new ie_param_t();
                ie_exec_network_get_metric(_executable_network, "RANGE_FOR_STREAMS", ref result).Check(nameof(ie_exec_network_get_metric));
                unsafe
                {
                    return (result.range_for_streams[0], result.range_for_streams[1]);
                }
            }
        }

        public InferenceEngineExecutableNetwork(InferenceEngineNetwork network, string deviceName)
        {
            _network = network;
            var core = _network._core;
            var config = new ie_config_t();
            ie_core_load_network(core.Core, _network.Network, deviceName, config, out _executable_network).Check(nameof(ie_core_load_network));
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                ie_exec_network_free(ref _executable_network);

                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~InferenceEngineExecutableNetwork()
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
