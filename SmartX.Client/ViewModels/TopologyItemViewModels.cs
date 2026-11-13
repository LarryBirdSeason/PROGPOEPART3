using System.Globalization;
using System.Windows.Media;
using SmartX.Core.Models;

namespace SmartX.Client.ViewModels;

public sealed class TopoNodeViewModel : ObservableObject
{
    private TopologyNodeDto _dto;
    private bool _isOnRoute;

    public TopoNodeViewModel(TopologyNodeDto dto)
    {
        _dto = dto;
    }

    public void Update(TopologyNodeDto dto)
    {
        _dto = dto;
        OnPropertyChanged(string.Empty);
    }

    public string Id => _dto.Id;
    public string Name => _dto.Name;
    public NodeRole Role => _dto.Role;
    public double Left => _dto.X - 50;
    public double Top => _dto.Y - Diameter / 2;

    public double Diameter => _dto.Role switch
    {
        NodeRole.Server => 44,
        NodeRole.Gateway => 38,
        _ => 30
    };

    public bool IsOnRoute
    {
        get => _isOnRoute;
        set
        {
            if (!SetField(ref _isOnRoute, value)) return;
            OnPropertyChanged(nameof(StrokeBrush));
            OnPropertyChanged(nameof(StrokeThickness));
        }
    }

    public Brush Fill
    {
        get
        {
            if (_dto.Role == NodeRole.Server) return Palette.Blue;
            if (!_dto.IsUp) return Palette.Red;
            if (!_dto.Reachable) return Palette.Amber;
            return _dto.Role == NodeRole.Gateway ? Palette.Purple : Palette.Green;
        }
    }

    public Brush StrokeBrush => _isOnRoute ? Palette.White : Palette.Slate;
    public double StrokeThickness => _isOnRoute ? 4 : 1.5;

    public string SubLabel
    {
        get
        {
            if (_dto.Role == NodeRole.Server) return "root";
            if (!_dto.IsUp) return "DOWN";
            if (!_dto.Reachable) return "ISOLATED";
            return $"{_dto.Hops} hop{(_dto.Hops == 1 ? string.Empty : "s")}";
        }
    }

    public string ToolTip => $"{_dto.Name}\n{_dto.Id}\nSubnet: {_dto.Subnet}";
}

public sealed class TopoEdgeViewModel : ObservableObject
{
    private static readonly DoubleCollection Dashed = Freeze(new DoubleCollection { 4, 3 });
    private static readonly DoubleCollection Solid = Freeze(new DoubleCollection());

    private TopologyEdgeDto _dto;
    private bool _isOnRoute;
    private bool _showMst = true;

    public TopoEdgeViewModel(TopologyEdgeDto dto, TopologyNodeDto a, TopologyNodeDto b)
    {
        _dto = dto;
        X1 = a.X;
        Y1 = a.Y;
        X2 = b.X;
        Y2 = b.Y;
    }

    public double X1 { get; }
    public double Y1 { get; }
    public double X2 { get; }
    public double Y2 { get; }
    public double MidX => (X1 + X2) / 2 - 6;
    public double MidY => (Y1 + Y2) / 2 - 8;

    public string Key => _dto.Key;
    public string A => _dto.A;
    public string B => _dto.B;
    public string CostText => _dto.Cost.ToString("0.#", CultureInfo.InvariantCulture);

    public void Refresh(TopologyEdgeDto dto, bool isOnRoute, bool showMst)
    {
        _dto = dto;
        _isOnRoute = isOnRoute;
        _showMst = showMst;
        OnPropertyChanged(string.Empty);
    }

    public Brush Stroke
    {
        get
        {
            if (_isOnRoute) return Palette.Amber;
            return _dto.State switch
            {
                LinkState.Cut => Palette.Red,
                LinkState.Down => Palette.Grey,
                LinkState.Mst when _showMst => Palette.Green,
                LinkState.Mst => Palette.Steel,
                _ => Palette.Slate
            };
        }
    }

    public double Thickness
    {
        get
        {
            if (_isOnRoute) return 5;
            return _dto.State switch
            {
                LinkState.Cut => 2,
                LinkState.Down => 1.5,
                LinkState.Mst when _showMst => 3.5,
                LinkState.Mst => 2.5,
                _ => 1.2
            };
        }
    }

    public DoubleCollection Dash =>
        !_isOnRoute && _dto.State is LinkState.Cut or LinkState.Down ? Dashed : Solid;

    private static DoubleCollection Freeze(DoubleCollection collection)
    {
        collection.Freeze();
        return collection;
    }
}

public sealed class LinkOption : ObservableObject
{
    private readonly string _label;
    private LinkState _state;

    public LinkOption(TopologyEdgeDto edge, string nameA, string nameB)
    {
        Key = edge.Key;
        A = edge.A;
        B = edge.B;
        _label = $"{nameA}  -  {nameB}  ({edge.Cost.ToString("0.#", CultureInfo.InvariantCulture)} mW)";
        _state = edge.State;
    }

    public string Key { get; }
    public string A { get; }
    public string B { get; }
    public string Text => _state == LinkState.Cut ? _label + "  [CUT]" : _label;

    public void Update(LinkState state)
    {
        if (_state == state) return;
        _state = state;
        OnPropertyChanged(nameof(Text));
    }
}

public sealed class GatewayOption
{
    public GatewayOption(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }
    public string Name { get; }
}

public sealed class RouteHopItem
{
    public RouteHopItem(RouteHopDto hop, int index, bool isLast)
    {
        Title = $"{index + 1}.  {hop.Name}";
        Mac = hop.Id;
        CostText = isLast ? string.Empty : $"{hop.LinkCost.ToString("0.#", CultureInfo.InvariantCulture)} mW to next hop";
        Brush = hop.IsUp ? Palette.Green : Palette.Red;
    }

    public string Title { get; }
    public string Mac { get; }
    public string CostText { get; }
    public Brush Brush { get; }
}

public sealed class SubnetNodeItem
{
    public SubnetNodeItem(SubnetNodeDto node)
    {
        Name = node.Name;
        Status = node.Status.ToUpperInvariant();
        Hint = node.Hint;
        HasHint = !string.IsNullOrEmpty(node.Hint);
        DepthText = node.Depth >= 0 ? $"depth {node.Depth}" : string.Empty;
        Brush = node.Status switch
        {
            "Reachable" => Palette.Green,
            "Down" => Palette.Red,
            _ => Palette.Amber
        };
    }

    public string Name { get; }
    public string Status { get; }
    public string Hint { get; }
    public bool HasHint { get; }
    public string DepthText { get; }
    public Brush Brush { get; }
}

public sealed class LogRowItem
{
    public LogRowItem(ArchiveRecordDto record, string unit)
    {
        var local = record.Timestamp.ToLocalTime();
        DateText = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        TimeText = local.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        ValueText = $"{record.Value.ToString("0.##", CultureInfo.InvariantCulture)} {unit}".Trim();
        LevelText = record.Severity switch
        {
            Severity.Critical => "CRITICAL",
            Severity.Warning => "WARNING",
            _ => "OK"
        };
        Brush = record.Severity switch
        {
            Severity.Critical => Palette.Red,
            Severity.Warning => Palette.Amber,
            _ => Palette.Green
        };
    }

    public string DateText { get; }
    public string TimeText { get; }
    public string ValueText { get; }
    public string LevelText { get; }
    public Brush Brush { get; }
}
