using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Lock
{
    /// <summary>
    /// Safe lock class.
    /// </summary>
    /// <typeparam name="L">Type of raw lock</typeparam>
    /// <typeparam name="Token">Type of token of raw lock</typeparam>
    /// <typeparam name="Data">Type of data inside the lock</typeparam>
    public class Lock<L, Token, Data> where L : IRawLock<Token>
    {
        private L rawLock;
        private Data data;

        /// <summary>
        /// Lock guard that can access to the data inside the lock. Dispose this
        /// object to release the lock.
        /// </summary>
        public class LockGuard : IDisposable
        {
            private Lock<L, Token, Data> @lock;
            private Token token;

            private bool disposedValue;

            internal LockGuard(Lock<L, Token, Data> @lock, Token token)
            {
                this.@lock = @lock ?? throw new ArgumentNullException(nameof(@lock));
                this.token = token;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (!disposedValue)
                {
                    if (disposing)
                    {
                        @lock.rawLock.Unlock(token);
                    }

                    disposedValue = true;
                }
            }

            /// <summary>
            /// Releases the lock.
            /// </summary>
            public void Dispose()
            {
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Gets inner data in the lock.
            /// </summary>
            public ref Data InnerData => ref @lock.data;
        }

        /// <summary>
        /// Creates a lock with raw lock and initial data.
        /// </summary>
        /// <param name="rawLock">Raw lock object to use in the lock</param>
        /// <param name="data">Initial data in the lock object</param>
        public Lock(L rawLock, Data data)
        {
            this.rawLock = rawLock;
            this.data = data;
        }

        /// <summary>
        /// Acquires the lock and gets lock guard.
        /// </summary>
        /// <returns>Lock guard</returns>
        public LockGuard Acquire()
        {
            var token = rawLock.Lock();
            return new LockGuard(this, token);
        }
    }
}
