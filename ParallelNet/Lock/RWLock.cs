using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Lock
{
    public class RWLock<L, Token, Data> where L : IRawLock<Token>
    {
        private Lock<L, Token, ulong> readCount;
        private L globalLock;
        private Token? globalLockToken;
        private Data data;

        public class ReadLockGuard : IDisposable
        {
            private RWLock<L, Token, Data> @lock;

            private bool disposedValue;

            internal ReadLockGuard(RWLock<L, Token, Data> @lock)
            {
                this.@lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
            }

            public Data InnerData => @lock.data;

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        using (var b = @lock.readCount.Acquire())
                        {
                            b.InnerData--;
                            if (b.InnerData == 0)
                            {
                                Debug.Assert(@lock.globalLockToken != null);
                                @lock.globalLock.Unlock(@lock.globalLockToken);
                            }
                        }
                    }

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        public class WriteLockGuard : IDisposable
        {
            private RWLock<L, Token, Data> @lock;

            private bool disposedValue;

            internal WriteLockGuard(RWLock<L, Token, Data> @lock)
            {
                this.@lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
            }

            public ref Data InnerData => ref @lock.data;

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        Debug.Assert(@lock.globalLockToken != null);
                        @lock.globalLock.Unlock(@lock.globalLockToken);
                    }

                    disposedValue = true;
                }
            }

            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }

        public RWLock(L readLock, L globalLock, Data data)
        {
            readCount = new Lock<L, Token, ulong>(readLock, 0);
            this.globalLock = globalLock;
            globalLockToken = default;
            this.data = data;
        }

        public ReadLockGuard Read()
        {
            using (var b = readCount.Acquire())
            {
                b.InnerData++;
                if (b.InnerData == 1)
                {
                    globalLockToken = globalLock.Lock();
                }

                return new ReadLockGuard(this);
            }
        }

        public WriteLockGuard Write()
        {
            globalLockToken = globalLock.Lock();
            return new WriteLockGuard(this);
        }
    }
}
