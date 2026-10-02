# Boss region source verification

Verified on **2026-10-02** for Black Desert **PC NA/EU**. Console and Mobile schedules are separate and were not used.

## Schedule sources

- Pearl Abyss [World Bosses guide](https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=83), inspected in the browser on 2026-10-02. The live page reports its last edit as 2026-09-24 09:45 UTC.
- [Official NA timetable image](https://s1.pearlcdn.com/NAEU/Upload/WIKI/f8e0383561920251224145958103.png), visually inspected on 2026-10-02. Its URL contains the upload date 2025-12-24; its heading specifies NA (PT).
- [Official EU timetable image](https://s1.pearlcdn.com/NAEU/Upload/WIKI/8dc7628e14e20251224150012814.png), linked from the same guide. EU's existing seed was previously verified on 2026-09-23 and remains the migration source.
- [Black Desert Foundry table](https://www.blackdesertfoundry.com/world-bosses-dungeons-guide/), checked on 2026-10-02 as corroboration. Every scheduled NA world-boss cell below agrees with the primary image. Its article-level last-updated label says February 2024 despite containing newer content, so that label is not a reliable timetable revision date.

## Verified NA weekly timetable

Wall-clock times are in US Pacific time. A slash means simultaneous bosses. Abbreviations: Pig = Golden Pig King. Black Shadow is a field boss and is excluded, consistently with the existing EU boss roster. Dark Bonghwang is a random replacement for Morning Light bosses, not an additional scheduled spawn.

| PT | Monday | Tuesday | Wednesday | Thursday | Friday | Saturday | Sunday |
|---|---|---|---|---|---|---|---|
| 00:00 | Pig / Kzarka | Sangoon / Nouver | Pig / Kutum | Bulgasal / Karanda | Uturi / Kzarka | Bulgasal / Nouver | Sangoon / Offin |
| 10:00 | Uturi / Nouver | Pig / Kutum | Bulgasal / Nouver | Sangoon / Kzarka | Bulgasal / Karanda | Uturi / Kzarka | Pig / Kutum |
| 12:00 | Garmoth | Garmoth | Garmoth | Garmoth | Garmoth | Garmoth | Garmoth |
| 14:00 | — | — | — | Quint / Muraka | Sangoon / Kutum | — | Vell |
| 17:00 | Sangoon / Karanda | Bulgasal / Kzarka | Vell | Uturi / Offin | Pig / Nouver | Quint / Muraka | Garmoth |
| 20:15 | Pig / Kutum | Sangoon / Nouver | Sangoon / Karanda | Bulgasal / Kutum | Uturi / Kzarka | — | Uturi / Karanda |
| 21:15 | Garmoth | Garmoth | Garmoth | Garmoth | Garmoth | — | Garmoth |
| 22:15 | Bulgasal / Offin | Uturi / Karanda | Uturi / Kzarka | Pig / Nouver | Sangoon / Karanda | Pig / Kutum | Bulgasal / Nouver |

There are 13 named scheduled bosses and 84 boss slots per week, including 14 Garmoth slots. NA Quint/Muraka spawn on Thursday and Saturday; EU's Tuesday/Thursday slots must not be shifted to derive NA.

## Server clock and daylight saving

Pearl Abyss's [2026 PC daylight-saving notice](https://www.naeu.playblackdesert.com/en-US/News/Detail?groupContentNo=9799), verified on 2026-10-02, explicitly distinguishes NA PST/PDT from EU CET/CEST. It identifies NA's spring transition as March 8 and EU's as March 29. After those transitions, world bosses return to their usual wall-clock slots. This supports `America/Los_Angeles` for the regular NA timetable and the existing `Europe/Berlin` for EU; a fixed UTC offset or EU's DST dates would be incorrect for NA.

The notice also records a maintenance exception: NA bosses were one hour earlier in PST after March 5 maintenance until March 8, and EU bosses were one hour earlier in CET after March 26 maintenance until March 29. Server schedule adjustments can therefore precede the civil clock change. A recurring time-zone rule alone does not reproduce those short maintenance windows. Future maintenance windows must be verified from new Pearl Abyss notices, not inferred from past maintenance dates.

[NIST's DST rules](https://www.nist.gov/pml/time-and-frequency-division/popular-links/daylight-saving-time-dst), checked on 2026-10-02, identify US DST as March 8 through November 1 in 2026. Regular NA offsets are UTC−08:00 (PST) and UTC−07:00 (PDT). The October 2026 EU transition and November 2026 NA transition are different weeks. No 2026 autumn game-maintenance adjustment was verified in this research.

Daily reset remains fixed at 00:00 UTC according to Pearl Abyss's notice; it is 17:00 PDT in NA and 02:00 CEST in EU. The world-boss guide specifies Thursday 00:00 UTC for Garmoth and Morning Light weekly reward resets in both regions. Boss-region selection should therefore leave the application's saved UTC/local daily and weekly to-do schedules and next-reset boundaries unchanged.

## Implementation status

EU and NA are implemented through the Core region catalog. NA's embedded seed uses `America/Los_Angeles`; EU
retains `Europe/Berlin` and its existing spawn times. Settings selects the region for boss screens, the overlay and
alerts. Source URLs and verification dates are included in the bundles and shown in the timetable tooltip.

The implementation follows the regular weekly wall-clock timetable and each time zone's DST rules. It does not
automatically model the short server-maintenance exceptions described above. A new verified notice is needed before
adding any future date-specific override. Custom timers retain their own zones, and region selection never changes
to-do schedules, checks or saved next-reset boundaries.

## Integration and verification

- Bosses remain in the existing timer list, with a stable region id. Per-region profiles save seeding, accepted baseline
  and timetable notice state. Migration assigns legacy configuration to EU, retaining ids, schedules, alert choices
  and future skipped occurrences. Returning to a seeded region restores its saved timers without duplicating bosses.
- Timetable comparison, Apply selected, Keep current times, spawn reset and alert reset operate on the selected region.
  A pending review or reset confirmation closes when selection changes. Backup validation checks all saved profiles.
- Boss board/grid/tiles and overlay use the selected region. Cache invalidation follows the immutable `AppData` snapshot.
- Scheduler and speech preparation exclude inactive bosses. Switching atomically saves a selection token and a monotonic
  UTC boundary from `IClock`, so already-due leads and overlay windows cannot replay after rapid switches or rollback.
  Queued audio rechecks eligibility after waiting and before speech; shared custom alerts remain eligible.
- Changes use immutable records through `PersistentState.Update` and `TimerStore`. To-do state is not touched by selection.

Core tests cover preserved migration, persistent EU→NA→EU round trips, independent edits/alerts/baselines, selected-only
views/alerts/speech, old-lead and queued-alert suppression, custom alerts, unchanged personal to-do reset boundaries,
failed-save atomicity and backup round trips. Pacific seed conversions cover March 8/November 1 and October 25 while
EU has already changed. Fake-clock scheduler tests exercise edited spawn times in both regions' spring gaps and autumn
folds, including single firing in a repeated hour. The README's manual checklist covers UI, audio and restore behavior.

Validation on **2026-10-02**: `dotnet test tests/BdoTimers.Core.Tests` passed **430 tests**; `dotnet build
src/BdoTimers.App` succeeded with no errors. Both reported NU1900 because the environment could not reach NuGet's
vulnerability metadata. The manual UI/audio checklist was updated but was not executed in this session.
