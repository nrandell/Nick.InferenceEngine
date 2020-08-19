using System;
using System.Runtime.InteropServices;
using System.Threading;

using Microsoft.Win32.SafeHandles;

using Nick.Inference;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_blob_t = IntPtr;

    public class Blob : IDisposable
    {
        private ie_blob_t _blob;
        internal ie_blob_t NativeBlob => _blob;
        private bool disposedValue;
        private readonly bool _ownsMemory;

        public Blob(ie_blob_t blob)
        {
            _blob = blob;
        }

        public Blob(in tensor_desc_t description, Span<byte> data)
        {
            ie_blob_make_memory_from_preallocated(in description, MemoryMarshal.GetReference(data), data.Length, out _blob).Check(nameof(ie_blob_make_memory_from_preallocated));
        }

        public unsafe ReadOnlyMemory<T> AsReadOnlyMemory<T>()
            where T : unmanaged
        {
            var size = Size;
            var buffer = new ie_blob_buffer_t();

            ie_blob_get_cbuffer(_blob, ref buffer).Check(nameof(ie_blob_get_cbuffer));
            var manager = new UnmanagedMemoryManager<T>((T*)buffer.cbuffer, Size);
            return manager.Memory;
        }

        public unsafe Memory<T> AsMemory<T>()
            where T : unmanaged
        {
            var size = Size;
            var buffer = new ie_blob_buffer_t();

            ie_blob_get_buffer(_blob, ref buffer).Check(nameof(ie_blob_get_cbuffer));
            var manager = new UnmanagedMemoryManager<T>((T*)buffer.buffer, Size);
            return manager.Memory;
        }

        public Blob(in tensor_desc_t description)
        {
            ie_blob_make_memory(description, out _blob).Check(nameof(ie_blob_make_memory));
            _ownsMemory = true;
        }

        public Blob(Blob yBlob, Blob uBlob, Blob vBlob)
        {
            ie_blob_make_memory_i420(yBlob._blob, uBlob._blob, vBlob._blob, out _blob).Check(nameof(ie_blob_make_memory_i420));
        }

        public Blob(Blob yBlob, Blob uvBlob)
        {
            ie_blob_make_memory_nv12(yBlob._blob, uvBlob._blob, out _blob).Check(nameof(ie_blob_make_memory_nv12));
        }

        public Blob(Blob sourceBlob, in roi_t roi)
        {
            ie_blob_make_memory_with_roi(sourceBlob._blob, roi, out _blob).Check(nameof(ie_blob_make_memory_with_roi));
        }

        public Blob FromArea(int id, int x, int y, int width, int height)
        {
            var roi = new roi_t(id, x, y, width, height);
            return new Blob(this, roi);
        }

        public unsafe ReadOnlySpan<T> AsSpan<T>() where T : unmanaged
        {
            var size = Size;
            var buffer = new ie_blob_buffer_t();

            ie_blob_get_cbuffer(_blob, ref buffer).Check(nameof(ie_blob_get_cbuffer));
            return new ReadOnlySpan<T>(buffer.cbuffer, Size);
        }

        public int Size
        {
            get
            {
                ie_blob_size(_blob, out var size).Check(nameof(ie_blob_size));
                return size;
            }
        }

        public int ByteSize
        {
            get
            {
                ie_blob_byte_size(_blob, out var size).Check(nameof(ie_blob_byte_size));
                return size;
            }
        }

        public dimensions_t Dimensions
        {
            get
            {
                var dimensions = new dimensions_t();
                ie_blob_get_dims(_blob, ref dimensions).Check(nameof(ie_blob_get_dims));
                return dimensions;
            }
        }

        public layout_e Layout
        {
            get
            {
                ie_blob_get_layout(_blob, out var layout).Check(nameof(ie_blob_get_layout));
                return layout;
            }
        }

        public precision_e Precision
        {
            get
            {
                ie_blob_get_precision(_blob, out var precision).Check(nameof(ie_blob_get_precision));
                return precision;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (_ownsMemory)
                {
                    ie_blob_deallocate(ref _blob);
                }
                else
                {
                    ie_blob_free(ref _blob);
                }

                disposedValue = true;
            }
        }

#pragma warning disable MA0055 // Do not use destructor
        ~Blob()
#pragma warning restore MA0055 // Do not use destructor
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
