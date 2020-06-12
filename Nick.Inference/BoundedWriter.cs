namespace Nick.Inference
{
    public class BoundedWriter<T>
    {
        private readonly LinkedList<T> _freeList;
        private readonly ChannelWriter<BoundedOwner<T>> _writer;
        private readonly object _lock = new object();

        public BoundedWriter(ChannelWriter<BoundedOwner<T>> writer, IEnumerable<T> values)
        {
            var freeList = new LinkedList<T>();
            foreach (var value in values)
            {
                freeList.AddFirst(value);
            }
            _freeList = freeList;
            _writer = writer;
        }

        internal void Release(LinkedListNode<T> node)
        {
            lock (_lock)
            {
                _freeList.AddLast(node);
            }
        }

        public bool TryGet([NotNullWhen(true)] out BoundedOwner<T>? owner)
        {
            LinkedListNode<T>? node;
            lock (_lock)
            {
                var freeList = _freeList;
                if (freeList.Count == 0)
                {
                    node = null;
                }
                else
                {
                    node = _freeList.First;
                    _freeList.Remove(node);
                }
            }

            if (node == null)
            {
                owner = null;
                return false;
            }
            else
            {
                owner = new BoundedOwner<T>(this, node);
                return true;
            }
        }

        public void Send(BoundedOwner<T> owner)
        {
            if (!_writer.TryWrite(owner))
            {
                owner.Dispose();
                Console.WriteLine("Failed to write to channel - dropping");
            }
        }
    }
}
