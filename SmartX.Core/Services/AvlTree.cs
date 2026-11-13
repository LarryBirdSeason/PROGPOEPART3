namespace SmartX.Core.Services;

public sealed record AvlRange<TKey, TValue>(
    List<KeyValuePair<TKey, TValue>> Items,
    int NodesVisited,
    bool Truncated);

public sealed class AvlTree<TKey, TValue> where TKey : IComparable<TKey>
{
    private sealed class Node
    {
        public Node(TKey key, TValue value)
        {
            Key = key;
            Value = value;
            Height = 1;
        }

        public TKey Key;
        public TValue Value;
        public int Height;
        public Node? Left;
        public Node? Right;
    }

    private Node? _root;

    public int Count { get; private set; }
    public long Rotations { get; private set; }
    public int Height => HeightOf(_root);

    public bool Insert(TKey key, TValue value)
    {
        var added = false;
        _root = Insert(_root, key, value, ref added);
        if (added) Count++;
        return added;
    }

    public bool TryGet(TKey key, out TValue value)
    {
        var node = _root;
        while (node is not null)
        {
            var compare = key.CompareTo(node.Key);
            if (compare == 0)
            {
                value = node.Value;
                return true;
            }

            node = compare < 0 ? node.Left : node.Right;
        }

        value = default!;
        return false;
    }

    public bool ContainsKey(TKey key) => TryGet(key, out _);

    public bool TryGetMin(out TKey key, out TValue value)
    {
        var node = _root;
        if (node is null)
        {
            key = default!;
            value = default!;
            return false;
        }

        while (node.Left is not null) node = node.Left;
        key = node.Key;
        value = node.Value;
        return true;
    }

    public bool TryGetMax(out TKey key, out TValue value)
    {
        var node = _root;
        if (node is null)
        {
            key = default!;
            value = default!;
            return false;
        }

        while (node.Right is not null) node = node.Right;
        key = node.Key;
        value = node.Value;
        return true;
    }

    public AvlRange<TKey, TValue> Range(TKey low, TKey high, int max, bool descending = false)
    {
        var items = new List<KeyValuePair<TKey, TValue>>();
        var visited = 0;
        var truncated = false;
        Collect(_root, low, high, max, descending, items, ref visited, ref truncated);
        return new AvlRange<TKey, TValue>(items, visited, truncated);
    }

    public bool IsValid() => Validate(_root, out _);

    private static void Collect(
        Node? node,
        TKey low,
        TKey high,
        int max,
        bool descending,
        List<KeyValuePair<TKey, TValue>> items,
        ref int visited,
        ref bool truncated)
    {
        if (node is null || truncated) return;

        visited++;

        var goLow = low.CompareTo(node.Key) < 0;
        var goHigh = high.CompareTo(node.Key) > 0;
        var inRange = low.CompareTo(node.Key) <= 0 && high.CompareTo(node.Key) >= 0;

        var first = descending ? node.Right : node.Left;
        var second = descending ? node.Left : node.Right;
        var firstAllowed = descending ? goHigh : goLow;
        var secondAllowed = descending ? goLow : goHigh;

        if (firstAllowed) Collect(first, low, high, max, descending, items, ref visited, ref truncated);
        if (truncated) return;

        if (inRange)
        {
            if (items.Count >= max)
            {
                truncated = true;
                return;
            }

            items.Add(new KeyValuePair<TKey, TValue>(node.Key, node.Value));
        }

        if (secondAllowed) Collect(second, low, high, max, descending, items, ref visited, ref truncated);
    }

    private Node Insert(Node? node, TKey key, TValue value, ref bool added)
    {
        if (node is null)
        {
            added = true;
            return new Node(key, value);
        }

        var compare = key.CompareTo(node.Key);
        if (compare < 0)
            node.Left = Insert(node.Left, key, value, ref added);
        else if (compare > 0)
            node.Right = Insert(node.Right, key, value, ref added);
        else
        {
            node.Value = value;
            return node;
        }

        return Rebalance(node);
    }

    private Node Rebalance(Node node)
    {
        UpdateHeight(node);
        var balance = HeightOf(node.Left) - HeightOf(node.Right);

        if (balance > 1)
        {
            if (HeightOf(node.Left!.Left) < HeightOf(node.Left.Right))
                node.Left = RotateLeft(node.Left);
            return RotateRight(node);
        }

        if (balance < -1)
        {
            if (HeightOf(node.Right!.Right) < HeightOf(node.Right.Left))
                node.Right = RotateRight(node.Right);
            return RotateLeft(node);
        }

        return node;
    }

    private Node RotateRight(Node y)
    {
        var x = y.Left!;
        y.Left = x.Right;
        x.Right = y;
        UpdateHeight(y);
        UpdateHeight(x);
        Rotations++;
        return x;
    }

    private Node RotateLeft(Node x)
    {
        var y = x.Right!;
        x.Right = y.Left;
        y.Left = x;
        UpdateHeight(x);
        UpdateHeight(y);
        Rotations++;
        return y;
    }

    private static int HeightOf(Node? node) => node?.Height ?? 0;

    private static void UpdateHeight(Node node) =>
        node.Height = 1 + Math.Max(HeightOf(node.Left), HeightOf(node.Right));

    private static bool Validate(Node? node, out int height)
    {
        if (node is null)
        {
            height = 0;
            return true;
        }

        var leftOk = Validate(node.Left, out var left);
        var rightOk = Validate(node.Right, out var right);
        height = 1 + Math.Max(left, right);

        var ordered = (node.Left is null || node.Left.Key.CompareTo(node.Key) < 0)
                      && (node.Right is null || node.Right.Key.CompareTo(node.Key) > 0);

        return leftOk && rightOk && ordered && Math.Abs(left - right) <= 1 && height == node.Height;
    }
}
