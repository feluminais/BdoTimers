# BDO Timers

Tray app for Black Desert Online (EU and North America) world-boss spawns and your own timers.

- Bosses screen: previous, next and following spawn at a glance, over a week grid in your local time
- Timers screen: Farm (22-hour growth estimate that continues through overgrowth to 200%), Fishing (stopwatch) and
  Horse registration (up to ten independent 10-minute waits from the game's registration notice to sale) on top,
  then your own countdowns, weekly timers and dated one-time events
- To-do screen: daily and weekly checklists with child rows, shared reset times and your own lists
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert in a natural offline
  voice (Kokoro), an in-game overlay you can pin or call up with a hotkey (clock, bosses, farm, fishing, horse registrations,
  custom countdowns and upcoming one-time events)
- Can start with Windows, minimized to the tray (off until you turn it on in Settings)
- Local backup and restore, and a review of timetable changes included in newer releases

## Install
`pwsh scripts/publish.ps1` builds `publish/BdoTimers-Setup-<version>.exe`. Run it; no admin rights needed.

- Next shows the install location, `%LocalAppData%\Programs` by default; the app always gets its own `BdoTimers` folder
  inside it, shown in gold after the path. The install is per-user, so a folder that needs admin rights (Program Files) is refused with a note.
- Adds a Start Menu shortcut and can launch the app when setup finishes. Start with Windows stays off until you turn
  it on in Settings; uninstall removes it.
- Everything the app keeps (timers, to-do lists in `todos.json`, settings, your sounds and pictures, logs) is in `Data` inside its folder; the app
  warns and exits if it can't write there.
- Run the setup again (or Uninstall in Settings → Apps) to see where it's installed and to repair or remove it.
  Uninstall keeps your data by default. Clear "Keep my timers, lists and settings" to delete it, including the recovery
  copies made by backup restore. Reinstall into the same folder to use the kept data.
- Newer builds upgrade in place, reuse the chosen folder, and close the running app first.
- To change the install location, export a backup first and restore it in the new installation, or close the app
  and copy the existing `Data` folder into the new `BdoTimers` folder. Setup does not move personal data.
- Silent: `BdoTimers-Setup-<version>.exe /quiet InstallRoot=D:\Games` and `/quiet /uninstall`.
  Quiet uninstall keeps data; `/quiet /uninstall DeleteData=1` explicitly deletes it.

The setup's version is `1.0.<number of commits>`. Publishing requires full Git history; each descendant commit gets
a higher version. For a shallow clone, run `git fetch --unshallow` first.

Settings → About → Check for updates checks the [official GitHub releases](https://github.com/feluminais/BdoTimers/releases).
It shows Checking, Up to date, Update available (with the version and View release), or Couldn’t check.
View release opens GitHub so you can download and run the setup yourself. The app never downloads or installs updates.
Release builds also check in the background on startup, at most once per 24 hours. A newer stable release shows a green
update icon before Overlay settings in the top bar, with no Windows update notification. Click it to see a small dialog
over the app with the version, official release URL, Open GitHub and instructions to download the .exe to update.
Failed and manual attempts count toward the daily limit, saved in
`Data/update-check.json`. Drafts, prereleases, equal versions and older versions are ignored. Offline or failed checks
leave timers running, time out after ten seconds, and can be retried manually. Debug builds only check on request.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Priority notifications).
- The overlay only appears over the game in borderless window mode.
- Overlay settings are behind the screen icon in the top bar or in the tray menu; drag the overlay while the panel is open.
- Default shortcuts: Ctrl+Shift+F8 pins the overlay, Ctrl+Shift+F9 shows it for 10 seconds, and Ctrl+Shift+F10 starts a
  Horse registration timer. They work while the app is in the tray. Change or clear them in Overlay settings and the
  Horse registration tile if they conflict with your game bindings. Existing saved shortcuts stay as set.
- Custom countdowns have an optional hotkey in their timer panel: Ready → Start, Running → Pause, Paused → Resume.
  It works with BDO focused and the app in the tray, including with Alerts off or the overlay off. Reset stays on the
  tile. Change or clear the hotkey in the panel; shortcuts already assigned anywhere in BDO Timers are refused.
  Tray → Custom timers also offers Start, Pause or Resume. Overlay → Sections → Custom timers shows running and
  paused countdowns in creation order, with paused times marked “Paused”.
- Timers → New timer → One-time event: set the name, date (`YYYY-MM-DD`), time (`HH:mm`) and time zone, then choose
  alert channels and lead times. The event appears on the Timers screen and in the overlay's Custom timers section;
  an enabled Overlay alert also brings it into pop-ups. It stays visible as Finished after its occurrence and never
  repeats. Edit its date, time or time zone to a future occurrence to use it again, or delete it. Reopening after a
  missed event shows one concise missed-event notice, with no replayed lead alerts or notices on later launches.
- Weekly timers have optional Start date and End date in their own time zone; blank means no limit. Both dates are
  inclusive, even when the occurrence falls on a different date in your local time. After the last allowed occurrence,
  the timer shows Expired and stops scheduling. An end before the start is refused. Events and weekly timers use the
  same DST rules: spring gaps shift forward and an autumn repeated time uses its first instance.
- To-do lists start Off. Turn one on, tick its boxes, and click its text to edit. Use + by Weekly or Daily to make a list;
  Settings controls the reset time for all lists of that kind and can restore deleted default lists.
- Settings → Data → Export backup saves timers, lists, settings, sounds and pictures in one ZIP outside `Data`.
  Restore backup checks the file before asking to replace current data and restart. The previous folder is kept beside
  `Data` as `Data.before-restore-<timestamp>-<id>`. Running timers and checklist resets reconcile with the current time.
- Settings → Bosses → Region chooses Europe or North America for boss screens, the overlay and boss alerts. Each
  region keeps its own Alerts on/off choices, alert settings, edited spawn times, skipped spawns and accepted timetable.
  Switching back restores them; existing installations start in EU with their saved configuration. Custom timers keep
  their schedules and time zones, and to-do checks and reset schedules stay as saved. Already-due boss alerts and
  overlay pop-ups are skipped when switching; future alert leads still fire.
- Settings → Bosses shows the selected timetable's verification date (source URLs in its tooltip). When its spawn times
  change, review the added, removed and changed bosses, then Apply selected or Keep current times. Custom spawn times
  are marked and Off initially. Alert settings and the timers you created are kept. Timetable updates arrive with app
  releases and do not require an online feed.
- NA uses Pacific time (`America/Los_Angeles`, PST/PDT); EU uses `Europe/Berlin` (CET/CEST). Their DST transitions
  occur on different dates. The bundles represent the regular weekly timetable. Short server-maintenance shifts around
  DST are not applied automatically; see [verified sources and exceptions](docs/boss-region-sources.md).
- Open the Horse registration tile to change its start hotkey. Each press starts a separate timer and speaks
  “Horse registration time started”. The overlay's Sections include horse registrations; it shows the newest two and
  counts any others that are running.

## Develop
`scripts/get-voice.ps1` fetches the voice model (132 MB) that the setup ships.

## License
GPL-3.0 (see LICENSE). Bundled components and their licenses are listed in THIRD-PARTY-NOTICES.txt. BDO Timers is not
affiliated with Pearl Abyss; the boss, farm, fishing and horse pictures are Black Desert game artwork © Pearl Abyss and
are not covered by the GPL.

## Manual test checklist
1. First launch: Bosses screen shows the strip and this week's grid in local time; a "priority notifications" toast appears once.
2. Settings (gear) → Test alert → Send: the alert sound, spoken "Test boss in 5 minutes", urgent toast.
3. Timers → New timer → Countdown: set Duration 0:02 and alerts "1, At spawn"; hover the tile, press play → alert at 1:00
   and at 0:00, then it shows Ready again.
4. Start BDO fullscreen, repeat step 3: sound + speech play; toast breaks through.
5. Top bar → Overlay: the overlay shows as a framed preview; drag it, close the panel; restart the app, open the panel:
   same place. Outside the panel, clicks pass through.
6. With BDO in borderless and the app in the tray, Ctrl+Shift+F8 pins and unpins the overlay; the panel's switch
   follows. Ctrl+Shift+F9 shows it for 10 s and a second press hides it early. With Always show off, a countdown set
   to Overlay 2 min before still pops it up.
7. Right-click a future boss in the grid → Skip this spawn: struck through, no alert for it; Unskip restores it.
8. Bosses, under the table → click a boss tile → panel: set Alerts to Off → the tile says "Alerts off", the boss dims
   in the grid and leaves the strip.
9. Tray → Pause alerts for 1 hour: "Alerts paused" shows in the top bar; no alerts; Resume clears it.
10. Close window → app quits by default. Enable Settings → Close to tray → close window → stays in tray; launch the exe again → existing window comes to front at the same size and position. Tray → Quit always exits.
11. Settings → Start with Windows On, reboot → app starts minimized in the tray. Off → it no longer starts.
12. Start a countdown, quit, wait past its end, relaunch → one "ended while closed" toast.
13. Timer panel → click the picture (badge "Change picture") → Choose picture…: the tile shows it, fading into black.
14. Boss panel → Sound: stepping plays nothing; ▶ plays the choice. + → pick a WAV or MP3: it's selected and listed in
    Settings → Your sounds. A file that isn't audio shows "Couldn't play …".
15. Settings → Your sounds → ✕: the sound is gone; timers that used it show Default.
16. Boss panel → Voice shows what it says; Custom voice line… → + Name / + Time left insert at the caret, ▶ speaks the
    line, Reset restores "Kzarka in 5 minutes".
17. Settings → Default alert times: change them → bosses without their own times follow. In a boss panel, change a chip
    → "Use default" appears and the boss gets a pencil in the table; Use default → it follows again.
18. Boss panel → Spawn times opens the list below it; the same header closes it.
19. Settings → Bosses → Reset EU/NA boss alert settings → Reset: the selected region's pencils are gone and bosses
    with alerts off are on again; spawn times and the other region's choices stay as saved.
20. Timers: Farm and Fishing come first and have no Delete. Hover idle Farm → clock icon → pick a time 2 h ago → Start:
    it runs with 20:00:xx left and "Started <that time>". Fishing → clock → 1 h ago → Start: it counts from 01:00:00.
    Farm → clock → a time over 22 h ago: it starts overgrown, shows negative time and growth above 100% on the tile
    and overlay. The displayed growth caps at 200%. Pause freezes the negative time; resume continues it. The square
    resets a timer.
21. Timers: Horse registration is third, 10:00, and its (i) explains when to start. Press Ctrl+Shift+F10 twice: two
    numbered registration tiles appear with independent countdowns, and each press speaks
    "Horse registration time started". The first alerts at 1:00 and 0:00, then disappears without stopping the second.
    Start ten at once: the next press adds none and shows the limit notice. Stop one and a new press can start another.
    With horse registrations enabled in Overlay → Sections, the newest two appear with their time left and "+8 more
    running" at ten. Delete the preset, restart, and check that its hotkey no longer starts registrations.
22. To-do: Weekly quests and Daily tasks start Off, with grey struck rows. Turn Weekly quests on, tick a Garmoth child;
    it moves to the bottom of its group and its parent shows partial. Tick the parent; all three children become done and
    the group moves down. Untick it; the saved order returns. Repeat with Space and check that focus stays on the row.
23. To-do: click row text to open the editor. Rename a row, Enter to add, Tab to make a child, Shift+Tab to move it out,
    Backspace on a blank row to remove it, and Alt+arrows or the grip to reorder. Close and reopen; names, order and
    checks survive. Make a new list with +, then close it untouched; it disappears.
24. Settings → To-do: change the daily time and weekly day, time and zone. Existing checks remain. At a reset boundary,
    checks clear even on an Off list; quitting before the boundary and reopening after it also clears them. The next reset
    is shown in local time in each column header.
25. Delete a default To-do list, restore it from Settings and check that its edited rows return. Delete a custom list,
    restart, and confirm it stays deleted.
26. Settings → Data → Export backup: save a ZIP outside Data, edit a timer and checklist, then Restore backup → Restore.
    The app restarts with the backed-up data; the previous Data folder is kept beside the restored one. Invalid ZIPs
    report an error without closing the app. Cancel a checked restore and confirm the current data stays.
27. With a newer bundled seed for either region, Settings → Bosses → Review timetable changes: compare the slot changes, keep a
    marked custom schedule Off, apply other changes and verify the boss's sound, voice, lead times and Alerts state stay.
    Restart and confirm the reviewed version no longer prompts. Reset spawn times also accepts the bundled version.
28. Uninstall through setup with only JSON edits: the keep-data choice still appears, checked. Keep and reinstall into
    the same folder: edits return. Explicit deletion removes Data and restore recovery copies. Quiet uninstall keeps
    them unless DeleteData=1 is passed.
29. Create two countdowns, assign different hotkeys in their panels, then turn Alerts off. With BDO focused and the app
    in the tray, press one key: Ready → Running → Paused → Running; the other timer stays unchanged. Time freezes while
    paused and resumes with the saved time left. Reset from the tile; the next press starts the full duration. Repeat
    with the overlay off, and after restarting the app with one running and one paused timer.
30. Try assigning an overlay shortcut, Horse registration's shortcut, or another countdown's shortcut: “Used by another
    hotkey”, with the previous binding kept. Repeat from the overlay and Horse registration fields, including with the
    overlay or Show on hotkey off. While any hotkey field listens, no shortcut fires; Esc, clicking away, and closing
    the panel restore bindings. A shortcut held by another app shows “In use by another app”. Change or clear a
    countdown's shortcut, or delete the timer: the old key no longer controls it and can be assigned to another timer.
31. Pin the overlay with two running custom countdowns; pause one, rename one and resume it: rows stay in creation
    order and a paused row says “Paused”. Check List, Card and Bar, turn Custom timers off/on, and confirm Ready and
    completed timers leave the section. Pop-up alerts show each countdown only once. Outside preview, clicks pass
    through; the preview still drags. Horse registration presses still start independent runs.
32. Tray → Custom timers: check Start, Pause and Resume against each countdown's state, including Alerts off. Create,
    rename and delete a timer; reopen the menu and check that the entries follow. Each action controls only that timer.
33. Settings → About → Check for updates: Checking appears immediately and the button is disabled until completion.
    With an equal/newer installed version (or no stable releases), it shows Up to date. With an older installed version,
    it shows Update available and the numeric release version; View release opens that tag's page in the official repository.
    Repeat with `1.0.9` installed and `1.0.10` released, and with draft/prerelease releases: only the newer stable version is offered.
34. Disconnect the network and check manually: Couldn’t check appears within ten seconds, with no release action.
    Restore connectivity and retry; check while a short countdown runs and confirm its alerts still arrive. Close/reopen
    Settings during a check and confirm the current status returns without a second concurrent request. A controlled HTTP
    fixture/proxy returning 403, 429, 500 or malformed JSON should produce the same concise failure with no immediate retry.
35. Start a Release build normally and with `--minimized`: it stays responsive and no Windows update notification appears.
    When an update is available, open the app: a green update icon appears immediately before Overlay settings. Hover:
    “Update available, click to open GitHub”. Click: a small centered dialog shows the version, official release URL and
    “Download the .exe to update.” Cancel, Escape, the close button or clicking outside dismisses it without opening a browser;
    Open GitHub opens the shown release page and closes the dialog. A manual check finding an update also shows the icon.
    Up-to-date and failed checks show no icon. Restart within 24 hours, including after an offline failure or manual check:
    no automatic request. After 24 hours a startup can check again. A Debug build makes no automatic request, and its manual
    check still works. Verify that no installer or release asset was downloaded by any check.
36. Upgrade a copy of existing EU data containing disabled bosses, own alert settings, edited spawn times, skipped
    future spawns and an accepted baseline. EU stays selected, with the same bosses and choices; no new duplicate
    bosses or timetable changes appear. To-do checks, list order and next reset times stay as saved.
37. Settings → Bosses → Region → North America: the strip, grid, tiles and pinned/hotkey overlay show NA in local
    time. Verify Quint/Muraka at Thursday 14:00 and Saturday 17:00 Pacific, Vell at Wednesday 17:00 and Sunday 14:00.
    EU bosses do not alert or start overlay pop-ups. Custom countdowns and scheduled timers still alert at their own times.
38. Edit NA Kzarka's spawn times, alert leads, sound/voice and Alerts state; make different edits to EU Kzarka. Switch
    EU → NA → EU repeatedly, restart and return to NA: each configuration, skipped future spawn and accepted baseline
    returns, with 13 bosses per region. Export/restore a backup while NA is selected; both regions' choices return.
39. Open a timetable review or reset confirmation, then switch region: it closes and the verification label, source
    tooltip and reset actions follow the new region. Apply selected, Keep current times and Reset spawn times affect
    only that region. Accept a newer EU timetable, switch to an unaccepted NA version and confirm NA still requests review.
40. With a boss lead just passed, switch away and back before the next scheduler tick: no old toast, sound, speech or
    overlay pop-up replays, while later leads still fire. Queue a boss alert behind long audio and switch regions:
    its pending audio is dropped, including after switching back. A shared custom timer still receives its alert.
41. Set personal daily/weekly to-do times, including a local-time daily reset, tick rows and record both next reset
    headers. Switch regions and restart before those boundaries: checks, schedules and next reset times are unchanged.
42. In a disposable dev environment, check regular Garmoth noon Pacific spawns around NA's 2026 DST boundaries:
    March 7 → 20:00 UTC, March 8 → 19:00 UTC, October 25 → 19:00 UTC, November 1 → 20:00 UTC. EU switches on
    March 29/October 25. For edited times in a spring gap, the app shifts forward; an autumn repeated time alerts once
    at its first instance. Check the [source notes](docs/boss-region-sources.md) for separate maintenance exceptions.
43. Timers → New timer → One-time event: name it, set a date/time a few minutes ahead and choose its zone. Set own
    leads to 1 minute and At spawn, enable all channels and pin the overlay. Verify the timer counts down, sound,
    urgent toast and speech arrive at each lead, and the event appears once in List, Card and Bar. Turn Custom timers
    off: its Overlay alert still pops up within its configured window. After the occurrence, its tile stays Finished,
    it leaves the overlay, and no alerts repeat. Restart and confirm it remains Finished.
44. Edit that finished event's name or alert settings: it stays Finished. Edit its date/time into the future (also try
    changing the zone so its occurrence is future): it counts down and alerts once again. Delete it and restart:
    it stays deleted. Turn Alerts off or pause all alerts across another event: it still becomes Finished at its end.
45. Quit before an event, reopen after it (including less than 60 seconds late): one concise missed-event notice and
    a Finished tile, with no sound, speech or replayed leads. Quit/reopen again: no second notice. Repeat after sleep
    beyond the 60-second grace. Export/restore a finished event and confirm its state persists without another notice.
46. Weekly timer: set Start date to a matching weekday more than eight days away and End date to its following
    occurrence. Verify the tile counts down to the distant start; no earlier lead or overlay appears. Check both
    boundary occurrences alert, then the tile says Expired and subsequent weeks produce nothing. Clear either date
    and confirm the remaining limit works; clear both and confirm unlimited recurrence returns.
47. Enter an end date before the start, an impossible date (2026-02-30), and invalid time: concise errors appear and
    Done is disabled; correct them and Done returns. Close/reopen with invalid input: the last valid saved values
    remain. Restart and export/restore timers with both limits: dates, slots, zone, channels and leads stay saved.
48. In disposable data, compare one-time and single-day weekly schedules at Europe/Berlin 2026-03-29 02:30 and
    2026-10-25 02:30: the spring occurrence is 01:30 UTC and the autumn occurrence is 00:30 UTC, firing once.
    Check a weekly Pacific Friday 23:30 with the same start/end Friday: its Saturday occurrence in Europe is included.
    Upgrade existing JSON without date fields: countdowns, presets, boss profiles, weekly slots and alerts are retained.
