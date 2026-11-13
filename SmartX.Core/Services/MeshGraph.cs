namespace SmartX.Core.Services;

public sealed record MeshEdge(string A, string B, double Cost)
{
    public string Key => MeshGraph.LinkKey(A, B);

    public string Other(string vertex) =>
        string.Equals(A, vertex, StringComparison.OrdinalIgnoreCase) ? B : A;
}

public sealed record BfsResult(List<string> Order, Dictionary<string, int> Depth, Dictionary<string, string?> Parent);

public sealed record MstResult(List<MeshEdge> Edges, Dictionary<string, string?> Parent, double TotalCost);

public sealed class MeshGraph
{
    private readonly Dictionary<string, List<MeshEdge>> _adjacency = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<MeshEdge> _edges = new();

    public IReadOnlyCollection<string> Vertices => _adjacency.Keys;
    public IReadOnlyList<MeshEdge> Edges => _edges;

    public static string LinkKey(string a, string b) =>
        string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0
            ? a.ToUpperInvariant() + "|" + b.ToUpperInvariant()
            : b.ToUpperInvariant() + "|" + a.ToUpperInvariant();

    public void AddVertex(string id)
    {
        if (!_adjacency.ContainsKey(id)) _adjacency[id] = new List<MeshEdge>();
    }

    public MeshEdge AddEdge(string a, string b, double cost)
    {
        AddVertex(a);
        AddVertex(b);
        var edge = new MeshEdge(a, b, cost);
        _adjacency[a].Add(edge);
        _adjacency[b].Add(edge);
        _edges.Add(edge);
        return edge;
    }

    public bool Contains(string id) => _adjacency.ContainsKey(id);

    public IReadOnlyList<MeshEdge> EdgesOf(string id) =>
        _adjacency.TryGetValue(id, out var list) ? list : Array.Empty<MeshEdge>();

    public MeshEdge? FindEdge(string a, string b) =>
        EdgesOf(a).FirstOrDefault(e => e.Key == LinkKey(a, b));

    public BfsResult Bfs(string start, Func<string, bool> allowVertex, Func<MeshEdge, bool> allowEdge)
    {
        var order = new List<string>();
        var depth = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var parent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        if (!Contains(start) || !allowVertex(start)) return new BfsResult(order, depth, parent);

        var queue = new Queue<string>();
        queue.Enqueue(start);
        depth[start] = 0;
        parent[start] = null;

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            order.Add(current);

            foreach (var edge in _adjacency[current])
            {
                var next = edge.Other(current);
                if (depth.ContainsKey(next) || !allowVertex(next) || !allowEdge(edge)) continue;

                depth[next] = depth[current] + 1;
                parent[next] = current;
                queue.Enqueue(next);
            }
        }

        return new BfsResult(order, depth, parent);
    }

    public List<string> Dfs(string start, Func<string, bool> allowVertex, Func<MeshEdge, bool> allowEdge)
    {
        var order = new List<string>();
        if (!Contains(start) || !allowVertex(start)) return order;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<string>();
        stack.Push(start);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (!seen.Add(current)) continue;

            order.Add(current);

            foreach (var edge in _adjacency[current])
            {
                var next = edge.Other(current);
                if (!seen.Contains(next) && allowVertex(next) && allowEdge(edge)) stack.Push(next);
            }
        }

        return order;
    }

    public List<List<string>> Components(Func<string, bool> allowVertex, Func<MeshEdge, bool> allowEdge)
    {
        var components = new List<List<string>>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var vertex in _adjacency.Keys)
        {
            if (seen.Contains(vertex) || !allowVertex(vertex)) continue;

            var members = Dfs(vertex, allowVertex, allowEdge);
            foreach (var member in members) seen.Add(member);
            components.Add(members);
        }

        return components;
    }

    public MstResult Prim(string root, Func<string, bool> allowVertex, Func<MeshEdge, bool> allowEdge)
    {
        var parent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var edges = new List<MeshEdge>();
        double total = 0;

        if (!Contains(root) || !allowVertex(root)) return new MstResult(edges, parent, total);

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { root };
        var heap = new PriorityQueue<(string To, string From, MeshEdge Edge), double>();
        parent[root] = null;
        Push(root);

        while (heap.TryDequeue(out var item, out _))
        {
            if (!visited.Add(item.To)) continue;

            parent[item.To] = item.From;
            edges.Add(item.Edge);
            total += item.Edge.Cost;
            Push(item.To);
        }

        return new MstResult(edges, parent, total);

        void Push(string vertex)
        {
            foreach (var edge in _adjacency[vertex])
            {
                var other = edge.Other(vertex);
                if (!visited.Contains(other) && allowVertex(other) && allowEdge(edge))
                    heap.Enqueue((other, vertex, edge), edge.Cost);
            }
        }
    }

    public static List<string> PathToRoot(Dictionary<string, string?> parent, string node)
    {
        var path = new List<string>();
        if (!parent.ContainsKey(node)) return path;

        string? current = node;
        while (current is not null)
        {
            path.Add(current);
            current = parent.TryGetValue(current, out var next) ? next : null;
        }

        return path;
    }
}
