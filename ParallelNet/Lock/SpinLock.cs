using ParallelNet.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Lock
{
    /// <summary>
    /// Spinlock.
    /// </summary>
    public class SpinLock : IRawTryLock<None>
    {
        private int locked;

        /// <summary>
        /// Creates a new spinlock.
        /// </summary>
        public SpinLock()
        {
            locked = 0;
        }

        None IRawLock<None>.Lock()
        {
            while (Interlocked.CompareExchange(ref locked, 1, 0) == 1)
            {
                Thread.Yield();
            }

            Interlocked.MemoryBarrier();

            return None.Value;
        }

        void IRawLock<None>.Unlock(None token)
        {
            Interlocked.MemoryBarrier();

            Interlocked.Exchange(ref locked, 0);
        }

        /// <summary>
        /// Locks the spinlock.
        /// </summary>
        public void Lock()
        {
            ((IRawLock<None>)this).Lock();
        }

        /// <summary>
        /// Unlocks the spinlock.
        /// </summary>
        public void Unlock()
        {
            ((IRawLock<None>)this).Unlock(None.Value);
        }

        public Result<None, None> TryLock()
        {
            if (Interlocked.CompareExchange(ref locked, 1, 0) == 1)
                return Result<None, None>.Failed(None.Value);

            Interlocked.MemoryBarrier();

            return Result<None, None>.Suceeded(None.Value);
        }
    }
}
