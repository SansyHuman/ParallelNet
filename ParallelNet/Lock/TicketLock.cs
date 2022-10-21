using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Lock
{
    /// <summary>
    /// Ticket lock. Ticket lock ensures the fairness.
    /// </summary>
    public class TicketLock : IRawLock<ulong>
    {
        private ulong curr;
        private ulong next;

        public TicketLock()
        {
            curr = 0;
            next = 0;
        }

        /// <summary>
        /// Acquires the lock.
        /// </summary>
        /// <returns>Current thread's ticket</returns>
        public ulong Lock()
        {
            ulong ticket = Interlocked.Add(ref next, 1) - 1;

            TicketCheck:
            ulong curr = Interlocked.Read(ref this.curr);
            Interlocked.MemoryBarrier();
            if (curr != ticket)
            {
                Thread.Yield();
                goto TicketCheck;
            }

            return ticket;
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <param name="token">Current thread's ticket</param>
        public void Unlock(ulong token)
        {
            Interlocked.MemoryBarrier();
            Interlocked.Exchange(ref curr, token + 1);
        }
    }
}
