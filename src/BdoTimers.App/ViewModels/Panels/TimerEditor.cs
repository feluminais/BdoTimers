using BdoTimers.Core.Model;
using BdoTimers.Core.Seed;
using BdoTimers.Core.Storage;
using BdoTimers.Core.Text;

namespace BdoTimers.App.ViewModels.Panels;

/// <summary>Private timer settings; the scheduler continues using the saved timer until generation completes.</summary>
public sealed class TimerEditor(AppServices services, TimerDef timer)
{
    public EditDraft<TimerDef> Draft { get; } = new(timer);
    public EditDraft<AppSettings> Settings { get; } = new(services.Settings.Current);
    public TimerDef Current => Draft.Current;
    public bool HasChanges => Draft.HasChanges || Settings.HasChanges;
    public bool IsBusy { get; private set; }
    readonly HashSet<string> _pictures = timer.ImageFile is { } image ? [image] : [];
    public event Action? Changed;

    public void Modify(Func<TimerDef, TimerDef> change, string? key = null)
    {
        var next = change(Current);
        if (next.Scheduled is { } spec)
        {
            BdoTimers.Core.Scheduling.ScheduleMath.ValidateDateRange(spec.StartDate, spec.EndDate);
            BdoTimers.Core.Scheduling.ScheduleMath.ValidateEveryWeeks(spec.EveryWeeks);
        }
        Draft.Update(change, key);
        if (Current.ImageFile is { } picture) _pictures.Add(picture);
        Changed?.Invoke();
    }

    public async Task GenerateAsync(bool forceVoice = false)
    {
        if (IsBusy) return;
        IsBusy = true;
        Changed?.Invoke();
        try
        {
            var data = services.Timers.Current;
            var timers = data.Timers.Select(t =>
            {
                if (t.Id != timer.Id) return t;
                var edited = Draft.Apply(t);
                return forceVoice ? edited with { Enabled = true, Alerts = edited.Alerts with { Tts = edited.Alerts.Tts with { Enabled = true } } } : edited;
            }).Where(t => BossRegions.IsEligible(data, t));
            // Include shared lines with this timer's peers as well as its individual lines.
            var lines = SpeechLines.ForTimers(timers, services.Settings.Current.DefaultLeadTimesMinutes, services.Clock, timer.Id);
            await services.Tts.GenerateLinesAsync(lines, services.Settings.Current.TtsVoice, services.Settings.Current.TtsRate);
        }
        finally { IsBusy = false; Changed?.Invoke(); }
    }

    public async Task SaveAsync()
    {
        await GenerateAsync();
        if (services.Timers.Current.Timers.All(t => t.Id != timer.Id))
            throw new InvalidOperationException("This timer has ended or been removed.");
        services.Timers.Modify(timer.Id, Draft.Apply);
        if (Settings.HasChanges) services.Settings.Update(Settings.Apply);
    }

    public void Close()
    {
        foreach (var picture in _pictures) services.Undo.ReleasePicture(picture);
    }
}
