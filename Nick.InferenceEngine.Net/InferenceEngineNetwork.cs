using System;
using System.Collections.Generic;
using System.IO;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;
    public partial class InferenceEngineNetwork : IDisposable
    {
        private IntPtr _network;
        private readonly InferenceEngineCore _core;

        internal IntPtr? ExeNetwork { get; private set; }

        internal InferenceEngineNetwork(InferenceEngineCore core, string xmlFileName, string? weightsFileName)
        {
            _core = core;
            var binFileName = weightsFileName ?? Path.ChangeExtension(xmlFileName, ".bin");
            ie_core_read_network(core.Core, xmlFileName, binFileName, out _network).Check("Read network");
        }

        public InferenceEngineRequest CreateRequest(IReadOnlyDictionary<string, Blob> blobs) => new InferenceEngineRequest(this, blobs);

        public string GetInputName(int index) => _network.GetInputName(index);
        public string GetOutputName(int index) => _network.GetOutputName(index);

        public precision_e GetInputPrecision(string inputName)
        {
            var precision = precision_e.CUSTOM;

            ie_network_get_input_precision(_network, inputName, ref precision).Check(nameof(ie_network_get_input_precision));
            return precision;
        }

        public void SetInputPrecision(string inputName, precision_e precision)
        {
            ie_network_set_input_precision(_network, inputName, precision).Check(nameof(ie_network_set_input_precision));
        }

        public precision_e GetOutputPrecision(string outputName)
        {
            var precision = precision_e.CUSTOM;

            ie_network_get_output_precision(_network, outputName, ref precision).Check(nameof(ie_network_get_output_precision));
            return precision;
        }

        public void SetOutputPrecision(string outputName, precision_e precision)
        {
            ie_network_set_output_precision(_network, outputName, precision).Check(nameof(ie_network_set_output_precision));
        }

        public dimensions_t GetInputDimensions(string inputName)
        {
            var dims = new dimensions_t();
            ie_network_get_input_dims(_network, inputName, ref dims).Check(nameof(ie_network_get_input_dims));
            return dims;
        }

        public dimensions_t GetOutputDimensions(string outputName)
        {
            var dims = new dimensions_t();
            ie_network_get_output_dims(_network, outputName, ref dims).Check(nameof(ie_network_get_output_dims));
            return dims;
        }

        public long GetOutputsNumber()
        {
            var outputNumber = 0L;
            ie_network_get_outputs_number(_network, ref outputNumber).Check(nameof(ie_network_get_outputs_number));
            return outputNumber;
        }

        public input_shapes_t GetInputShapes()
        {
            var shapes = new input_shapes_t();
            ie_network_get_input_shapes(_network, ref shapes).Check(nameof(ie_network_get_input_shapes));
            return shapes;
        }

        public void Load(string deviceName)
        {
            var config = new ie_config_t();
            ie_core_load_network(_core.Core, _network, deviceName, config, out var exeNetwork).Check(nameof(ie_core_load_network));
            ExeNetwork = exeNetwork;
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

                var exeNetwork = ExeNetwork;
                if (exeNetwork != null)
                {
                    var network = exeNetwork.Value;
                    ie_exec_network_free(ref network);
                    ExeNetwork = null;
                }
                ie_network_free(ref _network);

                disposedValue = true;
            }
        }

        ~InferenceEngineNetwork()
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
