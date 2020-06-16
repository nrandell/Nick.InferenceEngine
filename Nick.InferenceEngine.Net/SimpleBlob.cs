using System;
using System.Buffers;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Nick.Inference;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    using ie_blob_t = IntPtr;

    public class SimpleBlob : IDisposable
    {
        private ie_blob_t _blob;
        internal ie_blob_t Blob => _blob;
        private bool disposedValue;

        public SimpleBlob(ie_blob_t blob)
        {
            _blob = blob;
        }

        public SimpleBlob(in tensor_desc_t description, Span<byte> data)
        {
            ie_blob_make_memory_from_preallocated(in description, MemoryMarshal.GetReference(data), data.Length, out _blob).Check(nameof(ie_blob_make_memory_from_preallocated));
        }

        public unsafe ReadOnlyMemory<T> AsMemory<T>()
            where T: unmanaged
        {
            var size = Size;
            var buffer = new ie_blob_buffer_t();

            ie_blob_get_cbuffer(_blob, ref buffer).Check(nameof(ie_blob_get_cbuffer));
            var manager = new UnmanagedMemoryManager<T>((T*)buffer.cbuffer, Size);
            return manager.Memory;
        }

        public unsafe ReadOnlySpan<T> AsSpan<T>() where T:unmanaged
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
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                ie_blob_free(ref _blob);

                disposedValue = true;
            }
        }

        ~SimpleBlob()
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
