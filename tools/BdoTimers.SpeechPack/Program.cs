using BdoTimers.App.Alerts;
using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Text;
using BdoTimers.Core.Scheduling;

if (args.Length != 2) throw new ArgumentException("Expected voice model and output directories.");
var lines = new HashSet<string> { "Kzarka and Uturi in 5 minutes", "Horse registration time started", "Test boss in 5 minutes" };
foreach (var region in BossRegions.All)
{
    var timers = SeedService.ToTimers(SeedService.LoadEmbedded(region.Id), new(), region.Id)
        .Concat(Presets.Create(region.Id)).Append(Presets.CreateHorseRegistration()).ToList();
    var horse = Presets.CreateHorseRegistration();
    timers.AddRange(Enumerable.Range(1, 10).Select(n => Presets.HorseRun(horse, n)));
    foreach (var line in SpeechLines.ForTimers(timers.Select(t => t with { Enabled = true }), AlertConfig.StandardLeadTimesMinutes, new SystemClock())) lines.Add(line);
}
using var tts = new TtsChannel(new KokoroEngine(Path.GetFullPath(args[0])), new SpeechCache(Path.GetFullPath(args[1])));
Console.WriteLine($"Preparing {lines.Count} standard voice lines...");
await tts.GenerateLinesAsync(lines, KokoroEngine.Default.Id, 0);
Console.WriteLine("Standard voice lines ready.");
