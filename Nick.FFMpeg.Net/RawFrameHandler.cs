using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Nick.FFMpeg.Net
{
    public class RawFrameHandler : IRawFrameHandler
    {
        private const int _Producing = 0;
        private const int _Produced = 1;
        private const int _Consuming = 2;
        private const int _Consumed = 3;
        private const int _Finished = 4;

        static RawFrameHandler() => Helper.Initialise();

        private readonly RawFrame _frame;
        private readonly int _id;
        private readonly bool _log;
        private int _state = _Consumed;
        private readonly object? _lock;
        public bool IsFinished => _state == _Finished;

        public RawFrameHandler(int id, bool blocking, bool log = false) : this(new RawFrame(), id, blocking, log)
        {
        }

        public RawFrameHandler(RawFrame frame, int id, bool blocking, bool log = false)
        {
            _frame = frame;
            _id = id;
            _log = log;
            if (blocking)
            {
                _lock = new object();
            }
        }

        private void Block()
        {
            var padlock = _lock;
            if (padlock != null)
            {
                lock (padlock)
                {
                    Monitor.Wait(padlock);
                }
            }
        }

        private void Unblock()
        {
            var padlock = _lock;
            if (padlock != null)
            {
                lock (padlock)
                {
                    Monitor.Pulse(padlock);
                }
            }
        }

        public bool TryConsume(int id, [NotNullWhen(true)] out RawFrame? frame)
        {
            VerifyId(id);
            if (Interlocked.CompareExchange(ref _state, _Consuming, _Produced) == _Produced)
            {
                Log("Consuming");
                frame = _frame;
                return true;
            }

            frame = default;
            return false;
        }

        public bool TryProduce(int id, [NotNullWhen(true)] out RawFrame? frame)
        {
            VerifyId(id);
            if (Interlocked.CompareExchange(ref _state, _Producing, _Consumed) == _Consumed)
            {
                Log("Producing");
                frame = _frame;
                return true;
            }

            frame = default;
            return false;
        }

        private void Verify(int id, RawFrame frame)
        {
            VerifyId(id);
            VerifyFrame(frame);
        }

        private void VerifyFrame(RawFrame frame)
        {
            if (!ReferenceEquals(_frame, frame))
            {
                throw new InvalidOperationException("Different frames");
            }
        }

        private void VerifyId(int id)
        {
            if (id != _id)
            {
                throw new InvalidOperationException("Different id");
            }
        }

        public void Produced(int id, RawFrame producedFrame)
        {
            Verify(id, producedFrame);

            if (Interlocked.CompareExchange(ref _state, _Produced, _Producing) != _Producing)
            {
                Log("Produced failed");
                throw new InvalidOperationException("Cannot produce when consuming");
            }
            Log("Produced");
            Block();
        }

        public void Consumed(int id, RawFrame consumedFrame)
        {
            Verify(id, consumedFrame);
            if (Interlocked.CompareExchange(ref _state, _Consumed, _Consuming) != _Consuming)
            {
                Log("Consumed failed");
                throw new InvalidOperationException("Cannot consume when producing");
            }
            Log("Consumed");
            Unblock();
        }

        public void Finished(int id)
        {
            VerifyId(id);
            Log("Finished");
            _state = _Finished;
        }

        private void Log(string msg)
        {
            if (_log)
            {
                Console.WriteLine(FormattableString.Invariant($"{_id}: {msg}"));
            }
        }
    }
}
