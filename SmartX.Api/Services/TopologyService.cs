using SmartX.Core.Demo;
using SmartX.Core.Models;
using SmartX.Core.Services;

namespace SmartX.Api.Services;

public sealed class TopologyService
{
    private sealed record View(
        HashSet<string> Up,
        HashSet<string> Cut,
        MstResult Live,
        MstResult Nominal);

    private readonly DeviceRegistry _registry;
    private readonly MeshGraph _graph = new();
    private readonly Dictionary<string, TopologyVertexSpec> _specs = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _cut = new();
    private readonly object _gate = new();

    public TopologyService(DeviceRegistry registry)
    {
        _registry = registry;

        foreach (var vertex in DemoTopology.Vertices)
        {
            _graph.AddVertex(vertex.Id);
            _specs[vertex.Id] = vertex;
        }

        foreach (var link in DemoTopology.Links)
            _graph.AddEdge(link.A, link.B, link.Cost);
    }

    public string? Resolve(string? query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;

        var text = query.Trim();
        if (_specs.ContainsKey(text)) return _specs[text].Id;

        var exact = _specs.Values.FirstOrDefault(s => s.Name.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact.Id;

        var compact = Compact(text);
        var partial = _specs.Values.FirstOrDefault(s => Compact(s.Name).Contains(compact) || Compact(s.Id).Contains(compact));
        return partial?.Id;
    }

    public (bool Cut, bool Found) ToggleLink(string a, string b)
    {
        var edge = _graph.FindEdge(a, b);
        if (edge is null) return (false, false);

        lock (_gate)
        {
            if (!_cut.Add(edge.Key)) _cut.Remove(edge.Key);
            return (_cut.Contains(edge.Key), true);
        }
    }

    public string NameOf(string id) => _specs.TryGetValue(id, out var spec) ? spec.Name : id;

    public TopologyDto Snapshot()
    {
        var view = Capture();
        var liveEdges = view.Live.Edges.Select(e => e.Key).ToHashSet();

        var nodes = new List<TopologyNodeDto>();
        foreach (var spec in DemoTopology.Vertices)
        {
            var reachable = view.Live.Parent.ContainsKey(spec.Id);
            var hops = reachable ? MeshGraph.PathToRoot(view.Live.Parent, spec.Id).Count - 1 : -1;
            view.Live.Parent.TryGetValue(spec.Id, out var parent);

            nodes.Add(new TopologyNodeDto(
                spec.Id, spec.Name, spec.Role, spec.Subnet, spec.X, spec.Y,
                view.Up.Contains(spec.Id), reachable, hops, parent));
        }

        var edges = new List<TopologyEdgeDto>();
        foreach (var edge in _graph.Edges)
        {
            var inMst = liveEdges.Contains(edge.Key);
            var state = view.Cut.Contains(edge.Key)
                ? LinkState.Cut
                : !view.Up.Contains(edge.A) || !view.Up.Contains(edge.B)
                    ? LinkState.Down
                    : inMst ? LinkState.Mst : LinkState.Mesh;

            edges.Add(new TopologyEdgeDto(edge.Key, edge.A, edge.B, edge.Cost, state, inMst));
        }

        var partitions = _graph.Components(view.Up.Contains, e => !view.Cut.Contains(e.Key)).Count;
        var downNames = DemoTopology.Vertices
            .Where(v => !view.Up.Contains(v.Id))
            .Select(v => v.Name)
            .ToList();

        return new TopologyDto(
            nodes,
            edges,
            view.Live.TotalCost,
            view.Nominal.TotalCost,
            view.Up.Count,
            downNames.Count,
            view.Live.Parent.Count,
            partitions,
            downNames);
    }

    public RouteDto Route(string query)
    {
        var id = Resolve(query);
        if (id is null)
        {
            return new RouteDto(query, false, string.Empty, string.Empty, false, false, 0, 0,
                new List<RouteHopDto>(), new List<RouteHopDto>(),
                $"No device matches '{query}'. Try a MAC address or part of a name.", new List<string>());
        }

        var view = Capture();
        var name = NameOf(id);
        var reachable = view.Live.Parent.ContainsKey(id);

        var livePath = MeshGraph.PathToRoot(view.Live.Parent, id);
        var nominalPath = MeshGraph.PathToRoot(view.Nominal.Parent, id);
        var hints = new List<string>();

        if (reachable)
        {
            var total = PathCost(livePath);
            var rerouted = !livePath.SequenceEqual(nominalPath, StringComparer.OrdinalIgnoreCase);
            var message = $"{name} reaches the server in {livePath.Count - 1} hops at {total:0.#} mW.";

            if (rerouted)
            {
                var avoided = nominalPath
                    .Where(v => !view.Up.Contains(v))
                    .Select(NameOf)
                    .ToList();

                var cutLinks = NominalLinks(nominalPath).Where(view.Cut.Contains).Count();

                if (avoided.Count > 0)
                    message += $" Rerouted around {string.Join(", ", avoided)}.";
                else if (cutLinks > 0)
                    message += $" Rerouted around {cutLinks} cut link(s).";
                else
                    message += " Rerouted because the best path changed.";

                hints.Add($"Normal route: {string.Join(" > ", nominalPath.Select(NameOf))}");
            }

            return new RouteDto(query, true, id, name, true, rerouted, total, livePath.Count - 1,
                Hops(livePath, view), Hops(nominalPath, view), message, hints);
        }

        var isUp = view.Up.Contains(id);
        string reason;

        if (!isUp)
        {
            reason = $"{name} is down and cannot be reached.";
            foreach (var edge in _graph.EdgesOf(id))
            {
                var neighbour = edge.Other(id);
                if (!view.Live.Parent.ContainsKey(neighbour)) continue;

                var cost = PathCost(MeshGraph.PathToRoot(view.Live.Parent, neighbour));
                hints.Add($"Hop via {NameOf(neighbour)}: online, {edge.Cost:0.#} mW away, {cost:0.#} mW from the server.");
            }

            if (hints.Count == 0) hints.Add("No live neighbour. This part of the mesh is cut off.");
        }
        else
        {
            reason = $"{name} is online but has no live route to the server.";
            foreach (var edge in _graph.EdgesOf(id))
            {
                var neighbour = edge.Other(id);
                if (view.Cut.Contains(edge.Key))
                    hints.Add($"Link to {NameOf(neighbour)} is cut. Restoring it re-joins the mesh.");
                else if (!view.Up.Contains(neighbour))
                    hints.Add($"Neighbour {NameOf(neighbour)} is down.");
            }
        }

        return new RouteDto(query, true, id, name, false, false, 0, 0,
            new List<RouteHopDto>(), Hops(nominalPath, view), reason, hints);
    }

    public SubnetDto? Subnet(string gatewayQuery)
    {
        var id = Resolve(gatewayQuery);
        if (id is null || _specs[id].Role != NodeRole.Gateway) return null;

        var view = Capture();
        var gateway = _specs[id];
        var members = DemoTopology.Vertices
            .Where(v => v.Role == NodeRole.Node && v.Subnet == gateway.Subnet)
            .ToList();
        var memberIds = members.Select(m => m.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);

        var bfs = _graph.Bfs(
            id,
            v => view.Up.Contains(v) && (v.Equals(id, StringComparison.OrdinalIgnoreCase) || memberIds.Contains(v)),
            e => !view.Cut.Contains(e.Key));

        var nodes = new List<SubnetNodeDto>();
        foreach (var member in members)
        {
            if (bfs.Depth.TryGetValue(member.Id, out var depth))
            {
                nodes.Add(new SubnetNodeDto(member.Id, member.Name, depth, "Reachable", string.Empty));
                continue;
            }

            if (!view.Up.Contains(member.Id))
            {
                var alternatives = _graph.EdgesOf(member.Id)
                    .Select(e => e.Other(member.Id))
                    .Where(n => view.Live.Parent.ContainsKey(n))
                    .Select(NameOf)
                    .ToList();

                var hint = alternatives.Count > 0
                    ? $"Hop via {string.Join(" or ", alternatives)}"
                    : "No live neighbour to hop through";

                nodes.Add(new SubnetNodeDto(member.Id, member.Name, -1, "Down", hint));
                continue;
            }

            string isolatedHint;
            if (view.Live.Parent.TryGetValue(member.Id, out var next) && next is not null)
                isolatedHint = $"Re-homed via {NameOf(next)} outside this subnet";
            else
                isolatedHint = "No live path to any gateway";

            nodes.Add(new SubnetNodeDto(member.Id, member.Name, -1, "Isolated", isolatedHint));
        }

        nodes = nodes
            .OrderBy(n => n.Status == "Reachable" ? 0 : 1)
            .ThenBy(n => n.Depth)
            .ThenBy(n => n.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SubnetDto(
            id,
            gateway.Name,
            members.Count,
            nodes.Count(n => n.Status == "Reachable"),
            nodes.Count(n => n.Status == "Down"),
            nodes.Count(n => n.Status == "Isolated"),
            nodes,
            bfs.Order.Select(NameOf).ToList());
    }

    private View Capture()
    {
        var devices = _registry.Snapshot().ToDictionary(d => d.Mac, StringComparer.OrdinalIgnoreCase);
        var up = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var spec in DemoTopology.Vertices)
        {
            if (spec.Role != NodeRole.Node)
                up.Add(spec.Id);
            else if (devices.TryGetValue(spec.Id, out var device) && device.Connection != ConnectionState.Offline)
                up.Add(spec.Id);
        }

        HashSet<string> cut;
        lock (_gate)
        {
            cut = new HashSet<string>(_cut);
        }

        var nominal = _graph.Prim(DemoTopology.Server, _ => true, _ => true);
        var live = _graph.Prim(DemoTopology.Server, up.Contains, e => !cut.Contains(e.Key));
        return new View(up, cut, live, nominal);
    }

    private double PathCost(List<string> path)
    {
        double total = 0;
        for (var i = 0; i < path.Count - 1; i++)
            total += _graph.FindEdge(path[i], path[i + 1])?.Cost ?? 0;
        return total;
    }

    private IEnumerable<string> NominalLinks(List<string> path)
    {
        for (var i = 0; i < path.Count - 1; i++)
            yield return MeshGraph.LinkKey(path[i], path[i + 1]);
    }

    private List<RouteHopDto> Hops(List<string> path, View view)
    {
        var hops = new List<RouteHopDto>();
        for (var i = 0; i < path.Count; i++)
        {
            var cost = i < path.Count - 1 ? _graph.FindEdge(path[i], path[i + 1])?.Cost ?? 0 : 0;
            hops.Add(new RouteHopDto(path[i], NameOf(path[i]), view.Up.Contains(path[i]), cost));
        }

        return hops;
    }

    private static string Compact(string text) =>
        new string(text.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
