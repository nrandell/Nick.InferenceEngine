using System;

#if NET5_0
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
#endif

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_infer_request_t = IntPtr;

    public class InferenceEngineRequest : IDisposable
    {
        private readonly InferenceEngineExecutableNetwork _executableNetwork;
        private ie_infer_request_t _inferRequest;

#if NET5_0
        private readonly Object _padlock = new object();
        private bool _active;
        private IntPtr _cbArray;
        private GCHandle _handle;

        public void WaitForFinished()
        {
            var padlock = _padlock;
            lock (padlock)
            {
                if (_active)
                {
                    Monitor.Wait(padlock);
                }
            }
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void CompleteCallBack(IntPtr arg)
        {
            var handle = GCHandle.FromIntPtr(arg);
            if (handle.Target is InferenceEngineRequest request)
            {
                request.InstanceCompleteCallBack();
            }
            else
            {
                Console.WriteLine(FormattableString.Invariant($"Cannot get request from handle: {arg}"));
            }
        }

        private void InstanceCompleteCallBack()
        {
            var padlock = _padlock;
            lock (padlock)
            {
                if (!_active)
                {
                    Console.WriteLine("Completed when not active");
                }
                _active = false;
                Monitor.PulseAll(padlock);
            }
        }

#endif

        public unsafe InferenceEngineRequest(InferenceEngineExecutableNetwork executableNetwork)
        {
            _executableNetwork = executableNetwork;
            ie_exec_network_create_infer_request(executableNetwork.ExecutableNetwork, out _inferRequest).Check(nameof(ie_exec_network_create_infer_request));

#if NET5_0
            _handle = GCHandle.Alloc(this);
            var cbArray = Marshal.AllocHGlobal(sizeof(IntPtr) * 2);
            var span = new Span<IntPtr>((void*)cbArray, 2);
            span[0] = (IntPtr)(delegate*<IntPtr, void>)&CompleteCallBack;
            span[1] = GCHandle.ToIntPtr(_handle);
            _cbArray = cbArray;
#endif
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

#if NET5_0
        public void StartInfer()
        {
            var padlock = _padlock;
            lock (padlock)
            {
                if (_active)
                {
                    throw new InvalidOperationException("Already inferring");
                }
                _active = true;
            }

            try
            {
                ie_infer_set_completion_callback(_inferRequest, _cbArray).Check(nameof(ie_infer_set_completion_callback));
                ie_infer_request_infer_async(_inferRequest).Check(nameof(ie_infer_request_infer_async));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to start inferring: {ex}");
                InstanceCompleteCallBack();
            }
        }
#endif

        #region IDisposable Support
        private bool disposedValue; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
#if NET5_0
                if (_active)
                {
                    Console.WriteLine("Still active");
                }

                _handle.Free();
                Marshal.FreeHGlobal(_cbArray);
                _cbArray = IntPtr.Zero;
#endif
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
