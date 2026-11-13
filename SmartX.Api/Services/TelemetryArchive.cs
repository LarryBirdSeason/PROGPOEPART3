using System.Diagnostics;
using SmartX.Core.Models;
using SmartX.Core.Services;

namespace SmartX.Api.Services;

public sealed record ArchiveSlice(
    List<(DateTimeOffset At, double Value)> Items,
    int NodesVisited,
    bool Truncated,
    int TreeCount,
    int TreeHeight,
    long TreeRotations,
    double ElapsedMicroseconds);

public sealed class TelemetryArchive
{
    private readonly Dictionary<string, AvlTree<long, double>> _trees = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public void Add(string mac, DateTimeOffset at, double value)
    {
        lock (_gate)
        {
            if (!_trees.TryGetValue(mac, out var tree))
            {
                tree = new AvlTree<long, double>();
                _trees[mac] = tree;
            }

            var key = at.UtcTicks;
            while (tree.ContainsKey(key)) key++;
            tree.Insert(key, value);
        }
    }

    public ArchiveSlice Query(string mac, DateTimeOffset from, DateTimeOffset to, int max, bool newestFirst)
    {
        lock (_gate)
        {
            if (!_trees.TryGetValue(mac, out var tree))
                return new ArchiveSlice(new List<(DateTimeOffset, double)>(), 0, false, 0, 0, 0, 0);

            var watch = Stopwatch.StartNew();
            var range = tree.Range(from.UtcTicks, to.UtcTicks, max, newestFirst);
            watch.Stop();

            var items = range.Items
                .Select(pair => (new DateTimeOffset(pair.Key, TimeSpan.Zero), pair.Value))
                .ToList();

            var micro = watch.ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency;
            return new ArchiveSlice(items, range.NodesVisited, range.Truncated, tree.Count, tree.Height, tree.Rotations, micro);
        }
    }

    public ArchiveStatsDto Stats(Func<string, string> nameOf)
    {
        lock (_gate)
        {
            var devices = new List<ArchiveDeviceStatDto>();
            var total = 0;

            foreach (var (mac, tree) in _trees.OrderBy(pair => nameOf(pair.Key), StringComparer.OrdinalIgnoreCase))
            {
                DateTimeOffset? oldest = tree.TryGetMin(out var min, out _) ? new DateTimeOffset(min, TimeSpan.Zero) : null;
                DateTimeOffset? newest = tree.TryGetMax(out var max, out _) ? new DateTimeOffset(max, TimeSpan.Zero) : null;
                devices.Add(new ArchiveDeviceStatDto(mac, nameOf(mac), tree.Count, tree.Height, tree.Rotations, oldest, newest));
                total += tree.Count;
            }

            return new ArchiveStatsDto(total, devices);
        }
    }
}
