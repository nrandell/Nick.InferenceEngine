using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_network_t = IntPtr;
    using size_t = Int64;

    public partial class InferenceEngineNetwork : IDisposable
    {
        private ie_network_t _network;
        internal ie_network_t Network => _network;

        internal readonly InferenceEngineCore _core;

        public InferenceEngineNetwork(InferenceEngineCore core, string xmlFileName, string? weightsFileName = null)
        {
            _core = core;
            var binFileName = weightsFileName ?? Path.ChangeExtension(xmlFileName, ".bin");
            ie_core_read_network(core.Core, xmlFileName, binFileName, out _network).Check(nameof(ie_core_read_network));
        }

        public string NetworkName
        {
            get
            {
                ie_network_get_name(_network, out var name).Check(nameof(ie_network_get_name));
                try
                {
                    return Marshal.PtrToStringAnsi(name)!;
                }
                finally
                {
                    ie_network_name_free(ref name);
                }
            }
        }

        public int NumberOfInputs
        {
            get
            {
                size_t result = default;
                ie_network_get_inputs_number(_network, ref result).Check(nameof(ie_network_get_inputs_number));
                return (int)result;
            }
        }

        public string GetInputName(int number)
        {
            ie_network_get_input_name(_network, number, out var namePtr).Check(nameof(ie_network_get_input_name));
            try
            {
                return Marshal.PtrToStringAnsi(namePtr)!;
            }
            finally
            {
                ie_network_name_free(ref namePtr);
            }
        }

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

        public layout_e GetInputLayout(string inputName)
        {
            layout_e result = default;
            ie_network_get_input_layout(_network, inputName, ref result).Check(nameof(ie_network_get_input_layout));
            return result;
        }

        public void SetInputLayout(string inputName, layout_e layout)
        {
            ie_network_set_input_layout(_network, inputName, layout).Check(nameof(ie_network_set_input_layout));
        }

        public resize_alg_e GetInputResizeAlgorithm(string inputName)
        {
            resize_alg_e result = default;
            ie_network_get_input_resize_algorithm(_network, inputName, ref result).Check(nameof(ie_network_get_input_resize_algorithm));
            return result;
        }

        public void SetInputResizeAlgorithm(string inputName, resize_alg_e resize_algorithm)
        {
            ie_network_set_input_resize_algorithm(_network, inputName, resize_algorithm).Check(nameof(ie_network_set_input_resize_algorithm));
        }

        public dimensions_t GetInputDimensions(string inputName)
        {
            var dims = new dimensions_t();
            ie_network_get_input_dims(_network, inputName, ref dims).Check(nameof(ie_network_get_input_dims));
            return dims;
        }

        public unsafe InputShape[] GetInputShapes()
        {
            var shapes = new input_shapes_t();
            ie_network_get_input_shapes(_network, ref shapes).Check(nameof(ie_network_get_input_shapes));
            try
            {
                var numShapes = (int)shapes.shape_num;
                var results = new InputShape[numShapes];
                var span = new ReadOnlySpan<input_shape_t>(shapes.shapes, numShapes);
                for (var i = 0; i < numShapes; i++)
                {
                    var shape = span[i];
                    results[i] = new InputShape(Marshal.PtrToStringAnsi(shape.name)!, shape.shape);
                }
                return results;
            }
            finally
            {
                ie_network_input_shapes_free(ref shapes);
            }
        }

        public int NumberOfOutputs
        {
            get
            {
                size_t result = default;
                ie_network_get_outputs_number(_network, ref result).Check(nameof(ie_network_get_outputs_number));
                return (int)result;
            }
        }

        public string GetOutputName(int number)
        {
            ie_network_get_output_name(_network, number, out var namePtr).Check(nameof(ie_network_get_output_name));
            var result = Marshal.PtrToStringAnsi(namePtr)!;
            ie_network_name_free(ref namePtr);
            return result;
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

        public layout_e GetOutputLayout(string outputName)
        {
            layout_e result = default;
            ie_network_get_output_layout(_network, outputName, ref result).Check(nameof(ie_network_get_output_layout));
            return result;
        }

        public void SetOutputLayout(string outputName, layout_e layout)
        {
            ie_network_set_output_layout(_network, outputName, layout).Check(nameof(ie_network_set_output_layout));
        }

        public dimensions_t GetOutputDimensions(string outputName)
        {
            var dims = new dimensions_t();
            ie_network_get_output_dims(_network, outputName, ref dims).Check(nameof(ie_network_get_output_dims));
            return dims;
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
