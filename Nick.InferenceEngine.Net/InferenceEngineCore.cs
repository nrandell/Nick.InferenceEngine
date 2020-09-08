using System;
using System.Runtime.InteropServices;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_core_t = IntPtr;

    public class InferenceEngineCore : IDisposable
    {
        private ie_core_t _core;

        internal ie_core_t Core => _core;

        public unsafe InferenceEngineCore(string? xmlConfigFile = null)
        {
            ie_core_create(xmlConfigFile ?? string.Empty, out _core).Check(nameof(ie_core_create));
        }

        public unsafe string[] GetAvailableDevices()
        {
            var devices = new ie_available_devices_t();
            ie_core_get_available_devices(_core, ref devices).Check(nameof(ie_core_get_available_devices));
            try
            {
                var numDevices = (int)devices.num_devices;
                var results = new string[numDevices];
                var span = new ReadOnlySpan<IntPtr>(devices.devices, numDevices);
                for (var i = 0; i < numDevices; i++)
                {
                    var device = span[i];
                    results[i] = Marshal.PtrToStringAnsi(device)!;
                }
                return results;
            }
            finally
            {
                ie_core_available_devices_free(ref devices);
            }
        }

        public unsafe CoreVersions[] GetCoreVersions(string device)
        {
            var versions = new ie_core_versions_t();
            ie_core_get_versions(_core, device, ref versions).Check(nameof(ie_core_get_versions));
            try
            {
                var numVersions = (int)versions.num_vers;
                var results = new CoreVersions[numVersions];
                var span = new ReadOnlySpan<ie_core_version_t>(versions.versions, numVersions);
                for (var i = 0; i < numVersions; i++)
                {
                    var version = span[i];
                    var description = Marshal.PtrToStringAnsi(version.description);
                    var buildNumber = Marshal.PtrToStringAnsi(version.build_number);
                    var deviceName = Marshal.PtrToStringAnsi(version.device_name);
                    results[i] = new CoreVersions(deviceName, description, version.major, version.minor, buildNumber);
                }
                return results;
            }
            finally
            {
                ie_core_versions_free(ref versions);
            }
        }

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                ie_core_free(ref _core);

                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~InferenceEngineCore()
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
