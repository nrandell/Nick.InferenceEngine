using System.Runtime.InteropServices;

namespace Nick.InferenceEngine.Net
{
    public static partial class InferenceEngineLibrary
    {
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
}
