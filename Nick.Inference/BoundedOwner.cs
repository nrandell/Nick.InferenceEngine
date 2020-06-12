namespace Nick.Inference
{
    public class BoundedOwner<T> : IDisposable
    {
        private readonly BoundedWriter<T> _writer;
        private readonly LinkedListNode<T> _node;

        public static implicit operator T(BoundedOwner<T> owner) => owner._node.Value;

        internal BoundedOwner(BoundedWriter<T> writer, LinkedListNode<T> node)
        {
            _writer = writer;
            _node = node;
        }

        public void Dispose()
        {
            if (_node.List != null)
            {
                throw new ObjectDisposedException("Node is already on a list");
            }
            _writer.Release(_node);
        }
    }
}
