#if USE
using System;

using Nick.Inference;

namespace Nick.InferenceEngine.Net
{
    using static InferenceEngineLibrary;

    public class Blob : IDisposable
    {
        public NativeMemory NativeMemory { get; }

        public tensor_desc_t Description { get; }
        private IntPtr _blob;

        public static implicit operator IntPtr(Blob blob) => blob._blob;

        public dimensions_t Dimensions { get; }

        public Blob(layout_e layout, dimensions_t dimensions, precision_e precision)
        {
            var description = new tensor_desc_t(layout, dimensions, precision);
            var size = 1L;
            for (var i = 0; i < dimensions.ranks; i++)
            {
                size *= dimensions[i];
            }

            size *= PrecisionSize(precision);
            NativeMemory = new NativeMemory((int)size);
            ie_blob_make_memory_from_preallocated(description, NativeMemory.Pointer, size, out var blob).Check(nameof(ie_blob_make_memory_from_preallocated));
            Description = description;
            _blob = blob;
            Dimensions = dimensions;
        }

        private int PrecisionSize(precision_e precision)
        {
            switch (precision)
            {
                case precision_e.U8:
                case precision_e.I8:
                    return 1;

                case precision_e.I16:
                case precision_e.FP16:
                case precision_e.U16:
                    return 2;

                case precision_e.FP32:
                case precision_e.I32:
                    return 4;

                case precision_e.I64:
                    return 8;

                default: throw new InvalidOperationException($"Unknown precision size {precision}");
            }
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
                    NativeMemory.Dispose();
                }

                ie_blob_deallocate(ref _blob).Check(nameof(ie_blob_deallocate));

                disposedValue = true;
            }
        }

        ~Blob()
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
#endif
