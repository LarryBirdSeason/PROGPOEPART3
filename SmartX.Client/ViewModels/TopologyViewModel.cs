using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using SmartX.Client.Services;
using SmartX.Core.Models;
using SmartX.Core.Services;

namespace SmartX.Client.ViewModels;

public sealed class TopologyViewModel : ObservableObject, INavigable
{
    private static readonly string[] TimeFormats = { "yyyy-MM-dd HH:mm", "HH:mm" };

    private readonly ApiClient _api;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, TopoNodeViewModel> _nodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, TopoEdgeViewModel> _edges = new();
    private readonly Dictionary<string, LinkOption> _linkOptions = new();
    private TopologyDto? _topology;
    private HashSet<string> _routeNodes = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _routeLinks = new();
    private string? _trackedMac;
    private bool _refreshing;
    private bool _active;

    private string _searchText = string.Empty;
    private bool _showMst = true;
    private string _routeTitle = "No route tracked yet";
    private string _routeMessage = "Enter a MAC address or device name, or click a node on the map.";
    private Brush _routeBrush = Palette.Grey;
    private GatewayOption? _selectedGateway;
    private string _subnetSummary = string.Empty;
    private string _traversalText = string.Empty;
    private LinkOption? _selectedLink;
    private string _linkStatus = string.Empty;
    private string _mstText = "-";
    private string _networkText = "-";
    private string _downText = string.Empty;
    private string _trackedName = "No node selected";
    private string _fromText;
    private string _toText;
    private bool _newestFirst = true;
    private string _logSummary = "Track a node to load its telemetry history.";
    private string _treeText = string.Empty;
    private bool _apiOnline = true;
    private string _connectionMessage = string.Empty;

    public TopologyViewModel(ApiClient api)
    {
        _api = api;

        var now = DateTime.Now;
        _toText = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        _fromText = now.AddHours(-2).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += async (_, _) => await RefreshAsync();

        TrackCommand = new RelayCommand(async _ => await TrackAsync(_searchText));
        SelectNodeCommand = new RelayCommand(async p => await SelectNodeAsync(p as TopoNodeViewModel));
        ToggleLinkCommand = new RelayCommand(async _ => await ToggleLinkAsync(), _ => _selectedLink is not null);
        QueryLogCommand = new RelayCommand(async _ => await QueryLogAsync());
        QuickRangeCommand = new RelayCommand(async p => await QuickRangeAsync(p as string));
    }

    public ObservableCollection<TopoNodeViewModel> Nodes { get; } = new();
    public ObservableCollection<TopoEdgeViewModel> Edges { get; } = new();
    public ObservableCollection<LinkOption> LinkOptions { get; } = new();
    public ObservableCollection<GatewayOption> GatewayOptions { get; } = new();
    public ObservableCollection<RouteHopItem> RoutePath { get; } = new();
    public ObservableCollection<string> Hints { get; } = new();
    public ObservableCollection<SubnetNodeItem> SubnetNodes { get; } = new();
    public ObservableCollection<LogRowItem> LogRows { get; } = new();

    public ICommand TrackCommand { get; }
    public ICommand SelectNodeCommand { get; }
    public ICommand ToggleLinkCommand { get; }
    public ICommand QueryLogCommand { get; }
    public ICommand QuickRangeCommand { get; }

    public double MapWidth => 760;
    public double MapHeight => 440;

    public string SearchText { get => _searchText; set => SetField(ref _searchText, value); }

    public bool ShowMst
    {
        get => _showMst;
        set
        {
            if (SetField(ref _showMst, value)) ApplyHighlights();
        }
    }

    public string RouteTitle { get => _routeTitle; private set => SetField(ref _routeTitle, value); }
    public string RouteMessage { get => _routeMessage; private set => SetField(ref _routeMessage, value); }
    public Brush RouteBrush { get => _routeBrush; private set => SetField(ref _routeBrush, value); }

    public GatewayOption? SelectedGateway
    {
        get => _selectedGateway;
        set
        {
            if (!SetField(ref _selectedGateway, value)) return;
            _ = RefreshSubnetAsync();
        }
    }

    public string SubnetSummary { get => _subnetSummary; private set => SetField(ref _subnetSummary, value); }
    public string TraversalText { get => _traversalText; private set => SetField(ref _traversalText, value); }

    public LinkOption? SelectedLink { get => _selectedLink; set => SetField(ref _selectedLink, value); }
    public string LinkStatus { get => _linkStatus; private set => SetField(ref _linkStatus, value); }

    public string MstText { get => _mstText; private set => SetField(ref _mstText, value); }
    public string NetworkText { get => _networkText; private set => SetField(ref _networkText, value); }
    public string DownText { get => _downText; private set => SetField(ref _downText, value); }

    public string TrackedName { get => _trackedName; private set => SetField(ref _trackedName, value); }
    public string FromText { get => _fromText; set => SetField(ref _fromText, value); }
    public string ToText { get => _toText; set => SetField(ref _toText, value); }
    public bool NewestFirst { get => _newestFirst; set => SetField(ref _newestFirst, value); }
    public string LogSummary { get => _logSummary; private set => SetField(ref _logSummary, value); }
    public string TreeText { get => _treeText; private set => SetField(ref _treeText, value); }

    public bool ApiOffline => !_apiOnline;
    public string ConnectionMessage { get => _connectionMessage; private set => SetField(ref _connectionMessage, value); }

    private bool ApiOnline
    {
        get => _apiOnline;
        set
        {
            if (SetField(ref _apiOnline, value)) OnPropertyChanged(nameof(ApiOffline));
        }
    }

    public async Task ActivateAsync()
    {
        _active = true;
        _timer.Start();
        await RefreshAsync();
    }

    public void Deactivate()
    {
        _active = false;
        _timer.Stop();
    }

    private async Task RefreshAsync()
    {
        if (_refreshing || !_active) return;
        _refreshing = true;

        try
        {
            var topologyTask = _api.GetTopologyAsync();
            var routeTask = _trackedMac is null ? null : _api.GetRouteAsync(_trackedMac);
            var subnetTask = _selectedGateway is null ? null : _api.GetSubnetAsync(_selectedGateway.Id);

            _topology = await topologyTask;
            SyncGraph(_topology);

            if (routeTask is not null) ApplyRoute(await routeTask, false);
            else ApplyHighlights();

            if (subnetTask is not null) ApplySubnet(await subnetTask);

            ApiOnline = true;
            ConnectionMessage = string.Empty;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or System.Text.Json.JsonException)
        {
            ApiOnline = false;
            ConnectionMessage = "API unreachable at http://localhost:5080. Showing the last known map; retrying.";
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void SyncGraph(TopologyDto dto)
    {
        var byId = dto.Nodes.ToDictionary(n => n.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var node in dto.Nodes)
        {
            if (_nodes.TryGetValue(node.Id, out var existing))
            {
                existing.Update(node);
                continue;
            }

            var item = new TopoNodeViewModel(node);
            _nodes[node.Id] = item;
            Nodes.Add(item);
        }

        foreach (var edge in dto.Edges)
        {
            if (!_edges.ContainsKey(edge.Key))
            {
                var item = new TopoEdgeViewModel(edge, byId[edge.A], byId[edge.B]);
                _edges[edge.Key] = item;
                Edges.Add(item);
            }

            if (_linkOptions.TryGetValue(edge.Key, out var option))
            {
                option.Update(edge.State);
            }
            else
            {
                option = new LinkOption(edge, byId[edge.A].Name, byId[edge.B].Name);
                option.Update(edge.State);
                _linkOptions[edge.Key] = option;
                LinkOptions.Add(option);
            }
        }

        if (GatewayOptions.Count == 0)
        {
            foreach (var gateway in dto.Nodes.Where(n => n.Role == NodeRole.Gateway))
                GatewayOptions.Add(new GatewayOption(gateway.Id, gateway.Name));

            if (GatewayOptions.Count > 0) SelectedGateway = GatewayOptions[0];
        }

        MstText = $"Live MST cost {dto.MstCost:0.#} mW  (full mesh {dto.NominalMstCost:0.#} mW)";
        NetworkText = $"{dto.ReachableCount} of {dto.Nodes.Count} nodes reach the server  ·  {dto.DownCount} down  ·  {dto.Partitions} partition{(dto.Partitions == 1 ? string.Empty : "s")}";
        DownText = dto.DownNames.Count == 0 ? "All nodes online" : "Down: " + string.Join(", ", dto.DownNames);
    }

    private void ApplyHighlights()
    {
        foreach (var node in Nodes) node.IsOnRoute = _routeNodes.Contains(node.Id);

        if (_topology is null) return;

        foreach (var edge in _topology.Edges)
        {
            if (_edges.TryGetValue(edge.Key, out var item))
                item.Refresh(edge, _routeLinks.Contains(edge.Key), _showMst);
        }
    }

    private async Task TrackAsync(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return;

        try
        {
            var route = await _api.GetRouteAsync(query.Trim());
            ApplyRoute(route, true);
            ApiOnline = true;

            if (route.Found) await QueryLogAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or System.Text.Json.JsonException)
        {
            ApiOnline = false;
            ConnectionMessage = "API unreachable at http://localhost:5080. Route not loaded.";
        }
    }

    private async Task SelectNodeAsync(TopoNodeViewModel? node)
    {
        if (node is null) return;

        SearchText = node.Id;
        await TrackAsync(node.Id);
    }

    private void ApplyRoute(RouteDto route, bool newTrack)
    {
        if (!route.Found)
        {
            _trackedMac = null;
            _routeNodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _routeLinks = new HashSet<string>();
            RoutePath.Clear();
            Hints.Clear();
            RouteTitle = "No match";
            RouteMessage = route.Message;
            RouteBrush = Palette.Red;
            ApplyHighlights();
            return;
        }

        _trackedMac = route.Mac;
        TrackedName = $"{route.Name}  ({route.Mac})";
        RouteTitle = $"{route.Name}  ·  {route.Mac}";
        RouteMessage = route.Message;
        RouteBrush = !route.Reachable ? Palette.Red : route.Rerouted ? Palette.Amber : Palette.Green;

        var shown = route.Reachable ? route.Path : route.NominalPath;
        RoutePath.Clear();
        for (var i = 0; i < shown.Count; i++)
            RoutePath.Add(new RouteHopItem(shown[i], i, i == shown.Count - 1));

        Hints.Clear();
        foreach (var hint in route.Hints) Hints.Add(hint);

        _routeNodes = shown.Select(h => h.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _routeLinks = new HashSet<string>();
        for (var i = 0; i < shown.Count - 1; i++)
            _routeLinks.Add(MeshGraph.LinkKey(shown[i].Id, shown[i + 1].Id));

        ApplyHighlights();
    }

    private async Task RefreshSubnetAsync()
    {
        if (_selectedGateway is null) return;

        try
        {
            ApplySubnet(await _api.GetSubnetAsync(_selectedGateway.Id));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or System.Text.Json.JsonException)
        {
            SubnetSummary = "Subnet not loaded: API unreachable.";
        }
    }

    private void ApplySubnet(SubnetDto subnet)
    {
        SubnetSummary = $"{subnet.ReachableCount} reachable  ·  {subnet.DownCount} down  ·  {subnet.IsolatedCount} isolated  (of {subnet.Members})";
        TraversalText = "BFS order: " + string.Join(" > ", subnet.TraversalOrder);

        SubnetNodes.Clear();
        foreach (var node in subnet.Nodes) SubnetNodes.Add(new SubnetNodeItem(node));
    }

    private async Task ToggleLinkAsync()
    {
        var link = _selectedLink;
        if (link is null) return;

        try
        {
            var result = await _api.ToggleLinkAsync(link.A, link.B);
            LinkStatus = result.Message;
            ApiOnline = true;
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or System.Text.Json.JsonException)
        {
            ApiOnline = false;
            LinkStatus = "Link not changed: API unreachable.";
        }
    }

    private async Task QuickRangeAsync(string? minutes)
    {
        if (!int.TryParse(minutes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var span)) return;

        var now = DateTime.Now;
        ToText = now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        FromText = now.AddMinutes(-span).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        await QueryLogAsync();
    }

    private async Task QueryLogAsync()
    {
        if (_trackedMac is null)
        {
            LogSummary = "Track a node first: enter its MAC address or name, or click it on the map.";
            return;
        }

        if (!TryParseTime(_fromText, out var from) || !TryParseTime(_toText, out var to))
        {
            LogSummary = "Use the time format yyyy-MM-dd HH:mm, or just HH:mm for today.";
            return;
        }

        try
        {
            var result = await _api.QueryArchiveAsync(_trackedMac, from, to, 500, _newestFirst);

            LogRows.Clear();
            foreach (var record in result.Records) LogRows.Add(new LogRowItem(record, result.Unit));

            var local = (DateTimeOffset value) => value.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);
            var more = result.Truncated ? "+" : string.Empty;

            LogSummary = result.Returned == 0
                ? $"No readings for {result.Name} between {local(result.From)} and {local(result.To)}."
                : $"{result.Returned}{more} readings for {result.Name}, {local(result.From)} to {local(result.To)}  ·  "
                  + $"min {result.Minimum:0.##}  max {result.Maximum:0.##}  avg {result.Average:0.##} {result.Unit}";

            TreeText = $"AVL tree: {result.TreeCount} records, height {result.TreeHeight}, {result.TreeRotations} rotations  ·  "
                       + $"range query touched {result.NodesVisited} nodes in {result.ElapsedMicroseconds:0} µs";

            ApiOnline = true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException
                                       or System.Text.Json.JsonException)
        {
            ApiOnline = false;
            LogSummary = "History not loaded: API unreachable.";
        }
    }

    private static bool TryParseTime(string text, out DateTimeOffset value)
    {
        if (DateTime.TryParseExact(text.Trim(), TimeFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            value = new DateTimeOffset(DateTime.SpecifyKind(parsed, DateTimeKind.Local));
            return true;
        }

        value = default;
        return false;
    }
}
