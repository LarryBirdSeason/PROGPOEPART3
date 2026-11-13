using SmartX.Core.Models;

namespace SmartX.Core.Demo;

public sealed record TopologyVertexSpec(string Id, string Name, NodeRole Role, string Subnet, double X, double Y);

public sealed record TopologyLinkSpec(string A, string B, double Cost);

public static class DemoTopology
{
    public const string Server = "24:6F:28:A1:00:F0";
    public const string Gateway1 = "24:6F:28:A1:00:F1";
    public const string Gateway2 = "24:6F:28:A1:00:F2";

    public const string SubnetA = "Greenhouse";
    public const string SubnetB = "Plant Room";

    public static readonly IReadOnlyList<TopologyVertexSpec> Vertices = new List<TopologyVertexSpec>
    {
        new(Server, "Central Server", NodeRole.Server, "Core", 380, 36),
        new(Gateway1, "Gateway 1", NodeRole.Gateway, SubnetA, 190, 120),
        new(Gateway2, "Gateway 2", NodeRole.Gateway, SubnetB, 570, 120),

        new(DemoRoster.MoistureA, "Moisture Sensor A", NodeRole.Node, SubnetA, 60, 200),
        new(DemoRoster.TempA, "Temp Sensor A", NodeRole.Node, SubnetA, 205, 215),
        new(DemoRoster.Meter1, "Smart Meter 1", NodeRole.Node, SubnetA, 330, 235),
        new(DemoRoster.MoistureB, "Moisture Sensor B", NodeRole.Node, SubnetA, 110, 300),
        new(DemoRoster.PumpA, "Pump A", NodeRole.Node, SubnetA, 250, 330),
        new(DemoRoster.TempB, "Temp Sensor B", NodeRole.Node, SubnetA, 55, 390),
        new(DemoRoster.Valve1, "Valve 1", NodeRole.Node, SubnetA, 190, 405),

        new(DemoRoster.Meter2, "Smart Meter 2", NodeRole.Node, SubnetB, 470, 205),
        new(DemoRoster.Relay1, "Relay 1", NodeRole.Node, SubnetB, 600, 215),
        new(DemoRoster.PumpB, "Pump B", NodeRole.Node, SubnetB, 705, 190),
        new(DemoRoster.MoistureC, "Moisture Sensor C", NodeRole.Node, SubnetB, 420, 320),
        new(DemoRoster.Meter3, "Smart Meter 3", NodeRole.Node, SubnetB, 545, 305),
        new(DemoRoster.Valve2, "Valve 2", NodeRole.Node, SubnetB, 660, 320),
        new(DemoRoster.Valve3, "Valve 3", NodeRole.Node, SubnetB, 700, 405),
    };

    public static readonly IReadOnlyList<TopologyLinkSpec> Links = new List<TopologyLinkSpec>
    {
        new(Server, Gateway1, 2),
        new(Server, Gateway2, 3),

        new(Gateway1, DemoRoster.MoistureA, 4),
        new(Gateway1, DemoRoster.TempA, 5),
        new(Gateway1, DemoRoster.Meter1, 7),
        new(DemoRoster.MoistureA, DemoRoster.TempA, 2),
        new(DemoRoster.MoistureA, DemoRoster.MoistureB, 3),
        new(DemoRoster.TempA, DemoRoster.Meter1, 4),
        new(DemoRoster.MoistureB, DemoRoster.PumpA, 4),
        new(DemoRoster.MoistureB, DemoRoster.TempB, 3),
        new(DemoRoster.TempB, DemoRoster.Valve1, 2),
        new(DemoRoster.Valve1, DemoRoster.PumpA, 4),
        new(DemoRoster.PumpA, DemoRoster.Meter1, 6),

        new(Gateway2, DemoRoster.Meter2, 4),
        new(Gateway2, DemoRoster.Relay1, 5),
        new(Gateway2, DemoRoster.PumpB, 6),
        new(DemoRoster.Meter2, DemoRoster.Relay1, 3),
        new(DemoRoster.Meter2, DemoRoster.Meter3, 3),
        new(DemoRoster.Meter2, DemoRoster.MoistureC, 5),
        new(DemoRoster.Relay1, DemoRoster.PumpB, 2),
        new(DemoRoster.Meter3, DemoRoster.MoistureC, 4),
        new(DemoRoster.Meter3, DemoRoster.Valve2, 3),
        new(DemoRoster.PumpB, DemoRoster.Valve2, 5),
        new(DemoRoster.Valve2, DemoRoster.Valve3, 2),

        new(DemoRoster.Meter1, DemoRoster.MoistureC, 8),
        new(DemoRoster.PumpA, DemoRoster.MoistureC, 9),
    };
}
