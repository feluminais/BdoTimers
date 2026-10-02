using System.Diagnostics;
using System.Runtime.InteropServices;
using BdoTimers.Core.Diagnostics;

namespace BdoTimers.App;

/// <summary>Local performance observations; no timer data or machine identity is collected.</summary>
public static class PerformanceMetrics
{
    static long _began;
    static long _startupMilliseconds = -1;
    static long _firstSpeechMilliseconds = -1;
    static string? _speechSource;

    public static void Begin() => Interlocked.CompareExchange(ref _began, Stopwatch.GetTimestamp(), 0);

    public static void Started()
    {
        var elapsed = (long)Stopwatch.GetElapsedTime(Volatile.Read(ref _began)).TotalMilliseconds;
        if (Interlocked.CompareExchange(ref _startupMilliseconds, elapsed, -1) == -1)
            Log.Info($"App ready in {elapsed} ms");
    }

    public static void SpeechReady(long milliseconds, bool cached)
    {
        if (Interlocked.CompareExchange(ref _firstSpeechMilliseconds, milliseconds, -1) != -1) return;
        _speechSource = cached ? "cached" : "generated";
        Log.Info($"First speech ready in {milliseconds} ms ({_speechSource})");
    }

    public static async Task<string> ReportAsync(FailureInfo? lastFailure, CancellationToken cancellation = default)
    {
        using var process = Process.GetCurrentProcess();
        var start = Stopwatch.GetTimestamp();
        var before = process.TotalProcessorTime;
        await Task.Delay(TimeSpan.FromSeconds(1), cancellation);
        process.Refresh();
        var cpu = Math.Clamp((process.TotalProcessorTime - before).TotalSeconds /
            Stopwatch.GetElapsedTime(start).TotalSeconds / Environment.ProcessorCount * 100, 0, 100);
        var startup = Volatile.Read(ref _startupMilliseconds);
        var speech = Volatile.Read(ref _firstSpeechMilliseconds);
        var failure = lastFailure is null ? "None" : $"{lastFailure.Area}: {lastFailure.Message} ({lastFailure.Count} occurrence(s), {lastFailure.OccurredAt:u})";
        return $"""
            BDO Timers {ProductVersion.Number}
            Windows: {Environment.OSVersion.Version}
            Architecture: {RuntimeInformation.ProcessArchitecture}
            Runtime: {RuntimeInformation.FrameworkDescription}
            Startup ready: {(startup < 0 ? "Not measured" : $"{startup} ms")}
            CPU (1-second sample, whole machine): {cpu:F2}%
            Working set: {process.WorkingSet64 / 1048576d:F1} MB
            Private memory: {process.PrivateMemorySize64 / 1048576d:F1} MB
            First speech ready: {(speech < 0 ? "Not used" : $"{speech} ms ({_speechSource})")}
            Last problem: {failure}
            """;
    }
}
