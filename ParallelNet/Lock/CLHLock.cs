using ParallelNet.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace ParallelNet.Lock
{
    /// <summary>
    /// CLH lock.
    /// </summary>
    public class CLHLock : IRawLock<CLHLock.Token>, IDisposable
    {
        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct Node
        {
            [FieldOffset(0)]
            internal long locked;
        }

        /// <summary>
        /// Token of the lock.
        /// </summary>
        public struct Token
        {
            internal IntPtr token;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct CachePaddedPtr
        {
            [FieldOffset(0)]
            internal IntPtr ptr;
        }

        private CachePaddedPtr tail;

        private bool disposedValue;

        /// <summary>
        /// Creates new CLH lock.
        /// </summary>
        public CLHLock()
        {
            unsafe
            {
                IntPtr newNode = Marshal.AllocHGlobal(sizeof(Node));
                Node* pNewNode = (Node*)newNode.ToPointer();
                pNewNode->locked = 0;

                tail = new CachePaddedPtr { ptr = newNode };
            }
        }

        /// <summary>
        /// Acquires the lock.
        /// </summary>
        /// <returns>Current thread's ticket</returns>
        public Token Lock()
        {
            unsafe
            {
                IntPtr node = Marshal.AllocHGlobal(sizeof(Node));
                Node* pNode = (Node*)node.ToPointer();
                pNode->locked = 1;

                Interlocked.MemoryBarrier();
                IntPtr prev = Interlocked.Exchange(ref tail.ptr, node);
                Interlocked.MemoryBarrier();
                Node* pPrev = (Node*)prev.ToPointer();

            LockCheck:
                long locked = Interlocked.Read(ref pPrev->locked);
                Interlocked.MemoryBarrier();
                if (locked == 1)
                {
                    Thread.Yield();
                    goto LockCheck;
                }

                Marshal.FreeHGlobal(prev);
                return new Token() { token = node };
            }
        }

        /// <summary>
        /// Releases the lock.
        /// </summary>
        /// <param name="token">Current thread's token</param>
        public void Unlock(Token token)
        {
            unsafe
            {
                Interlocked.MemoryBarrier();
                Interlocked.Exchange(ref ((Node*)token.token.ToPointer())->locked, 0);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                }

                Marshal.FreeHGlobal(tail.ptr);
                disposedValue = true;
            }
        }

        ~CLHLock()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
