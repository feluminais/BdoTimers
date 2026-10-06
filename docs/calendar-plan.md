# Calendar screen

The Month of the Schedule screen (beside Today, Timers and To-do): everything BDO Timers knows will happen, on a month
in local time, with the chosen day's full list beside it. It adds no new kind of data: it is a view over what the app already
schedules, plus shortcuts into the existing panels.

## What it shows

| Source | Items | Notes |
|---|---|---|
| Bosses (selected region, bundled and added) | Each spawn | States from the week grid: past, next, skipped, alerts off |
| Weekly timers (own, Guild bosses, Guild war) | Each occurrence within its start/end dates | Can be skipped like bosses |
| One-time events | The event | Finished events stay, as past |
| Running countdowns (Farm, Horse registrations, own) | Their end time | Only while running; a paused one has no end |
| To-do resets | Daily and weekly reset | From Settings → To-do |

Stopwatches have no end and never appear.

## How it's built

- Core: `Scheduling/CalendarQuery.cs`. `CalendarQuery.Between` lists `CalendarItem(Kind, AtUtc, Timer, State)` for a range;
  `CalendarQuery.Month` lays a month out as 42 Monday-first local days. States come from `WeekGrid.StateOf`, shared with
  the Bosses week grid so the two can't disagree. Day placement is by local date; daylight-saving gaps and repeats go
  through `ScheduleMath.LocalToUtc` as everywhere else. Tests: `CalendarQueryTests`.
- Filters: `AppSettings.Calendar` (`CalendarSettings`): Bosses, Timers (weekly timers and countdowns), Events, Resets.
- App: `ViewModels/CalendarViewModel.cs` and `Views/CalendarView.xaml`, the "Calendar" tab in `MainWindow.xaml`, refreshed
  from the window's tick and `Timers.Changed`. The month is rebuilt only when the data, settings, month, chosen day,
  local day or time zone change, or when an item passes; other ticks do nothing.
- Month cells list own timers and events and the weekly reset (three lines, "+N more" past that) and count the boss
  spawns, which happen every day and would crowd everything else out. A gold dot marks the day of the next boss spawn.
  The daily reset is only in the day list for the same reason.
- The day list shows every item: click opens `BossPanelViewModel` or `CustomPanelViewModel`; right-click a boss or
  weekly timer to skip or unskip that occurrence (`TimerStore.ToggleMute`). New event creates a one-time event on the
  chosen day (today or later) and opens its panel.
- Smoke test: `CalendarViewTests` (navigation, day selection, filter chips). Manual steps: `docs/manual-tests.md` 63.

## Next steps

1. Week view (the Bosses week-grid layout with all kinds) and an agenda of the next days.
2. Export `.ics`: weekly items as `RRULE` with the timer's `TZID`, one-time events as single `VEVENT`s, skipped spawns as
   `EXDATE`, so the schedule can be imported into a phone calendar. A file save, no network.
3. Maybe: reset markers only for lists with unchecked rows; countdown ends off by default if they prove noisy.
