using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Lock
{
    public class MCSLock : IRawLock<MCSLock.Token>
    {
        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct Node
        {
            [FieldOffset(0)]
            internal long locked;

            [FieldOffset(8)]
            internal long next;
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

        public MCSLock()
        {
            tail = new CachePaddedPtr() { ptr = IntPtr.Zero };
        }

        public Token Lock()
        {
            unsafe
            {
                IntPtr node = Marshal.AllocHGlobal(sizeof(Node));
                Node* pNode = (Node*)node.ToPointer();
                pNode->locked = 1;
                pNode->next = IntPtr.Zero.ToInt64();

                Interlocked.MemoryBarrier();
                IntPtr prev = Interlocked.Exchange(ref tail.ptr, node);
                Node* pPrev = (Node*)prev.ToPointer();
                Interlocked.MemoryBarrier();

                if (prev == IntPtr.Zero)
                {
                    return new Token() { token = node };
                }

                Interlocked.MemoryBarrier();
                Interlocked.Exchange(ref pPrev->next, node.ToInt64());

            LockCheck:
                long locked = Interlocked.Read(ref pNode->locked);
                Interlocked.MemoryBarrier();
                if (locked == 1)
                {
                    Thread.Yield();
                    goto LockCheck;
                }

                return new Token() { token = node };
            }
        }

        public void Unlock(Token token)
        {
            unsafe
            {
                IntPtr node = token.token;
                Node* pNode = (Node*)node.ToPointer();

                while (true)
                {
                    IntPtr next = new IntPtr(Interlocked.Read(ref pNode->next));
                    Interlocked.MemoryBarrier();

                    if (!(next == IntPtr.Zero))
                    {
                        Marshal.FreeHGlobal(node);
                        Node* pNext = (Node*)next.ToPointer();

                        Interlocked.MemoryBarrier();
                        Interlocked.Exchange(ref pNext->locked, 0);
                        return;
                    }

                    Interlocked.MemoryBarrier();
                    if (Interlocked.CompareExchange(ref tail.ptr, IntPtr.Zero, node) == IntPtr.Zero)
                    {
                        Marshal.FreeHGlobal(node);
                        return;
                    }
                }
            }
        }
    }
}
