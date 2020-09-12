//#define DebugLibrary

using System;
#if !DebugLibrary
using System.IO;
using System.Reflection;
#endif

using System.Runtime.InteropServices;
using System.Text;

#pragma warning disable MA0048 // File name must match type name

namespace Nick.InferenceEngine.Net
{
    using ie_blob_t = IntPtr;
    using ie_core_t = IntPtr;
    using ie_executable_network_t = IntPtr;
    using ie_infer_request_t = IntPtr;
    using ie_network_t = IntPtr;
    using int64_t = Int64;
    using size_t = Int64;

#pragma warning disable IDE1006 // Naming Styles
    public enum IEStatusCode
    {
        NETWORK_NOT_READ = -12,
        INFER_NOT_STARTED = -11,
        NOT_ALLOCATED = -10,
        RESULT_NOT_READY = -9,
        REQUEST_BUSY = -8,
        /*
* @brief exception not of std::exception derived type was thrown
*/
        UNEXPECTED = -7,
        OUT_OF_BOUNDS = -6,
        NOT_FOUND = -5,
        PARAMETER_MISMATCH = -4,
        NETWORK_NOT_LOADED = -3,
        NOT_IMPLEMENTED = -2,
        GENERAL_ERROR = -1,
        OK = 0,
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct ie_version_t
    {
        public IntPtr api_version;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct ie_core_version_t
    {
        public size_t major;
        public size_t minor;
        public IntPtr device_name;
        public IntPtr build_number;
        public IntPtr description;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public unsafe struct ie_core_versions_t
    {
        public ie_core_version_t* versions;
        public size_t num_vers;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public unsafe struct ie_config_t
    {
        public IntPtr name;
        public IntPtr value;
        public ie_config_t* next;
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct ie_param_t
    {
        [FieldOffset(0)]
        public IntPtr parameters;

        [FieldOffset(0)]
        public int number;

        [FieldOffset(0)]
        public fixed int range_for_async_infer_requests[3];

        [FieldOffset(0)]
        public fixed int range_for_streams[2];
    }

    //[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    //public delegate void completeCallBackFunc(IntPtr args);

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public unsafe struct ie_complete_call_back_t
    {
        public delegate* cdecl<IntPtr, void> completeCallBack;
        //public IntPtr completeCallBack;
        public IntPtr args;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public unsafe struct ie_available_devices_t
    {
        public IntPtr* devices;
        public size_t num_devices;
    };

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public unsafe struct dimensions_t
    {
        private const int MaxDims = 8;
        public readonly size_t ranks;
        public fixed size_t dims[MaxDims];

        public void Set(int index, size_t value)
        {
            if ((index < 0) || (index >= ranks))
            {
#pragma warning disable MA0012 // Do not raise reserved exception type
                throw new IndexOutOfRangeException();
#pragma warning restore MA0012 // Do not raise reserved exception type
            }
            dims[index] = value;
        }

        public size_t this[int index]
        {
            get
            {
                if ((index < 0) || (index >= ranks))
                {
#pragma warning disable MA0012 // Do not raise reserved exception type
                    throw new IndexOutOfRangeException();
#pragma warning restore MA0012 // Do not raise reserved exception type
                }
                return dims[index];
            }
            set
            {
                if ((index < 0) || (index >= ranks))
                {
#pragma warning disable MA0012 // Do not raise reserved exception type
                    throw new IndexOutOfRangeException();
#pragma warning restore MA0012 // Do not raise reserved exception type
                }
                dims[index] = value;
            }
        }

        public dimensions_t(params size_t[] dims)
        {
            if (dims.Length > MaxDims)
            {
                throw new ArgumentOutOfRangeException($"Can only have {MaxDims} specified dims with length {dims.Length}");
            }
            this.ranks = dims.Length;
            for (var i = 0; i < dims.Length; i++)
            {
                this.dims[i] = dims[i];
            }
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.Append('[');
            for (var i = 0; i < ranks; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }
                sb.Append(dims[i]);
            }
            sb.Append(']');
            return sb.ToString();
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public struct input_shape_t
    {
        public IntPtr name;
        public dimensions_t shape;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public unsafe struct input_shapes_t
    {
        public input_shape_t* shapes;
        public size_t shape_num;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public readonly struct tensor_desc_t
    {
        public readonly layout_e layout;
        public readonly dimensions_t dims;
        public readonly precision_e precision;

        public tensor_desc_t(layout_e layout, dimensions_t dims, precision_e precision)
        {
            this.layout = layout;
            this.dims = dims;
            this.precision = precision;
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 0)]
    public readonly struct roi_t
    {
        public readonly size_t id;     // ID of a roi
        public readonly size_t posX;   // W upper left coordinate of roi
        public readonly size_t posY;   // H upper left coordinate of roi
        public readonly size_t sizeX;  // W size of roi
        public readonly size_t sizeY;  // H size of roi

        public roi_t(int id, int x, int y, int width, int height)
        {
            this.id = id;
            this.posX = x;
            this.posY = y;
            this.sizeX = width;
            this.sizeY = height;
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public unsafe struct ie_blob_buffer_t
    {
        [FieldOffset(0)]
        public void* buffer;  // buffer can be written

        [FieldOffset(0)]
        public void* cbuffer;  // cbuffer is read-only
    }

    public enum precision_e
    {
        MIXED = 0,  // Mixed value. Can be received from network. No applicable for tensors
        FP32 = 10,  // 32bit floating point value
        FP16 = 11,  // 16bit floating point value
        Q78 = 20,   // 16bit specific signed fixed point precision
        I16 = 30,   // 16bit signed integer value
        U8 = 40,    // 8bit unsigned integer value
        I8 = 50,    // 8bit signed integer value
        U16 = 60,   // 16bit unsigned integer value
        I32 = 70,   // 32bit signed integer value
        BIN = 71,   // 1bit integer value
        I64 = 72,   // 64bit signed integer value
        U64 = 73,   // 64bit unsigned integer value
        U32 = 74,   // 32bit unsigned integer value
        CUSTOM = 80, // custom precision has it's own name and size of elements
        UNSPECIFIED = 255, //< Unspecified value. Used by default
    }

    public enum layout_e
    {
        ANY = 0,    // "any" layout

        // I/O data layouts
        NCHW = 1,
        NHWC = 2,
        NCDHW = 3,
        NDHWC = 4,

        // weight layouts
        OIHW = 64,

        // Scalar
        SCALAR = 95,

        // bias layouts
        C = 96,

        // Single image layout (for mean image)
        CHW = 128,

        // 2D
        HW = 192,
        NC = 193,
        CN = 194,

        BLOCKED = 200,
    }

    public enum resize_alg_e
    {
        NO_RESIZE = 0,
        RESIZE_BILINEAR,
        RESIZE_AREA,
    }

    public enum colorformat_e
    {
        RAW = 0,     //< Plain blob (default), no extra color processing required
        RGB,         //< RGB color format
        BGR,         //< BGR color format, default in DLDT
        RGBX,        //< RGBX color format with X ignored during inference
        BGRX,        //< BGRX color format with X ignored during inference
        NV12,        //< NV12 color format represented as compound Y+UV blob
        I420,        //< I420 color format represented as compound Y+U+V blob
    }

    public class IEStatusCodeException : Exception
    {
        public IEStatusCode Code { get; }

        public IEStatusCodeException(IEStatusCode code, string message) : base($"{message} {code}")
        {
            Code = code;
        }

        public IEStatusCodeException() : base()
        {
        }

        public IEStatusCodeException(string message) : base(message)
        {
        }

        public IEStatusCodeException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public static class InferenceEngineLibrary
    {
        private static void AddDllDirectory(string pathName)
        {
            Console.WriteLine($"Add dll directory '{pathName}'");
            var path = Environment.GetEnvironmentVariable("PATH");
            Environment.SetEnvironmentVariable("PATH", pathName + ";" + path);
        }

        static InferenceEngineLibrary()
        {
#if DebugLibrary
            //AddDllDirectory(Path.Combine(baseDirectory, @"inference_engine\bin\intel64\Debug"));
            //AddDllDirectory(@"C:\Users\nickr\Source\Repos\ie_c_api\build\src\Debug");
            AddDllDirectory(@"C:\Users\nickr\Source\Code\openvino\bin\intel64\Debug");
            AddDllDirectory(@"C:\Users\nickr\Source\Code\openvino\inference-engine\temp\tbb\bin");
            AddDllDirectory(@"C:\Users\nickr\Source\Code\openvino\inference-engine\temp\gna_02.00.00.1047\win64\x64");
#else
            //const string installDirectory = @"C:\Program Files (x86)\IntelSWTools\openvino";
            //const string baseDirectory = installDirectory;
            var baseDirectory = Path.Combine(Path.GetDirectoryName(Assembly.GetCallingAssembly().Location)!, "redist");
            AddDllDirectory(Path.Combine(baseDirectory, @"inference_engine\bin\intel64\Release"));

            AddDllDirectory(Path.Combine(baseDirectory, @"ngraph\lib"));
            AddDllDirectory(Path.Combine(baseDirectory, @"inference_engine\external\tbb\bin"));
            AddDllDirectory(Path.Combine(baseDirectory, @"inference_engine\external\hddl\bin"));
            AddDllDirectory(Path.Combine(baseDirectory, @"intel64_win\compiler"));
#endif
        }

#if DebugLibrary
        private const string Library = "inference_engine_c_apid";
#else
        private const string Library = "inference_engine_c_api";
#endif
        private const CharSet ApiCharSet = CharSet.Ansi;

        internal static void Check(this IEStatusCode code, string message = "IEStatusCode error")
        {
            if (code != IEStatusCode.OK)
            {
                throw new IEStatusCodeException(code, message);
            }
        }

        [DllImport(Library)]
        internal static extern ie_version_t ie_c_api_version();

        [DllImport(Library)]
        internal static extern void ie_version_free(ref ie_version_t version);

        [DllImport(Library)]
        internal static extern void ie_param_free(ref ie_param_t param);

#pragma warning disable CA2101 // Specify marshaling for P/Invoke string arguments
        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_create(string XmlConfigFile, out ie_core_t core);

        [DllImport(Library)]
        internal static extern void ie_core_free(ref ie_core_t core);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_get_versions(ie_core_t core, string deviceName, ref ie_core_versions_t versions);

        [DllImport(Library)]
        internal static extern void ie_core_versions_free(ref ie_core_versions_t versions);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_read_network(ie_core_t core, string xml_file, string? weights_file, out ie_network_t network);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_read_network_from_memory(ie_core_t core, in byte xml_content, size_t xml_content_size, ie_blob_t weight_blob, out ie_network_t network);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_load_network(ie_core_t core, ie_network_t network, string device_name, in ie_config_t config, out ie_executable_network_t exe_network);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_set_config(ie_core_t core, in ie_config_t ie_core_config, string device_name);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_register_plugin(ie_core_t core, string plugin_name, string device_name);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_register_plugins(ie_core_t core, string xml_config_file);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_unregister_plugin(ie_core_t core, string device_name);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_add_extension(ie_core_t core, string extension_path, string device_name);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_get_metric(ie_core_t core, string device_name, string metric_name, ref ie_param_t param_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_core_get_config(ie_core_t core, string device_name, string config_name, ref ie_param_t param_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_core_get_available_devices(ie_core_t core, ref ie_available_devices_t available_devices);

        [DllImport(Library)]
        internal static extern void ie_core_available_devices_free(ref ie_available_devices_t available_devices);

        [DllImport(Library)]
        internal static extern void ie_exec_network_free(ref ie_executable_network_t ie_exec_network);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_exec_network_create_infer_request(ie_executable_network_t ie_exec_network, out ie_infer_request_t request);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_exec_network_get_metric(ie_executable_network_t ie_exec_network, string metric_name, ref ie_param_t param_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_exec_network_set_config(ie_executable_network_t ie_exec_network, in ie_config_t param_config);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_exec_network_get_config(ie_executable_network_t ie_exec_network, string metric_config, ref ie_param_t param_result);

        [DllImport(Library)]
        internal static extern void ie_infer_request_free(ref ie_infer_request_t infer_request);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_infer_request_get_blob(ie_infer_request_t infer_request, string name, out ie_blob_t blob);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_infer_request_set_blob(ie_infer_request_t infer_request, string name, ie_blob_t blob);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_infer_request_infer(ie_infer_request_t infer_request);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_infer_request_infer_async(ie_infer_request_t infer_request);

        [DllImport(Library)]
        //internal static extern IEStatusCode ie_infer_set_completion_callback(ie_infer_request_t infer_request, in ie_complete_call_back_t callback);
        internal static extern IEStatusCode ie_infer_set_completion_callback(ie_infer_request_t infer_request, IntPtr callback);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_infer_request_wait(ie_infer_request_t infer_request, int64_t timeout);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_infer_request_set_batch(ie_infer_request_t infer_request, size_t size);

        [DllImport(Library)]
        internal static extern void ie_network_free(ref ie_network_t network);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_network_get_name(ie_network_t network, out IntPtr name);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_network_get_inputs_number(ie_network_t network, ref size_t size_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_network_get_input_name(ie_network_t network, size_t number, out IntPtr name);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_input_precision(ie_network_t network, string input_name, ref precision_e prec_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_set_input_precision(ie_network_t network, string input_name, precision_e p);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_input_layout(ie_network_t network, string input_name, ref layout_e layout_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_set_input_layout(ie_network_t network, string input_name, layout_e l);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_input_dims(ie_network_t network, string input_name, ref dimensions_t dims_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_input_resize_algorithm(ie_network_t network, string input_name, ref resize_alg_e resize_alg_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_set_input_resize_algorithm(ie_network_t network, string input_name, resize_alg_e resize_algo);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_color_format(ie_network_t network, string input_name, ref colorformat_e colformat_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_set_color_format(ie_network_t network, string input_name, colorformat_e color_format);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_network_get_input_shapes(ie_network_t network, ref input_shapes_t shapes);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_network_reshape(ie_network_t network, input_shapes_t shapes);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_network_get_outputs_number(ie_network_t network, ref size_t size_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_network_get_output_name(ie_network_t network, size_t number, out IntPtr name);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_output_precision(ie_network_t network, string output_name, ref precision_e prec_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_set_output_precision(ie_network_t network, string output_name, precision_e p);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_output_layout(ie_network_t network, string output_name, ref layout_e layout_result);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_set_output_layout(ie_network_t network, string output_name, layout_e l);

        [DllImport(Library, CharSet = ApiCharSet)]
        internal static extern IEStatusCode ie_network_get_output_dims(ie_network_t network, string output_name, ref dimensions_t dims_result);
#pragma warning restore CA2101 // Specify marshaling for P/Invoke string arguments

        [DllImport(Library)]
        internal static extern void ie_network_input_shapes_free(ref input_shapes_t inputShapes);

        [DllImport(Library)]
        internal static extern void ie_network_name_free(ref IntPtr name);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_make_memory(in tensor_desc_t tensorDesc, out ie_blob_t blob);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_make_memory_from_preallocated(in tensor_desc_t tensorDesc, in byte data, size_t size, out ie_blob_t blob);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_make_memory_with_roi(ie_blob_t inputBlob, in roi_t roi, out ie_blob_t blob);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_make_memory_nv12(ie_blob_t y, ie_blob_t uv, out ie_blob_t nv12blob);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_make_memory_i420(ie_blob_t y, ie_blob_t u, ie_blob_t v, out ie_blob_t i420blob);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_size(ie_blob_t blob, out int size_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_byte_size(ie_blob_t blob, out int bsize_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_deallocate(ref ie_blob_t blob);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_get_buffer(ie_blob_t blob, ref ie_blob_buffer_t blob_buffer);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_get_cbuffer(ie_blob_t blob, ref ie_blob_buffer_t blob_cbuffer);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_get_dims(ie_blob_t blob, ref dimensions_t dims_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_get_layout(ie_blob_t blob, out layout_e layout_result);

        [DllImport(Library)]
        internal static extern IEStatusCode ie_blob_get_precision(ie_blob_t blob, out precision_e prec_result);

        [DllImport(Library)]
        internal static extern void ie_blob_free(ref ie_blob_t blob);

        public static string GetApiVersion()
        {
            var version = ie_c_api_version();
            try
            {
                return Marshal.PtrToStringAnsi(version.api_version)!;
            }
            finally
            {
                ie_version_free(ref version);
            }
        }
    }
#pragma warning restore IDE1006 // Naming Styles
}
#pragma warning restore MA0048 // File name must match type name
