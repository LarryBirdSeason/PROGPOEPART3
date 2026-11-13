using System.Net.Http.Json;
using SmartX.Core.Demo;
using SmartX.Core.Models;

namespace SmartX.Simulator;

internal static class Backfill
{
    private const int Minutes = 240;

    public static async Task RunAsync(HttpClient http, string[] args, CancellationToken ct)
    {
        if (args.Contains("--no-backfill")) return;

        try
        {
            var stats = await http.GetFromJsonAsync<ArchiveStatsDto>("/api/archive/stats", ApiJson.Options, ct);
            if (stats is { TotalRecords: > 500 })
            {
                Console.WriteLine("Archive already holds history. Skipping backfill.");
                return;
            }
        }
        catch (HttpRequestException)
        {
            return;
        }

        var end = DateTimeOffset.UtcNow.AddSeconds(-30);
        var sent = 0;

        foreach (var device in DemoRoster.Devices.Where(d => d.Kind is "Moisture" or "Temperature" or "Power"))
        {
            var points = Build(device, end);
            var url = $"/api/archive/{Uri.EscapeDataString(device.Mac)}/backfill";
            using var response = await http.PostAsJsonAsync(url, points, ApiJson.Options, ct);
            response.EnsureSuccessStatusCode();
            sent += points.Count;
        }

        Console.WriteLine($"Backfilled {sent} historical readings from device logs.");
    }

    private static List<BackfillPointDto> Build(DemoDevice device, DateTimeOffset end)
    {
        var random = new Random(device.Mac.GetHashCode());
        var points = new List<BackfillPointDto>(Minutes);

        for (var i = Minutes; i >= 1; i--)
        {
            var at = end.AddMinutes(-i);
            var phase = (Minutes - i) / 40.0;
            double value;

            switch (device.Kind)
            {
                case "Moisture":
                    value = 52 + 6 * Math.Sin(phase) + Noise(random, 1.2);
                    if (device.Mac == DemoRoster.MoistureB && i is >= 150 and <= 160)
                        value = 17 + Noise(random, 1.5);
                    break;
                case "Temperature":
                    value = 23 + 3 * Math.Sin(phase / 1.5) + Noise(random, 0.4);
                    if (device.Mac == DemoRoster.TempA && i is >= 90 and <= 96)
                        value = 41 + Noise(random, 0.8);
                    break;
                default:
                    var baseline = device.Mac == DemoRoster.Meter1 ? 420 : device.Mac == DemoRoster.Meter2 ? 640 : 310;
                    value = baseline + 60 * Math.Sin(phase) + Noise(random, 25);
                    if (device.Mac == DemoRoster.Meter2 && i is >= 60 and <= 64)
                        value = 3250 + Noise(random, 120);
                    value = Math.Round(value);
                    break;
            }

            points.Add(new BackfillPointDto(at, Math.Round(value, 2)));
        }

        return points;
    }

    private static double Noise(Random random, double amplitude) => (random.NextDouble() - 0.5) * 2 * amplitude;
}
