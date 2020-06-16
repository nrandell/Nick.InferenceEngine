using System;
using System.Buffers;

namespace Nick.Inference
{
    public sealed unsafe class UnmanagedMemoryManager<T> : MemoryManager<T> where T : unmanaged
    {
        private readonly T* _pointer;
        private readonly int _numberOfElements;

        public UnmanagedMemoryManager(T* pointer, int numberOfElements)
        {
            _pointer = pointer;
            _numberOfElements = numberOfElements;
        }

        public override Span<T> GetSpan() => new Span<T>(_pointer, _numberOfElements);

        public override MemoryHandle Pin(int elementIndex = 0)
        {
            if (elementIndex < 0 || elementIndex >= _numberOfElements)
            {
                throw new ArgumentOutOfRangeException(nameof(elementIndex));
            }
            return new MemoryHandle(_pointer + elementIndex);
        }

        public override void Unpin()
        {
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
