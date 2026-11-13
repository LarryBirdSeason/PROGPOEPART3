namespace SmartX.Core.Models;

public enum NodeRole
{
    Server,
    Gateway,
    Node
}

public enum LinkState
{
    Mesh,
    Mst,
    Cut,
    Down
}

public sealed record TopologyNodeDto(
    string Id,
    string Name,
    NodeRole Role,
    string Subnet,
    double X,
    double Y,
    bool IsUp,
    bool Reachable,
    int Hops,
    string? Parent);

public sealed record TopologyEdgeDto(
    string Key,
    string A,
    string B,
    double Cost,
    LinkState State,
    bool InMst);

public sealed record TopologyDto(
    List<TopologyNodeDto> Nodes,
    List<TopologyEdgeDto> Edges,
    double MstCost,
    double NominalMstCost,
    int UpCount,
    int DownCount,
    int ReachableCount,
    int Partitions,
    List<string> DownNames);

public sealed record RouteHopDto(string Id, string Name, bool IsUp, double LinkCost);

public sealed record RouteDto(
    string Query,
    bool Found,
    string Mac,
    string Name,
    bool Reachable,
    bool Rerouted,
    double TotalCost,
    int Hops,
    List<RouteHopDto> Path,
    List<RouteHopDto> NominalPath,
    string Message,
    List<string> Hints);

public sealed record SubnetNodeDto(string Id, string Name, int Depth, string Status, string Hint);

public sealed record SubnetDto(
    string GatewayId,
    string GatewayName,
    int Members,
    int ReachableCount,
    int DownCount,
    int IsolatedCount,
    List<SubnetNodeDto> Nodes,
    List<string> TraversalOrder);

public sealed record LinkToggleRequest(string A, string B);

public sealed record ArchiveRecordDto(DateTimeOffset Timestamp, double Value, Severity Severity);

public sealed record ArchiveQueryDto(
    string Mac,
    string Name,
    string Kind,
    string Unit,
    DateTimeOffset From,
    DateTimeOffset To,
    int Returned,
    bool Truncated,
    int NodesVisited,
    int TreeCount,
    int TreeHeight,
    long TreeRotations,
    double ElapsedMicroseconds,
    double Minimum,
    double Maximum,
    double Average,
    List<ArchiveRecordDto> Records);

public sealed record ArchiveDeviceStatDto(
    string Mac,
    string Name,
    int Count,
    int Height,
    long Rotations,
    DateTimeOffset? Oldest,
    DateTimeOffset? Newest);

public sealed record ArchiveStatsDto(int TotalRecords, List<ArchiveDeviceStatDto> Devices);

public sealed record BackfillPointDto(DateTimeOffset Timestamp, double Value);
