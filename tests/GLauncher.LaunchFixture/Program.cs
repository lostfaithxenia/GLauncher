using System.Diagnostics;
using System.Text.Json;

// Controlled test processes only. Never points at a user's game or existing application.
string mode = args.ElementAtOrDefault(0) ?? "wait";
int duration = int.TryParse(args.ElementAtOrDefault(1), out int value) ? value : 30000;
if (mode == "spawn")
{
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
    start.ArgumentList.Add("wait"); start.ArgumentList.Add(duration.ToString());
    var child = Process.Start(start)!;
    string? report = args.ElementAtOrDefault(2);
    if (report is not null) File.WriteAllText(report, JsonSerializer.Serialize(new { child.Id, CreationTimeUtc = child.StartTime.ToUniversalTime() }));
    await Task.Delay(500);
}
else await Task.Delay(duration);
