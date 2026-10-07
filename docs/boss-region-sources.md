# Boss timetable sources

Verified on **2026-10-02** for Black Desert **PC NA/EU**. Console and Mobile schedules are separate and were not used.

## Schedule sources

- Pearl Abyss [World Bosses guide](https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=83), checked on 2026-10-02 (page last edited 2026-09-24 09:45 UTC).
- [Official NA timetable image](https://s1.pearlcdn.com/NAEU/Upload/WIKI/f8e0383561920251224145958103.png), checked on 2026-10-02. Uploaded 2025-12-24 (per its URL); its heading specifies NA (PT).
- [Official EU timetable image](https://s1.pearlcdn.com/NAEU/Upload/WIKI/8dc7628e14e20251224150012814.png), linked from the same guide; the EU timetable was checked against it on 2026-09-23.
- [Black Desert Foundry table](https://www.blackdesertfoundry.com/world-bosses-dungeons-guide/), checked on 2026-10-02 as corroboration. Every scheduled NA world-boss cell below agrees with the primary image. Its article-level last-updated label says February 2024 despite containing newer content, so that label is not a reliable timetable revision date.

## Verified NA weekly timetable

Wall-clock times are in US Pacific time. A slash means simultaneous bosses. Abbreviations: Pig = Golden Pig King. Black Shadow is a field boss and is excluded, as in EU. Dark Bonghwang is a random replacement for Morning Light bosses, not an additional scheduled spawn.

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

Pearl Abyss's [2026 PC daylight-saving notice](https://www.naeu.playblackdesert.com/en-US/News/Detail?groupContentNo=9799), verified on 2026-10-02, explicitly distinguishes NA PST/PDT from EU CET/CEST. It identifies NA's spring transition as March 8 and EU's as March 29. After those transitions, world bosses return to their usual wall-clock slots. So NA uses `America/Los_Angeles` and EU uses `Europe/Berlin`; a fixed UTC offset or EU's DST dates would be wrong for NA.

The notice also records a maintenance exception: NA bosses were one hour earlier in PST after March 5 maintenance until March 8, and EU bosses were one hour earlier in CET after March 26 maintenance until March 29. Server schedule adjustments can therefore precede the civil clock change. A recurring time-zone rule alone does not reproduce those short maintenance windows. Future maintenance windows must be verified from new Pearl Abyss notices, not inferred from past maintenance dates.

[NIST's DST rules](https://www.nist.gov/pml/time-and-frequency-division/popular-links/daylight-saving-time-dst), checked on 2026-10-02, identify US DST as March 8 through November 1 in 2026. Regular NA offsets are UTC−08:00 (PST) and UTC−07:00 (PDT). The October 2026 EU transition and November 2026 NA transition are different weeks. No autumn 2026 maintenance adjustment had been announced when this was checked.

Daily reset remains fixed at 00:00 UTC according to Pearl Abyss's notice; it is 17:00 PDT in NA and 02:00 CEST in EU. The world-boss guide specifies Thursday 00:00 UTC for Garmoth and Morning Light weekly reward resets in both regions. Switching boss region therefore leaves to-do reset times unchanged. The Garmoth tracker's week ends at the weekly reset in Settings, Thursday 00:00 UTC by default.

## What differs between servers

The following is for PC NA/EU. War of the Roses recurring times were checked against the official guide on
**2026-10-04**. Dated notices override recurring rules; a timer does not detect game cancellations or maintenance.

| Schedule | EU | NA | Source and app data |
|---|---|---|---|
| Server clock / daylight saving | `Europe/Berlin`, CET/CEST | `America/Los_Angeles`, PST/PDT | The daylight-saving notice above; `Seed/BossRegions.cs`. Each region follows its own civil clock changes, with temporary maintenance exceptions checked separately. |
| World bosses | EU timetable | Separate NA timetable, including different boss/day combinations | Official timetable images above; `Data/bosses.eu.json` and `Data/bosses.na.json`. Do not derive NA by shifting EU. |
| War of the Roses | Third Legion deadline Sunday 15:05; battle 17:00–19:00 | Third Legion deadline Sunday 13:05; battle 15:00–17:00 | [Official guide](https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=367); regional slots in `Seed/BossRegions.cs`, repeat/anchor in `Seed/Presets.cs`. |
| Node war | Sunday–Friday, 20:00–21:00 CET/CEST | Sunday–Friday, 18:00–19:00 PT | [Node War guide](https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=56), [2024 time-change notice](https://www.naeu.playblackdesert.com/en-US/News/Detail?groupContentNo=6934). Guild war is user-configured; the app does not seed node-war times. |
| Conquest war | Saturday 20:00–Sunday 00:00 CET/CEST | Saturday 18:00–22:00 PT | [Conquest War guide](https://www.naeu.playblackdesert.com/en-US/Wiki?wikiNo=344). Guild war is user-configured; the app does not seed conquest-war times. |
| Maintenance | Same UTC window as NA; 10:00–14:00 CEST for 08:00–12:00 UTC | Same UTC window as EU; 01:00–05:00 PDT for 08:00–12:00 UTC | [September 3, 2026 notice](https://www.naeu.playblackdesert.com/en-us/News/Detail?groupContentNo=10531&countryType=en-us). This is a dated Thursday example, not a guaranteed weekly window; use each new notice. No maintenance timer is seeded. |
| Daily and weekly resets | Daily 00:00 UTC; weekly Thursday 00:00 UTC | The same UTC instants | Daylight-saving notice and World Bosses guide above; default to-do resets stay fixed in UTC when region changes. |

Node and conquest hours are the schedule references supplied for this update. The Node War time-change notice was
read directly; the two linked PvP guide pages did not return readable content on 2026-10-04. Recheck their current
rules before changing defaults or documenting a new season. Node-war modes can have different durations; the table
records the one-hour occupation window, not a promise that every mode ends after one hour.

### War of the Roses registration and battle weeks

The guide places the war on O'dyllita-1 every two weeks. Leading Guild applications run EU Saturday 01:00–Sunday
01:10 and NA Friday 23:00–Saturday 23:10; Third Legion applications run EU Sunday 01:10–15:05 and NA Saturday
23:10–Sunday 13:05. These are server wall-clock times. NA's Friday, Saturday and Sunday belong to the same
Monday-first schedule week as its battle.

The default combines **Applications close** (Third Legion deadline) and **Battle** (start) in one timer. It omits
overnight application openings; players can add labelled times. Its reference week contains **2026-09-20**, which
projects occurrences on **2026-10-04** and **2026-10-18**, skipping **2026-10-11**. This reference date was retained
from the schedule research supplied for this update. The cited [August 9 report](https://www.naeu.playblackdesert.com/en-US/News/Detail?groupContentNo=10438&countryType=en-US)
did not return readable content on 2026-10-04; these projected dates are not a newly verified announcement of an
active battle season. Verify a recent dated battle notice/report for both regions when updating the anchor.

Pearl Abyss can suspend or move battles: the [March 2024 schedule notice](https://www.naeu.playblackdesert.com/en-US/News/Detail?groupContentNo=6774)
cancelled the planned March 31 battle pending improvements. The supplied [later suspension reference](https://www.naeu.playblackdesert.com/en-us/News/Detail?groupContentNo=8894&countryType=en-us)
was also unreadable on this check, so it is not used to assert whether battles are active today. During a suspension,
turn this timer's alerts off; for a changed cadence, edit Repeat every and From week of. Reset restores the bundled
schedule for the selected region, keeping name, picture and alerts. A region switch moves a default schedule and
preserves an edited schedule.

## How the app uses this

Each region's timetable is an embedded JSON file in `src/BdoTimers.Core/Data/` with its source URLs and verification
date, shown in the timetable tooltip in Settings. The app follows the regular weekly wall-clock timetable and each time
zone's DST rules; it doesn't model the short maintenance exceptions above, which need a new Pearl Abyss notice each
time.
