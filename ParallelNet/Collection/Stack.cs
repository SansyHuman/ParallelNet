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
    /// Thread-safe non-blocking stack.
    /// </summary>
    /// <typeparam name="T">Type of elements in the stack</typeparam>
    public class Stack<T> : IEnumerable<T>, IReadOnlyCollection<T>
    {
        private class Node
        {
            internal T data;
            internal Node? next;

            internal Node(in T data, Node? next)
            {
                this.data = data;
                this.next = next;
            }
        }

        private Node? head;
        private int count;
        private ulong version;

        /// <summary>
        /// Creates an empty stack.
        /// </summary>
        public Stack()
        {
            head = null;
            count = 0;
            version = 0;
        }

        /// <summary>
        /// Pushs an element on top of the stack.
        /// </summary>
        /// <param name="value">Element to push</param>
        public void Push(in T value)
        {
            Node n = new Node(value, null);

            while (true)
            {
                Node? head = this.head;
                Interlocked.Exchange(ref n.next, head);

                Interlocked.MemoryBarrier();
                if (Interlocked.CompareExchange(ref this.head, n, head) == head)
                {
                    Interlocked.Increment(ref count);
                    Interlocked.Increment(ref version);
                    break;
                }
            }
        }

        /// <summary>
        /// Pops an element from the top of the stack.
        /// </summary>
        /// <returns>Element if succeeded, else failure</returns>
        public Result<T, None> Pop()
        {
            while (true)
            {
                Node? head = this.head;
                Interlocked.MemoryBarrier();

                if (head == null)
                    return (Result<T, None>)None.Value;
                else
                {
                    Node? next = head.next;

                    if (Interlocked.CompareExchange(ref this.head, next, head) == head)
                    {
                        Interlocked.Decrement(ref count);
                        Interlocked.Increment(ref version);
                        return head.data;
                    }
                }
            }
        }

        /// <summary>
        /// Gets an element from the top of the stack but does not pop.
        /// </summary>
        /// <returns>Element if succeeded, else failure</returns>
        public Result<T, None> Peek()
        {
            Node? head = this.head;
            Interlocked.MemoryBarrier();

            if (head == null)
                return (Result<T,None>)None.Value;
            return head.data;
        }

        /// <summary>
        /// Gets <see langword="true"/> if stack is emtpy, else <see langword="false"/>. 
        /// </summary>
        public bool Empty
        {
            get
            {
                bool empty = head == null;
                Interlocked.MemoryBarrier();

                return empty;
            }
        }

        /// <summary>
        /// Clears the stack.
        /// </summary>
        public void Clear()
        {
            while (Pop().ResultType == Result<T, None>.Type.Success) { }
        }

        public int Count => count;

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
            private Stack<T> stack;
            private bool initialized;
            private ulong version;
            private Node? current;

            internal Enumerator(Stack<T> stack)
            {
                this.stack = stack;
                initialized = false;
                version = this.stack.version;
                current = null;
            }

            public T Current
            {
                get
                {
                    if (!initialized)
                        throw new InvalidOperationException("Enumerator not started");
                    if (current == null)
                        throw new InvalidOperationException("Enumerator ended");

                    return current.data;
                }
            }

            object IEnumerator.Current => Current ?? throw new Exception("Unexpected error");

            public void Dispose()
            {
                initialized = false;
            }

            public bool MoveNext()
            {
                if (version != stack.version)
                    throw new InvalidOperationException("Stack revised");

                if (!initialized)
                {
                    current = stack.head;
                    initialized = true;
                    if (current == null)
                        return false;
                    return true;
                }

                if (current == null)
                    return false;

                current = current.next;
                if (current == null)
                    return false;
                return true;
            }

            public void Reset()
            {
                if (version != stack.version)
                    throw new InvalidOperationException("Stack revised");

                initialized = false;
                current = null;
            }
        }
    }
}
