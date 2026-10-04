# Plan: Calendar screen

A fourth screen beside Bosses, Timers and To-do, showing everything BDO Timers knows will happen, on a month (and
later week and agenda) calendar in local time. It adds no new kind of data: it is a view over what the app already
schedules, plus shortcuts into the existing panels.

## What it shows

| Source | Entries | Notes |
|---|---|---|
| Bosses (selected region, bundled and added) | Each spawn, bosses at the same instant grouped | States from the week grid: past, next, skipped, alerts off |
| Weekly timers (own, Guild bosses, Guild war) | Each occurrence within its start/end dates | Same skip state as bosses |
| One-time events | The event | Finished events stay visible as past |
| Running countdowns (Farm, Horse registrations, own) | Their end time | Only while running; a paused one has no end. Farm shows its 100% time |
| To-do resets | Daily and weekly reset | From Settings → To-do, shown as a quiet marker, not an event |

Stopwatches have no end and never appear.

## Core (`src/BdoTimers.Core/Scheduling/CalendarQuery.cs`)

- `CalendarQuery.Build(AppData timers, AppSettings settings, DateTimeOffset fromUtc, DateTimeOffset toUtc, DateTimeOffset now)`
  returns `IReadOnlyList<CalendarEntry>` sorted by time:
  `CalendarEntry(CalendarKind Kind, DateTimeOffset AtUtc, IReadOnlyList<TimerDef> Timers, CalendarState State)`,
  with `CalendarKind` Boss, Weekly, OneTime, CountdownEnd, Reset and `CalendarState` Past, Upcoming, Next, Skipped,
  AlertsOff.
- Reuse what exists, nothing new to keep in sync:
  - bosses: `BossBoard.Spawns(data, from, to, followedOnly: false)`, with the same state rules as `WeekGrid.Build`
    (moved into a shared helper so the grid and the calendar can't disagree);
  - weekly and one-time timers: `OccurrenceSource.Between`, filtered with `BossRegions.IsEligible`;
  - countdowns: `CountdownSpec.EndsAtUtc` of running ones;
  - resets: step `TodoReset.Next` through the range.
- Day placement is by local date (`TimeZoneInfo.Local`); daylight-saving gaps and repeats go through
  `ScheduleMath.LocalToUtc` as everywhere else.
- A `CalendarCache` like `BossBoardCache`: rebuilt when the `AppData` instance, the shown range or the local day changes.
  A month holds about 360 boss spawns plus timers, so one build per change is cheap; per-second ticks only move states.
- Tests (`tests/BdoTimers.Core.Tests/CalendarQueryTests.cs`, with `FakeClock`): each source appears; skipped and alerts-off
  states; dated weekly timers and dated added bosses stop at their end date; region switch hides the other region's
  bosses; a running countdown appears and a paused one doesn't; entries around EU and NA DST changes land on the right
  local day; ranges across a month boundary.

## App

- `ViewModels/CalendarViewModel.cs` and `Views/CalendarView.xaml`; a "Calendar" `TabButton` in `MainWindow.xaml` after
  To-do, wired like the other screens and refreshed from the same timer tick and `Timers.Changed`.
- Month view first: Monday-first 6×7 grid like the week grid; each day cell lists up to 4 entries (time, name, state colour
  from the week grid's styles) and "+N more", which opens that day as a list. Today is gold-outlined, the next entry
  marked as on the Bosses screen.
- Header: ‹ month ›, Today, and filter toggles (Bosses, Timers, Events, Resets). Filters and the last view are kept in a
  new `AppSettings.Calendar` record (add it to `JsonRoundTripTests`' sample and `SavedDataValidation`).
- Interactions, all through existing code:
  - click an entry → `BossPanelViewModel` or `CustomPanelViewModel`;
  - right-click a boss or weekly entry → Skip / Unskip (`TimerStore.ToggleMute`), as in the week grid;
  - double-click an empty day → new one-time event on that date (`OneTimeEvents.Create` with the date filled in), opening
    its panel.
- Copy stays minimal per CONTRIBUTING: labels only, a tooltip on an entry gives its full time, state and "in 2 h".
- Smoke test in `tests/BdoTimers.App.Tests`: the view binds, the filter toggles have automation names, an entry opens
  the right panel. Manual steps added to `docs/manual-tests.md` (DST week, region switch, many-entry day).

## Phases

1. `CalendarQuery` + tests; read-only month view.
2. Click to open, skip/unskip, create an event on a day.
3. Filters (saved), week view (reuse the Bosses week-grid layout with all kinds) and a day/agenda list.
4. Optional: export `.ics` (weekly entries as `RRULE` with the timer's `TZID`, one-time events as single `VEVENT`s,
   skipped spawns as `EXDATE`), so the schedule can be imported into a phone calendar. A file save, no network, in line
   with the app's no-network rule.

## Open questions

- Show countdown ends at all? They move when paused or restarted, so they may read as noise; they could be off by default.
- Past entries: dim them (as the week grid does) or hide days before today?
- Should the to-do reset markers be per list (only lists with unchecked rows) or always both?
- Week start is Monday everywhere in the app; follow that rather than the Windows locale.
