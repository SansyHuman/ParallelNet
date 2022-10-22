using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace ParallelNet.Collection
{
    public class SortedDictionary<K, V> : ICollection<KeyValuePair<K, V>>, IDictionary<K, V>, IEnumerable<KeyValuePair<K, V>>, IReadOnlyCollection<KeyValuePair<K, V>>, IReadOnlyDictionary<K, V>
    {
        private class Node
        {
            internal enum Color
            {
                Red, Black
            }

            internal KeyValuePair<K, V>? kv;
            internal Node parent;
            internal Node? left;
            internal Node? right;
            internal Color color;

            internal Node(in KeyValuePair<K, V> kv, Node parent)
            {
                this.kv = kv;
                this.parent = parent;
                left = null;
                right = null;
                color = Color.Black;
            }

            internal Node()
            {
                kv = null;
                parent = this;
                left = null;
                right = null;
                color = Color.Black;
            }
        }

        private readonly Node rootParent;
        private Node? root;
        private IComparer<K> comparer;
        private int count;
        private ulong version;

        public SortedDictionary() : this(Comparer<K>.Default)
        {

        }

        public SortedDictionary(IComparer<K> keyComparer)
        {
            rootParent = new Node();
            root = null;
            comparer = keyComparer;
            count = 0;
            version = 0;
        }

        private void LeftRotate(Node x)
        {
            Node y = x.right ?? throw new Exception("Unexpected error");
            
            x.right = y.left;
            if (y.left != null)
                y.left.parent = x;

            y.parent = x.parent;
            if (x.parent == rootParent)
            {
                root = y;
                rootParent.left = y;
            }
            else if (x == x.parent.left)
                x.parent.left = y;
            else
                x.parent.right = y;

            y.left = x;
            x.parent = y;
        }

        private void RightRotate(Node x)
        {
            Node y = x.left ?? throw new Exception("Unexpected error");
            
            x.left = y.right;
            if (y.right != null)
                y.right.parent = x;

            y.parent = x.parent;
            if (x.parent == rootParent)
            {
                root = y;
                rootParent.left = y;
            }
            else if (x == x.parent.left)
                x.parent.left = y;
            else
                x.parent.right = y;

            y.right = x;
            x.parent = y;
        }

        public V this[K key] => throw new NotImplementedException();

        V IDictionary<K, V>.this[K key] { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IEnumerable<K> Keys => throw new NotImplementedException();

        public IEnumerable<V> Values => throw new NotImplementedException();

        public int Count => throw new NotImplementedException();

        public bool IsReadOnly => throw new NotImplementedException();

        ICollection<K> IDictionary<K, V>.Keys => throw new NotImplementedException();

        ICollection<V> IDictionary<K, V>.Values => throw new NotImplementedException();

        public void Add(K key, V value)
        {
            Add(new KeyValuePair<K, V>(key, value));
        }

        public void Add(KeyValuePair<K, V> item)
        {
            Node z = rootParent;
            Node? y = root;

            while (y != null)
            {
                z = y;

                int comp = comparer.Compare(item.Key, (y.kv ?? throw new Exception("Unexpected error")).Key);
                if (comp < 0)
                    y = y.left;
                else if (comp == 0)
                {
                    y.kv = item;
                    return;
                }
                else
                    y = y.right;
            }

            Node x = new Node(item, z);
            if (z == rootParent)
            {
                root = x;
                rootParent.left = x;
            }
            else if (comparer.Compare(item.Key, (z.kv ?? throw new Exception("Unexpected error")).Key) < 0)
                z.left = x;
            else
                z.right = x;

            x.color = Node.Color.Red;
            InsertFixup(x);

            Interlocked.Increment(ref count);
            Interlocked.Increment(ref version);
        }

        private void InsertFixup(Node x)
        {
            while (x.parent.color == Node.Color.Red)
            {
                if (x.parent == x.parent.parent.left)
                {
                    Node? y = x.parent.parent.right;
                    if (y != null && y.color == Node.Color.Red)
                    {
                        x.parent.color = Node.Color.Black;
                        y.color = Node.Color.Black;
                        x.parent.parent.color = Node.Color.Red;
                        x = x.parent.parent;
                    }
                    else
                    {
                        if (x == x.parent.right)
                        {
                            x = x.parent;
                            LeftRotate(x);
                        }
                        x.parent.color = Node.Color.Black;
                        x.parent.parent.color = Node.Color.Red;
                        RightRotate(x.parent.parent);
                    }
                }
                else // x.parent == x.parent.parent.right
                {
                    Node? y = x.parent.parent.left;
                    if (y != null && y.color == Node.Color.Red)
                    {
                        x.parent.color = Node.Color.Black;
                        y.color = Node.Color.Black;
                        x.parent.parent.color = Node.Color.Red;
                        x = x.parent.parent;
                    }
                    else
                    {
                        if (x == x.parent.left)
                        {
                            x = x.parent;
                            RightRotate(x);
                        }
                        x.parent.color = Node.Color.Black;
                        x.parent.parent.color = Node.Color.Red;
                        LeftRotate(x.parent.parent);
                    }
                }
            }

            (root ?? throw new Exception("Unexpected error")).color = Node.Color.Black;
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public bool ContainsKey(K key)
        {
            throw new NotImplementedException();
        }

        public void CopyTo(KeyValuePair<K, V>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool Remove(K key)
        {
            Node? current = root;
            while (current != null)
            {
                int comp = comparer.Compare(key, (current.kv ?? throw new Exception("Unexpected error")).Key);
                if (comp == 0)
                {
                    Delete(current);
                    return true;
                }
                else if (comp < 0)
                    current = current.left;
                else
                    current = current.right;
            }
            return false;
        }

        private void Delete(Node z)
        {
            Node y;
            if (z.left == null || z.right == null)
                y = z;
            else
                y = Successor(z);

            Node? x;
            if (y.left != null)
                x = y.left;
            else
                x = y.right;
            if (x != null)
                x.parent = y.parent;

            if (y.parent == rootParent)
            {
                root = x;
                rootParent.left = x;
            }
            else
            {
                if (y == y.parent.left)
                    y.parent.left = x;
                else
                    y.parent.right = x;
            }

            if (y != z)
                z.kv = y.kv;
            if (y.color == Node.Color.Black && x != null)
                DeleteFixup(x);

            Interlocked.Decrement(ref count);
            Interlocked.Increment(ref version);
        }

        private void DeleteFixup(Node x)
        {
            while (x != root && x.color == Node.Color.Black)
            {
                if (x == x.parent.left)
                {
                    Node? w = x.parent.right;
                    if (w != null && w.color == Node.Color.Red)
                    {
                        w.color = Node.Color.Black;
                        x.parent.color = Node.Color.Red;
                        LeftRotate(x.parent);
                        w = x.parent.right;
                    }

                    if (w != null &&
                        w.left != null &&
                        w.left.color == Node.Color.Black &&
                        w.right != null &&
                        w.right.color == Node.Color.Black)
                    {
                        w.color = Node.Color.Red;
                        x = x.parent;
                    }
                    else
                    {
                        if (w != null &&
                            w.right != null &&
                            w.right.color == Node.Color.Black)
                        {
                            if (w.left != null)
                                w.left.color = Node.Color.Black;
                            w.color = Node.Color.Red;
                            RightRotate(w);
                            w = x.parent.right;
                        }

                        if (w != null)
                            w.color = x.parent.color;
                        x.parent.color = Node.Color.Black;
                        if (w != null && w.right != null)
                            w.right.color = Node.Color.Black;
                        LeftRotate(x.parent);
                        x = root ?? throw new Exception("Unexpected error");
                    }
                }
                else // x == x.parent.right
                {
                    Node? w = x.parent.left;
                    if (w != null && w.color == Node.Color.Red)
                    {
                        w.color = Node.Color.Black;
                        x.parent.color = Node.Color.Red;
                        RightRotate(x.parent);
                        w = x.parent.left;
                    }

                    if (w != null &&
                        w.left != null &&
                        w.left.color == Node.Color.Black &&
                        w.right != null &&
                        w.right.color == Node.Color.Black)
                    {
                        w.color = Node.Color.Red;
                        x = x.parent;
                    }
                    else
                    {
                        if (w != null &&
                            w.left != null &&
                            w.left.color == Node.Color.Black)
                        {
                            if (w.right != null)
                                w.right.color = Node.Color.Black;
                            w.color = Node.Color.Red;
                            LeftRotate(w);
                            w = x.parent.left;
                        }

                        if (w != null)
                            w.color = x.parent.color;
                        x.parent.color = Node.Color.Black;
                        if (w != null && w.left != null)
                            w.left.color = Node.Color.Black;
                        RightRotate(x.parent);
                        x = root;
                    }
                }
            }

            if (x != null)
                x.color = Node.Color.Black;
        }

        private Node Successor(Node n)
        {
            if (n.right != null)
                return MinValue(n.right);

            Node p = n.parent;
            while (p != rootParent && n == p.right)
            {
                n = p;
                p = p.parent;
            }

            return p;
        }

        private Node MinValue(Node node)
        {
            Node current = node;
            while (current.left != null)
                current = current.left;

            return current;
        }

        public bool Remove(KeyValuePair<K, V> item)
        {
            throw new NotImplementedException();
        }

        public bool TryGetValue(K key, [MaybeNullWhen(false)] out V value)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public void Inorder(Action<K, V> forEach)
        {
            System.Collections.Generic.Stack<Node> s = new System.Collections.Generic.Stack<Node>();
            Node? current = root;

            while (current != null || s.Count != 0)
            {
                while (current != null)
                {
                    s.Push(current);
                    current = current.left;
                }

                current = s.Pop();

                forEach(
                    (current.kv ?? throw new Exception("Unexpected exception")).Key,
                    (current.kv ?? throw new Exception("Unexpected exception")).Value
                    );

                current = current.right;
            }
        }
    }
}
