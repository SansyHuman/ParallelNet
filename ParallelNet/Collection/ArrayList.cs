using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
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

        private T[][] memory;
        private ArrayPool<T> pool;
        private Descriptor descriptor;
        private const int FIRST_BUCKET_SIZE = 8;

        public ArrayList()
        {
            memory = new T[29][];
            Array.Fill(memory, null);
            pool = ArrayPool<T>.Shared;
            descriptor = new Descriptor()
            {
                count = 0,
                writeOp = new Descriptor.WriteOperation()
            };

            memory[0] = pool.Rent(FIRST_BUCKET_SIZE);
            Array.Clear(memory[0], 0, FIRST_BUCKET_SIZE);
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
            return ref memory[hibit - HighestBit(FIRST_BUCKET_SIZE)][idx];
        }

        private void CompleteWrite(ref Descriptor.WriteOperation writeOp)
        {
            if (writeOp.pending)
            {
                Interlocked.MemoryBarrier();
                Interlocked.CompareExchange(ref At(writeOp.location), writeOp.newValue, writeOp.oldValue);
                writeOp.pending = false;
            }
        }

        public T this[int index] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

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

        public bool IsReadOnly => throw new NotImplementedException();

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
            } while (Interlocked.CompareExchange(ref descriptor, next, current) == current);

            CompleteWrite(ref next.writeOp);
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<T> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public int IndexOf(T item)
        {
            throw new NotImplementedException();
        }

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
