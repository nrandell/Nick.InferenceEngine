using System;
using System.Runtime.InteropServices;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    public class InferenceEngineCore : IDisposable
    {
        private IntPtr _core;

        internal IntPtr Core => _core;

        public unsafe InferenceEngineCore(string? xmlConfigFile = null)
        {
            var apiVersion = ie_c_api_version();
            Console.WriteLine($"C API Version: {Marshal.PtrToStringAnsi(apiVersion.api_version)}");
            ie_version_free(ref apiVersion);

            ie_core_create(xmlConfigFile ?? string.Empty, out _core).Check(nameof(ie_core_create));

            var versions = new ie_core_versions_t();
            ie_core_get_versions(_core, "CPU", ref versions).Check(nameof(ie_core_get_versions));
            var span = new ReadOnlySpan<ie_core_version_t>(versions.versions, (int)versions.num_vers);

            for (var i = 0; i < span.Length; i++)
            {
                var version = span[i];
                Console.WriteLine($"{Marshal.PtrToStringAnsi(version.description)} {version.major}.{version.minor} {Marshal.PtrToStringAnsi(version.build_number)}");
            }
            ie_core_versions_free(ref versions);
        }

        public InferenceEngineNetwork LoadNetwork(string xmlFileName, string? weightsFileName = null) => new InferenceEngineNetwork(this, xmlFileName, weightsFileName);

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

                ie_core_free(ref _core);

                disposedValue = true;
            }
        }

        ~InferenceEngineCore()
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
