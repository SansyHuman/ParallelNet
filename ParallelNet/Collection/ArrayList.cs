using ParallelNet.Common;

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Collection
{
    /// <summary>
    /// Thread-safe non-blocking array list. Because of the limitation
    /// of CAS operation, T must be a reference type. To use value type,
    /// wrap it with reference type.
    /// </summary>
    /// <typeparam name="T">Type of list elements</typeparam>
    public class ArrayList<T> : ICollection<T>, IEnumerable<T>, IList<T>, IReadOnlyCollection<T>, IReadOnlyList<T>
        where T : class
    {
        private class Descriptor
        {
            internal class WriteOperation
            {
                internal T? oldValue;
                internal T? newValue;
                internal int location;
                internal bool pending;

                internal WriteOperation(T? oldValue, T? newValue, int location)
                {
                    this.oldValue = oldValue;
                    this.newValue = newValue;
                    this.location = location;
                    pending = true;
                }

                internal WriteOperation()
                {
                    oldValue = default;
                    newValue = default;
                    location = -1;
                    pending = false;
                }
            }

            internal int count;
            internal WriteOperation writeOp;
        }

        private T[]?[] memory;
        private ArrayPool<T> pool;
        private Descriptor descriptor;
        private ulong version;
        private const int FIRST_BUCKET_SIZE = 8;

        private IEqualityComparer<T> comparer;

        /// <summary>
        /// Creates an empty list.
        /// </summary>
        public ArrayList() : this(EqualityComparer<T>.Default)
        {
            
        }

        /// <summary>
        /// Creates an empty list with comparer.
        /// </summary>
        /// <param name="comparer">Equality comparer</param>
        public ArrayList(IEqualityComparer<T> comparer)
        {
            memory = new T[29][];
            Array.Fill(memory, null);
            pool = ArrayPool<T>.Shared;
            descriptor = new Descriptor()
            {
                count = 0,
                writeOp = new Descriptor.WriteOperation()
            };
            version = 0;

            memory[0] = pool.Rent(FIRST_BUCKET_SIZE);
            Array.Clear(memory[0] ?? throw new Exception("Unexpected error"), 0, FIRST_BUCKET_SIZE);

            this.comparer = comparer;
        }

        private void AllocateBucket(int bucket)
        {
            int bucketSize = FIRST_BUCKET_SIZE * (1 << bucket);

            T[] mem = pool.Rent(bucketSize);
            Array.Clear(mem, 0, bucketSize);

            Interlocked.MemoryBarrier();
            if (Interlocked.CompareExchange(ref memory[bucket], mem, null) != null)
            {
                pool.Return(mem);
                return;
            }
        }

        private static int HighestBit(int num)
        {
            return 31 - BitOperations.LeadingZeroCount((uint)num);
        }

        /// <summary>
        /// Pre-allocates memory in the list at least the <paramref name="size"/>.
        /// </summary>
        /// <param name="size">Least size of the memory in the list</param>
        public void Reserve(int size)
        {
            int i = HighestBit(descriptor.count + FIRST_BUCKET_SIZE - 1);
            i -= HighestBit(FIRST_BUCKET_SIZE);
            if (i < 0)
                i = 0;

            while (i < HighestBit(size + FIRST_BUCKET_SIZE - 1) - HighestBit(FIRST_BUCKET_SIZE))
            {
                i++;
                AllocateBucket(i);
            }
        }

        private ref T At(int index)
        {
            int pos = index + FIRST_BUCKET_SIZE;
            int hibit = HighestBit(pos);
            int idx = pos ^ (1 << hibit);
            return ref (memory[hibit - HighestBit(FIRST_BUCKET_SIZE)] ?? throw new ArgumentOutOfRangeException(nameof(index)))[idx];
        }

        private void CompleteWrite(ref Descriptor.WriteOperation writeOp)
        {
            if (writeOp.pending)
            {
                Interlocked.MemoryBarrier();
                Interlocked.CompareExchange(ref At(writeOp.location), writeOp.newValue, writeOp.oldValue);
                Interlocked.Increment(ref version);
                writeOp.pending = false;
            }
        }

        public T this[int index] 
        { 
            get
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                return At(index);
            }
            set
            {
                if (index < 0 || index >= Count)
                    throw new ArgumentOutOfRangeException(nameof(index));

                At(index) = value;
                Interlocked.Increment(ref version);
            }
        }

        public int Count
        {
            get
            {
                Descriptor descriptor = this.descriptor;
                Interlocked.MemoryBarrier();

                int size = descriptor.count;
                if (descriptor.writeOp.pending)
                    size -= 1;

                return size;
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            Descriptor current;
            Descriptor next;
            do
            {
                current = descriptor;
                CompleteWrite(ref current.writeOp);

                int bucket = HighestBit(current.count + FIRST_BUCKET_SIZE) - HighestBit(FIRST_BUCKET_SIZE);
                if (memory[bucket] == null)
                    AllocateBucket(bucket);

                Descriptor.WriteOperation writeOp = new Descriptor.WriteOperation(
                    At(current.count),
                    item,
                    current.count
                    );
                next = new Descriptor()
                {
                    count = current.count + 1,
                    writeOp = writeOp
                };

                Interlocked.MemoryBarrier();
            } while (Interlocked.CompareExchange(ref descriptor, next, current) != current);

            CompleteWrite(ref next.writeOp);
        }

        /// <summary>
        /// Removes an element from the back of the list.
        /// </summary>
        /// <returns>Removed element if succeeded, else None</returns>
        public Result<T, None> PopBack()
        {
            Descriptor current;
            Descriptor next;
            T elem;

            do
            {
                current = descriptor;
                CompleteWrite(ref current.writeOp);
                if (current.count <= 0)
                    return (Result<T, None>)None.Value;

                elem = At(current.count - 1);
                next = new Descriptor()
                {
                    count = current.count - 1,
                    writeOp = new Descriptor.WriteOperation()
                };

                Interlocked.MemoryBarrier();
            } while (Interlocked.CompareExchange(ref descriptor, next, current) != current);

            Interlocked.Increment(ref version);
            return elem;
        }

        /// <inheritdoc/>
        /// <remarks>This method is not thread-safe</remarks>
        public void Clear()
        {
            descriptor = new Descriptor()
            {
                count = 0,
                writeOp = new Descriptor.WriteOperation()
            };
            for (int i = 1; i < memory.Length; i++)
            {
                T[]? mem = memory[i];
                if (mem == null)
                    break;
                pool.Return(mem, true);
                memory[i] = null;
            }

            Array.Clear(memory[0] ?? throw new Exception("Unexpected error"), 0, FIRST_BUCKET_SIZE);
            Interlocked.Increment(ref version);
        }

        /// <summary>
        /// Deallocates empty spaces so that the total space is about 4 times of number of elements.
        /// This method is not thread-safe.
        /// </summary>
        public void Reduce()
        {
            int pos = Count + FIRST_BUCKET_SIZE - 1;
            int hibit = HighestBit(pos);
            int bucket = hibit - HighestBit(FIRST_BUCKET_SIZE) + 2;

            for (int i = bucket; i < memory.Length; i++)
            {
                T[]? mem = memory[i];
                if (mem == null)
                    break;
                pool.Return(mem, true);
                memory[i] = null;
            }
        }

        public bool Contains(T item)
        {
            for (int i = 0; i < Count; i++)
            {
                T elem = At(i);
                Interlocked.MemoryBarrier();

                if (comparer.Equals(item, elem))
                    return true;
            }

            return false;
        }

        /// <inheritdoc/>
        /// <remarks>This method is not thread-safe</remarks>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (arrayIndex + Count > array.Length)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            for (int i = 0; i < Count; i++)
                array[arrayIndex + i] = At(i);
        }

        public IEnumerator<T> GetEnumerator()
        {
            return new Enumerator(this);
        }

        public int IndexOf(T item)
        {
            for (int i = 0; i < Count; i++)
            {
                T elem = At(i);
                Interlocked.MemoryBarrier();

                if (comparer.Equals(item, elem))
                    return i;
            }

            return -1;
        }

        /// <inheritdoc/>
        /// <remarks>This method is not thread-safe</remarks>
        public void Insert(int index, T item)
        {
            if (index < 0 || index > Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            Add(item);
            for (int i = Count - 1; i > index; i--)
            {
                this[i] = this[i - 1];
            }
            this[index] = item;
        }

        /// <inheritdoc/>
        /// <remarks>This method is not thread-safe</remarks>
        public bool Remove(T item)
        {
            int index = IndexOf(item);
            if (index == -1)
                return false;

            RemoveAt(index);
            return true;
        }

        /// <inheritdoc/>
        /// <remarks>This method is not thread-safe</remarks>
        public void RemoveAt(int index)
        {
            if (index < 0 || index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            T last = PopBack().ResultValue ?? throw new Exception("Unexpected error");
            for (int i = index; i < Count - 1; i++)
            {
                this[i] = this[i + 1];
            }
            this[Count - 1] = last;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class Enumerator : IEnumerator<T>
        {
            private ArrayList<T> list;
            private int index;
            private ulong version;

            internal Enumerator(ArrayList<T> list)
            {
                this.list = list;
                index = -2;
                version = list.version;
            }

            public T Current
            {
                get
                {
                    if (index == -2)
                        throw new InvalidOperationException("Enumerator not started");
                    if (index == -1)
                        throw new InvalidOperationException("Enumerator ended");

                    return list[index];
                }
            }

            object IEnumerator.Current => Current;

            public void Dispose()
            {
                index = -2;
            }

            public bool MoveNext()
            {
                if (version != list.version)
                    throw new InvalidOperationException("ArrayList revised");

                if (index == -2)
                {
                    if (list.Count == 0)
                        index = -1;
                    else
                    {
                        index = 0;
                        return true;
                    }
                }

                if (index == -1)
                    return false;

                index++;
                if (index >= list.Count)
                {
                    index = -1;
                    return false;
                }
                return true;
            }

            public void Reset()
            {
                if (version != list.version)
                    throw new InvalidOperationException("ArrayList revised");

                index = -2;
            }
        }
    }
}
