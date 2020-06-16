using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_executable_network_t = IntPtr;

    public class InferenceEngineExecutableNetwork : IDisposable
    {
        private ie_executable_network_t _executable_network;
        internal ie_executable_network_t ExecutableNetwork => _executable_network;

        internal readonly InferenceEngineNetwork _network;

        public InferenceEngineExecutableNetwork(InferenceEngineNetwork network, string deviceName)
        {
            _network = network;
            var core = _network._core;
            var config = new ie_config_t();
            ie_core_load_network(core.Core, _network.Network, deviceName, config, out _executable_network).Check(nameof(ie_core_load_network));
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
                ie_exec_network_free(ref _executable_network);

                disposedValue = true;
            }
        }

        ~InferenceEngineExecutableNetwork()
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
