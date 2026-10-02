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

Daily reset remains fixed at 00:00 UTC according to Pearl Abyss's notice; it is 17:00 PDT in NA and 02:00 CEST in EU. The world-boss guide specifies Thursday 00:00 UTC for Garmoth and Morning Light weekly reward resets in both regions. Switching boss region therefore leaves to-do reset times unchanged.

## How the app uses this

Each region's timetable is an embedded JSON file in `src/BdoTimers.Core/Data/` with its source URLs and verification
date, shown in the timetable tooltip in Settings. The app follows the regular weekly wall-clock timetable and each time
zone's DST rules; it doesn't model the short maintenance exceptions above, which need a new Pearl Abyss notice each
time.
