using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
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

            internal Node parent;
            internal Node left;
            internal Node right;
            internal KeyValuePair<K, V>? kv;
            internal Color color;
            internal bool isLeaf;
            internal int flag;
            internal int marker;

            internal const int TRUE = 1;
            internal const int FALSE = 0;
            internal const int DEFAULT_MARKER = -1;

            internal static Node DummyNode
            {
                get
                {
                    return new Node()
                    {
                        parent = null,
                        left = LeafNode,
                        right = LeafNode,
                        kv = null,
                        color = Color.Black,
                        isLeaf = false,
                        flag = FALSE,
                        marker = DEFAULT_MARKER
                    };
                }
            }

            internal static Node CreateNode(in K key, in V value)
            {
                Node node = new Node()
                {
                    parent = null,
                    left = LeafNode,
                    right = LeafNode,
                    kv = new KeyValuePair<K, V>(key, value),
                    color = Color.Red,
                    isLeaf = false,
                    flag = FALSE,
                    marker = DEFAULT_MARKER
                };

                node.left.parent = node;
                node.right.parent = node;

                return node;
            }

            internal static Node LeafNode
            {
                get
                {
                    return new Node()
                    {
                        left = null,
                        right = null,
                        kv = null,
                        color = Color.Black,
                        isLeaf = true,
                        flag = FALSE,
                        marker = DEFAULT_MARKER
                    };
                }
            }

            internal bool IsRoot(Node root)
            {
                if (parent == root)
                    return true;
                return false;
            }

            internal bool IsLeft
            {
                get
                {
                    Node parent = this.parent;
                    if (this == parent.left)
                        return true;
                    return false;
                }
            }

            internal Node? Uncle
            {
                get
                {
                    if (this.parent.isLeaf)
                        return null;

                    if (this.parent.parent.isLeaf)
                        return null;

                    Node parent = this.parent;
                    Node grandparent = parent.parent;
                    if (parent == grandparent.left)
                        return grandparent.right;
                    return grandparent.left;
                }
            }

            internal Node ReplaceParent(Node root)
            {
                Node child;
                if (left.isLeaf)
                {
                    child = right;
                    left = null;
                }
                else
                {
                    child = left;
                    right = null;
                }

                if (IsRoot(root))
                {
                    child.parent = root;
                    root.left = child;
                    parent = null;
                }
                else if (IsLeft)
                {
                    child.parent = parent;
                    parent.left = child;
                }
                else
                {
                    child.parent = parent;
                    parent.right = child;
                }

                return child;
            }
        }

        private Node root;
        private IComparer<K> comparer;
        private int count;
        private ulong version;

        private static ThreadLocal<System.Collections.Generic.List<Node?>> nodesOwnFlag;
        private static ThreadLocal<int> threadIndex;

        static SortedDictionary()
        {
            nodesOwnFlag = new ThreadLocal<System.Collections.Generic.List<Node?>>();
            threadIndex = new ThreadLocal<int>();
        }

        public SortedDictionary() : this(Comparer<K>.Default)
        {

        }

        public SortedDictionary(IComparer<K> keyComparer)
        {
            Node dummy1 = Node.DummyNode;
            Node dummy2 = Node.DummyNode;
            Node dummy3 = Node.DummyNode;
            Node dummy4 = Node.DummyNode;
            Node dummy5 = Node.DummyNode;
            Node dummySibling = Node.DummyNode;
            root = Node.DummyNode;

            dummySibling.parent = root;
            root.parent = dummy5;
            dummy5.parent = dummy4;
            dummy4.parent = dummy3;
            dummy3.parent = dummy2;
            dummy2.parent = dummy1;

            dummy1.left = dummy2;
            dummy2.left = dummy3;
            dummy3.left = dummy4;
            dummy4.left = dummy5;
            dummy5.left = root;
            root.right = dummySibling;

            comparer = keyComparer;
            count = 0;
            version = 0;
        }

        private void LeftRotate(Node node)
        {
            if (node.isLeaf)
                throw new Exception("Invalid rotate on null node");

            if (node.right.isLeaf)
                throw new Exception("Invalid rotate on node with null right child");

            Node right = node.right;
            right.parent = node.parent;
            if (node.IsLeft)
                node.parent.left = right;
            else
                node.parent.right = right;

            node.parent = right;

            node.right = right.left;
            right.left = node;

            if (node.right != null)
            {
                node.right.parent = node;
            }

            Interlocked.Increment(ref version);
        }

        private void RightRotate(Node node)
        {
            if (node.isLeaf)
                throw new Exception("Invalid rotate on null node");

            if (node.left.isLeaf)
                throw new Exception("Invalid rotate on node with null left child");

            Node left = node.left;
            left.parent = node.parent;
            if (node.IsLeft)
                node.parent.left = left;
            else
                node.parent.right = left;

            node.parent = left;

            node.left = left.right;
            left.right = node;

            if (node.left != null)
            {
                node.left.parent = node;
            }

            Interlocked.Increment(ref version);
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

        // True if inserted, false if revised.
        private bool Insert(Node newNode)
        {
            K key = (newNode.kv ?? throw new Exception("Unexpected error")).Key;

            int expected = Node.FALSE;
            while (Interlocked.CompareExchange(ref root.flag, Node.TRUE, expected) != expected) { }
            Interlocked.MemoryBarrier();

            if (root.left.isLeaf)
            {
                root.left = null;
                newNode.flag = Node.TRUE;
                root.left = newNode;
                newNode.parent = root;
                root.flag = Node.FALSE;

                Interlocked.Increment(ref count);
                Interlocked.Increment(ref version);

                return true;
            }

            root.flag = Node.FALSE;

        Restart:
            Node z = null;
            Node currNode = root.left;
            expected = Node.FALSE;
            if (Interlocked.CompareExchange(ref currNode.flag, Node.TRUE, expected) != expected)
                goto Restart;
            Interlocked.MemoryBarrier();

            while (!currNode.isLeaf)
            {
                z = currNode;
                int cmp = comparer.Compare(key, (currNode.kv ?? throw new Exception("Unexpected error")).Key);
                if (cmp > 0)
                    currNode = currNode.right;
                else if (cmp == 0)
                {
                    currNode.kv = newNode.kv;
                    z.flag = Node.FALSE;

                    Interlocked.Increment(ref version);
                    return false;
                }
                else
                    currNode = currNode.left;

                expected = Node.FALSE;
                if (Interlocked.CompareExchange(ref currNode.flag, Node.TRUE, expected) != expected)
                {
                    z.flag = Node.FALSE;
                    goto Restart;
                }

                if (!currNode.isLeaf)
                    z.flag = Node.FALSE;
            }

            Debug.Assert(z != null);

            newNode.flag = Node.TRUE;
            if (!SetupLocalAreaForInsert(z))
            {
                currNode.flag = Node.FALSE;
                z.flag = Node.FALSE;
                goto Restart;
            }

            newNode.parent = z;
            int cmp2 = comparer.Compare(key, (z.kv ?? throw new Exception("Unexpected error")).Key);
            if (cmp2 < 0)
                z.left = newNode;
            else if (cmp2 > 0)
                z.right = newNode;
            else
                throw new Exception("Unexpected error");

            Interlocked.Increment(ref count);
            Interlocked.Increment(ref version);
            return true;
        }

        public void Add(KeyValuePair<K, V> item)
        {
            ClearLocalArea();

            Node newNode = Node.CreateNode(item.Key, item.Value);

            if (!Insert(newNode))
                return;

            Node currNode = newNode;
            Node parent = currNode.parent;
            Node? uncle = null, grandparent = null;

            System.Collections.Generic.List<Node?> localArea = new System.Collections.Generic.List<Node?>();
            localArea.Add(currNode);
            localArea.Add(parent);

            if (parent != null)
                grandparent = parent.parent;

            if (grandparent != null)
            {
                if (grandparent.left == parent)
                    uncle = grandparent.right;
                else
                    uncle = grandparent.left;
            }

            localArea.Add(uncle);
            localArea.Add(grandparent);

            if (currNode.IsRoot(root))
            {
                currNode.color = Node.Color.Black;
                foreach (var node in localArea)
                {
                    if (node != null)
                        node.flag = Node.FALSE;
                }

                return;
            }

            while (true)
            {
                if (currNode.IsRoot(root))
                {
                    currNode.color = Node.Color.Black;
                    break;
                }

                parent = currNode.parent;

                if (parent.color == Node.Color.Black)
                    break;

                uncle = currNode.Uncle;

                if (parent.color == Node.Color.Red && uncle != null && uncle.color == Node.Color.Red)
                {
                    parent.color = Node.Color.Black;
                    uncle.color = Node.Color.Black;
                    parent.parent.color = Node.Color.Red;

                    currNode = MoveInserterUp(currNode, localArea);
                    continue;
                }

                if (parent.IsLeft)
                {
                    if (!currNode.IsLeft)
                    {
                        LeftRotate(parent);
                        currNode = parent;
                    }
                    parent = currNode.parent;
                    uncle = currNode.Uncle;

                    parent.parent.color = Node.Color.Red;
                    parent.color = Node.Color.Black;
                    RightRotate(parent.parent);

                    break;
                }
                else
                {
                    if (currNode.IsLeft)
                    {
                        RightRotate(parent);
                        currNode = parent;
                    }
                    parent = currNode.parent;
                    uncle = currNode.Uncle;

                    parent.parent.color = Node.Color.Red;
                    parent.color = Node.Color.Black;
                    LeftRotate(parent.parent);

                    break;
                }
            }

            foreach (var node in localArea)
            {
                if (node != null)
                    node.flag = Node.FALSE;
            }
        }

        private void InsertFixup(Node x)
        {
            
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

        private Node? Find(K key)
        {
            int expect;
            Node rootNode;

        Restart:
            do
            {
                rootNode = root.left;
                expect = Node.FALSE;
            }
            while (Interlocked.CompareExchange(ref rootNode.flag, Node.TRUE, expect) != expect);
            Interlocked.MemoryBarrier();

            Node y = rootNode;
            Node z = null;

            while (!y.isLeaf)
            {
                z = y;
                int cmp = comparer.Compare(key, (y.kv ?? throw new Exception("Unexpected error")).Key);
                if (cmp == 0)
                    return y;
                else if (cmp > 0)
                    y = y.right;
                else
                    y = y.left;

                expect = Node.FALSE;
                if (Interlocked.CompareExchange(ref y.flag, Node.TRUE, expect) != expect)
                {
                    z.flag = Node.FALSE;
                    Thread.Sleep(10);
                    goto Restart;
                }
                Interlocked.MemoryBarrier();

                if (!y.isLeaf)
                    z.flag = Node.FALSE;
            }

            return null;
        }

        private Node? FindSuccessor(Node deleteNode)
        {
            int expect;

            Node y = deleteNode.right;
            Node z = null;

            while (!y.left.isLeaf)
            {
                z = y;
                y = y.left;

                expect = Node.FALSE;
                if (Interlocked.CompareExchange(ref y.flag, Node.TRUE, expect) != expect)
                {
                    z.flag = Node.FALSE;
                    return null;
                }
                Interlocked.MemoryBarrier();

                z.flag = Node.FALSE;
            }

            return y;
        }

        public bool Remove(K key)
        {
            ClearLocalArea();

        Restart:
            Node? z = Find(key);
            Node? y;
            if (z == null)
                return false;

            if (z.left.isLeaf || z.right.isLeaf)
                y = z;
            else
                y = FindSuccessor(z);

            if (y == null)
            {
                z.flag = Node.FALSE;
                goto Restart;
            }

            if (!SetupLocalAreaForDelete(y, z))
            {
                y.flag = Node.FALSE;
                if (y != z)
                    z.flag = Node.FALSE;
                goto Restart;
            }

            Node replaceNode = y.ReplaceParent(root);

            if (y != z)
                z.kv = y.kv;

            if (!IsInLocalArea(z))
                z.flag = Node.FALSE;

            if (y.color == Node.Color.Black)
                replaceNode = RemoveFixup(replaceNode, z);

            while (!ReleaseMarkersAbove(replaceNode.parent, z)) { }

            ClearLocalArea();

            Interlocked.Decrement(ref count);
            Interlocked.Increment(ref version);

            return true;
        }

        private Node RemoveFixup(Node node, Node z)
        {
            while (node.IsRoot(root) && node.color == Node.Color.Black)
            {
                Node brotherNode;
                if (node.IsLeft)
                {
                    brotherNode = node.parent.right;
                    if (brotherNode.color == Node.Color.Red)
                    {
                        brotherNode.color = Node.Color.Black;
                        node.parent.color = Node.Color.Red;
                        LeftRotate(node.parent);
                        brotherNode = node.parent.right;

                        FixUpCase1(node, brotherNode);
                    }

                    if (brotherNode.left.color == Node.Color.Black && brotherNode.right.color == Node.Color.Black)
                    {
                        brotherNode.color = Node.Color.Red;
                        node = MoveDeleterUp(node);
                    }
                    else if (brotherNode.right.color == Node.Color.Black)
                    {
                        brotherNode.left.color = Node.Color.Black;
                        brotherNode.color = Node.Color.Red;
                        RightRotate(brotherNode);
                        brotherNode = node.parent.right;

                        FixUpCase3(node, brotherNode);
                    }
                    else
                    {
                        brotherNode.color = node.parent.color;
                        node.parent.color = Node.Color.Black;
                        brotherNode.right.color = Node.Color.Black;
                        LeftRotate(node.parent);

                        node = node.parent;
                        break;
                    }
                }
                else
                {
                    brotherNode = node.parent.left;
                    if (brotherNode.color == Node.Color.Red)
                    {
                        brotherNode.color = Node.Color.Black;
                        node.parent.color = Node.Color.Red;
                        RightRotate(node.parent);
                        brotherNode = node.parent.left;

                        FixUpCase1R(node, brotherNode);
                    }

                    if (brotherNode.left.color == Node.Color.Black && brotherNode.right.color == Node.Color.Black)
                    {
                        brotherNode.color = Node.Color.Red;
                        node = MoveDeleterUp(node);
                    }
                    else if (brotherNode.left.color == Node.Color.Black)
                    {
                        brotherNode.right.color = Node.Color.Black;
                        brotherNode.color = Node.Color.Red;
                        LeftRotate(brotherNode);
                        brotherNode = node.parent.left;

                        FixUpCase3R(node, brotherNode);
                    }
                    else
                    {
                        brotherNode.color = node.parent.color;
                        node.parent.color = Node.Color.Black;
                        brotherNode.left.color = Node.Color.Black;
                        RightRotate(node.parent);

                        node = node.parent;
                        break;
                    }
                }
            }

            node.color = Node.Color.Black;
            return node;
        }

        private void CheckLocalAreaCreation()
        {
            if (!nodesOwnFlag.IsValueCreated)
                nodesOwnFlag.Value = new System.Collections.Generic.List<Node?>();
        }

        private void InitializeThreadID()
        {
            if (!threadIndex.IsValueCreated)
                threadIndex.Value = Thread.CurrentThread.ManagedThreadId;
        }

        private void ClearLocalArea()
        {
            CheckLocalAreaCreation();

            if (nodesOwnFlag.Value.Count == 0)
                return;

            foreach (var node in nodesOwnFlag.Value)
            {
                if (node != null)
                    node.flag = Node.FALSE;
            }

            nodesOwnFlag.Value.Clear();
        }

        private bool IsInLocalArea(Node targetNode)
        {
            CheckLocalAreaCreation();

            foreach (var node in nodesOwnFlag.Value)
            {
                if (node == targetNode)
                    return true;
            }

            return false;
        }

        private bool HasNoOthersMarker(Node t, Node z, int tIDToIgnore)
        {
            if (t != z && t.marker != Node.DEFAULT_MARKER && t.marker != tIDToIgnore)
                return false;

            return true;
        }

        bool GetMarkersAbove(Node start, Node z, bool release)
        {
            InitializeThreadID();

            int expect;

            Node pos1, pos2, pos3, pos4;

            pos1 = start.parent;
            if (pos1 != z)
            {
                expect = Node.FALSE;
                if (Interlocked.CompareExchange(ref pos1.flag, Node.TRUE, expect) != expect)
                    return false;

                Interlocked.MemoryBarrier();
            }

            if (pos1 != start.parent || !HasNoOthersMarker(pos1, z, threadIndex.Value))
            {
                if (pos1 != z)
                    pos1.flag = Node.FALSE;

                return false;
            }

            pos2 = pos1.parent;
            if (pos2 != z)
            {
                expect = Node.FALSE;
                if (Interlocked.CompareExchange(ref pos2.flag, Node.TRUE, expect) != expect)
                {
                    if (pos1 != z)
                        pos1.flag = Node.FALSE;
                    return false;
                }
                Interlocked.MemoryBarrier();
            }

            if (pos2 != pos1.parent || !HasNoOthersMarker(pos2, z, threadIndex.Value))
            {
                if (pos1 != z)
                    pos1.flag = Node.FALSE;
                if (pos2 != z)
                    pos2.flag = Node.FALSE;
                return false;
            }

            pos3 = pos2.parent;
            if (pos3 != z)
            {
                expect = Node.FALSE;
                if (Interlocked.CompareExchange(ref pos3.flag, Node.TRUE, expect) != expect)
                {
                    if (pos1 != z)
                        pos1.flag = Node.FALSE;
                    if (pos2 != z)
                        pos2.flag = Node.FALSE;
                    return false;
                }
                Interlocked.MemoryBarrier();
            }

            if (pos3 != pos2.parent || !HasNoOthersMarker(pos3, z, threadIndex.Value))
            {
                if (pos1 != z)
                    pos1.flag = Node.FALSE;
                if (pos2 != z)
                    pos2.flag = Node.FALSE;
                if (pos3 != z)
                    pos3.flag = Node.FALSE;
                return false;
            }

            pos4 = pos3.parent;
            if (pos4 != z)
            {
                expect = Node.FALSE;
                if (Interlocked.CompareExchange(ref pos4.flag, Node.TRUE, expect) != expect)
                {
                    if (pos1 != z)
                        pos1.flag = Node.FALSE;
                    if (pos2 != z)
                        pos2.flag = Node.FALSE;
                    if (pos3 != z)
                        pos3.flag = Node.FALSE;
                    return false;
                }
                Interlocked.MemoryBarrier();
            }

            if (pos4 != pos3.parent || !HasNoOthersMarker(pos4, z, threadIndex.Value))
            {
                if (pos1 != z)
                    pos1.flag = Node.FALSE;
                if (pos2 != z)
                    pos2.flag = Node.FALSE;
                if (pos3 != z)
                    pos3.flag = Node.FALSE;
                if (pos4 != z)
                    pos4.flag = Node.FALSE;
                return false;
            }

            pos1.marker = threadIndex.Value;
            pos2.marker = threadIndex.Value;
            pos3.marker = threadIndex.Value;
            pos4.marker = threadIndex.Value;

            if (release)
            {
                if (pos1 != z)
                    pos1.flag = Node.FALSE;
                if (pos2 != z)
                    pos2.flag = Node.FALSE;
                if (pos3 != z)
                    pos3.flag = Node.FALSE;
                if (pos4 != z)
                    pos4.flag = Node.FALSE;
            }

            return true;
        }

        bool SetupLocalAreaForDelete(Node y, Node z)
        {
            CheckLocalAreaCreation();

            int expect;

            Node x = y.left;
            if (y.left.isLeaf)
                x = y.right;

            expect = Node.FALSE;
            if (Interlocked.CompareExchange(ref x.flag, Node.TRUE, expect) != expect)
                return false;
            Interlocked.MemoryBarrier();

            Node yp = y.parent;
            expect = Node.FALSE;
            if (yp != z && Interlocked.CompareExchange(ref yp.flag, Node.TRUE, expect) != expect)
            {
                x.flag = Node.FALSE;
                return false;
            }
            Interlocked.MemoryBarrier();

            if (yp != y.parent)
            {
                x.flag = Node.FALSE;
                if (yp != z)
                    yp.flag = Node.FALSE;
                return false;
            }

            Node w = y.parent.left;
            if (y.IsLeft)
                w = y.parent.right;

            expect = Node.FALSE;
            if (Interlocked.CompareExchange(ref w.flag, Node.TRUE, expect) != expect)
            {
                x.flag = Node.FALSE;
                if (yp != z)
                    yp.flag = Node.FALSE;
                return false;
            }
            Interlocked.MemoryBarrier();

            Node wlc = null, wrc = null;
            if (!w.isLeaf)
            {
                wlc = w.left;
                wrc = w.right;

                expect = Node.FALSE;
                if (Interlocked.CompareExchange(ref wlc.flag, Node.TRUE, expect) != expect)
                {
                    x.flag = Node.FALSE;
                    w.flag = Node.FALSE;
                    if (yp != z)
                        yp.flag = Node.FALSE;
                    return false;
                }
                Interlocked.MemoryBarrier();

                if (Interlocked.CompareExchange(ref wrc.flag, Node.TRUE, expect) != expect)
                {
                    x.flag = Node.FALSE;
                    w.flag = Node.FALSE;
                    wlc.flag = Node.FALSE;
                    if (yp != z)
                        yp.flag = Node.FALSE;
                    return false;
                }
            }

            if (!GetMarkersAbove(yp, z, true))
            {
                x.flag = Node.FALSE;
                w.flag = Node.FALSE;
                if (!w.isLeaf)
                {
                    wlc.flag = Node.FALSE;
                    wrc.flag = Node.FALSE;
                }
                if (yp != z)
                    yp.flag = Node.FALSE;
                return false;
            }

            nodesOwnFlag.Value.Add(x);
            nodesOwnFlag.Value.Add(w);
            nodesOwnFlag.Value.Add(yp);
            if (!w.isLeaf)
            {
                nodesOwnFlag.Value.Add(wlc);
                nodesOwnFlag.Value.Add(wrc);
            }

            return true;
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
    }
}
