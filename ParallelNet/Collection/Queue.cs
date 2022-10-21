using ParallelNet.Common;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Collection
{
    /// <summary>
    /// Thread-safe non-blocking queue.
    /// </summary>
    /// <typeparam name="T">Type of elements in the queue</typeparam>
    public class Queue<T> : IEnumerable<T>, IReadOnlyCollection<T>
    {
        private class Node
        {
            internal Option<T> data;
            internal Node? next;
        }

        private Node head;
        private Node tail;
        private int count;
        private ulong version;

        public int Count => count;

        /// <summary>
        /// Creates an empty queue.
        /// </summary>
        public Queue()
        {
            Node sentinel = new Node()
            {
                data = Option<T>.None(),
                next = null
            };

            Interlocked.Exchange(ref head, sentinel);
            Interlocked.Exchange(ref tail, sentinel);
        }

        /// <summary>
        /// Enqueues a value in the queue.
        /// </summary>
        /// <param name="value">Value to enqueue</param>
        public void Enqueue(in T value)
        {
            Node newNode = new Node()
            {
                data = Option<T>.Some(value),
                next = null
            };

            while (true)
            {
                Node tail = this.tail;
                Interlocked.MemoryBarrier();

                Node? next = tail.next;
                Interlocked.MemoryBarrier();

                if (next != null)
                {
                    Interlocked.MemoryBarrier();
                    Interlocked.CompareExchange(ref this.tail, next, tail);
                    continue;
                }

                Interlocked.MemoryBarrier();
                if (Interlocked.CompareExchange(ref tail.next, newNode, null) == null)
                {
                    Interlocked.MemoryBarrier();
                    Interlocked.CompareExchange(ref this.tail, newNode, tail);

                    Interlocked.Increment(ref count);
                    Interlocked.Increment(ref version);
                    break;
                }
            }
        }

        /// <summary>
        /// Dequeues the value.
        /// </summary>
        /// <returns>Value if queue is not empty, else None</returns>
        public Result<T, None> Dequeue()
        {
            while (true)
            {
                Node head = this.head;
                Interlocked.MemoryBarrier();

                Node? next = head.next;
                Interlocked.MemoryBarrier();
                if (next == null)
                    return (Result<T, None>)None.Value;

                Node tail = this.tail;
                if (tail == head)
                {
                    Interlocked.MemoryBarrier();
                    Interlocked.CompareExchange(ref this.tail, next, tail);
                }

                Interlocked.MemoryBarrier();
                if (Interlocked.CompareExchange(ref this.head, next, head) == head)
                {
                    Interlocked.Decrement(ref count);
                    Interlocked.Increment(ref version);
                    return next.data.Value ?? throw new Exception("Unexpected error");
                }
            }
        }

        /// <summary>
        /// Clears the queue. This method is not thread-safe.
        /// </summary>
        public void Clear()
        {
            head.next = null;
            tail = head;
            Interlocked.Exchange(ref count, 0);
            Interlocked.Increment(ref version);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class Enumerator : IEnumerator<T>
        {
            private Queue<T> queue;
            private ulong version;
            private Node? current;

            internal Enumerator(Queue<T> queue)
            {
                this.queue = queue;
                version = this.queue.version;
                current = this.queue.head;
            }

            public T Current
            {
                get
                {
                    if (current == null)
                        throw new InvalidOperationException("Enumerator ended");
                    if (current.data.OptionStatus == Option<T>.Status.None)
                        throw new InvalidOperationException("Enumerator not started");

                    return current.data.Value ?? throw new Exception("Unexpected error");
                }
            }

            object IEnumerator.Current => Current ?? throw new Exception("Unexpected error");

            public bool MoveNext()
            {
                if (version != queue.version)
                    throw new InvalidOperationException("Queue revised");

                if (current == null)
                    return false;

                current = current.next;
                if (current == null)
                    return false;
                return true;
            }

            public void Reset()
            {
                if (version != queue.version)
                    throw new InvalidOperationException("Queue revised");

                current = queue.head;
            }

            public void Dispose()
            {
                current = queue.head;
            }
        }
    }
}
