using System;
using System.Runtime.InteropServices;

namespace Nick.Inference
{
    public class NativeMemory : IDisposable
    {
        public IntPtr Pointer { get; private set; }
        public int Size { get; }
        public bool Owned { get; }

        public NativeMemory(int size)
        {
            Pointer = Marshal.AllocHGlobal(size);
            Size = size;
            Owned = true;
        }

        public NativeMemory(int size, IntPtr pointer)
        {
            Pointer = pointer;
            Size = size;
            Owned = false;
        }

        public unsafe ReadOnlySpan<T> AsSpan<T>()
            where T : struct
            => new ReadOnlySpan<T>((void*)Pointer, Size / Marshal.SizeOf<T>());

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (Owned && !disposedValue)
            {
                var pointer = Pointer;
                Pointer = IntPtr.Zero;
                Marshal.FreeHGlobal(pointer);

                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~NativeMemory()
#pragma warning restore MA0055 // Do not use destructor
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion

    }
}
