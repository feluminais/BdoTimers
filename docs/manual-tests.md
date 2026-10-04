# Manual test checklist

This is the authoritative manual checklist. Run against disposable data; close only a dev process whose executable
is under `bin\Debug`. Record the tested commit, Windows version, displays/scales and results in the release notes.
Unchecked items are unperformed, including all manual checks for the current improvement work; automated checks do
not establish Narrator, gaming, monitor, sleep, disk-failure or installer behavior.

Automated checks: `dotnet test tests/BdoTimers.Core.Tests`, `dotnet build src/BdoTimers.App`, and
`pwsh scripts/test-windows.ps1`. Windows CI runs these without downloading the speech model or publishing an
installer. The WPF tests use actual controls on an STA dispatcher for panel focus, keyboard navigation, command
bindings and automation names. Release packaging and signing: [releasing.md](releasing.md).
1. [ ] First launch: Bosses screen shows the strip and this week's grid in local time; a "priority notifications" toast appears once.
2. [ ] Settings (gear) → Test alert → Send: the alert sound, spoken "Test boss in 5 minutes", urgent toast.
3. [ ] Timers → New timer → Countdown: set Duration 0:02 and alerts "1, At spawn"; hover the tile, press play → alert at 1:00
   and at 0:00, then it shows Ready again.
4. [ ] Start BDO fullscreen, repeat step 3: sound + speech play; toast breaks through.
5. [ ] Top bar → Overlay: the overlay shows as a framed preview; drag it, close the panel; restart the app, open the panel:
   same place. Outside the panel, clicks pass through.
6. [ ] With BDO in borderless and the app in the tray, Ctrl+Shift+F8 pins and unpins the overlay; the panel's switch
   follows. Ctrl+Shift+F9 shows it for 10 s and a second press hides it early. With Always show off, a countdown set
   to Overlay 2 min before still pops it up.
7. [ ] Right-click a future boss in the grid → Skip this spawn: struck through, no alert for it; Unskip restores it.
8. [ ] Bosses, under the table → click a boss tile → panel: set Alerts to Off → the tile says "Alerts off", the boss dims
   in the grid and leaves the strip.
9. [ ] Tray → Pause alerts for 1 hour: "Alerts paused" shows in the top bar; no alerts; Resume clears it.
10. [ ] Close window → app quits by default. Enable Settings → Close to tray → close window → stays in tray; launch the exe again → existing window comes to front at the same size and position. Tray → Quit always exits.
11. [ ] Settings → Start with Windows On, reboot → app starts minimized in the tray. Off → it no longer starts.
12. [ ] Start a countdown, quit, wait past its end, relaunch → one "ended while closed" toast.
13. [ ] Timer panel → click the picture (badge "Change picture") → Choose picture…: the tile shows it, fading into black.
14. [ ] Boss panel → Sound: stepping plays nothing; ▶ plays the choice. + → pick a WAV or MP3: it's selected and listed in
    Settings → Your sounds. A file that isn't audio shows "Couldn't play …".
15. [ ] Settings → Your sounds → ✕: the sound is gone; timers that used it show Default.
16. [ ] Boss panel → Voice shows what it says; Custom voice line… → + Name / + Time left insert at the caret, ▶ speaks the
    line, Reset restores "Kzarka in 5 minutes".
17. [ ] Settings → Default alert times: change them → bosses without their own times follow. In a boss panel, change a chip
    → "Use default" appears and the boss gets a pencil in the table; Use default → it follows again.
18. [ ] Boss panel → Spawn times opens the list below it; the same header closes it.
19. [ ] Settings → Bosses → Reset EU/NA boss alert settings → Reset: the selected region's pencils are gone and bosses
    with alerts off are on again; spawn times and the other region's choices stay as saved.
20. [ ] Timers: Farm and Fishing come first and have no Delete. Hover idle Farm → clock icon → pick a time 2 h ago → Start:
    it runs with 20:00:xx left and "Started <that time>". Fishing → clock → 1 h ago → Start: it counts from 01:00:00.
    Farm → clock → a time over 22 h ago: it starts overgrown, shows negative time and growth above 100% on the tile
    and overlay. The displayed growth caps at 200%. Pause freezes the negative time; resume continues it. The square
    resets a timer.
21. [ ] Timers: Horse registration is third, 10:00, and its (i) explains when to start. Press Ctrl+Shift+F10 twice: two
    numbered registration tiles appear with independent countdowns, and each press speaks
    "Horse registration time started". The first alerts at 1:00 and 0:00, then disappears without stopping the second.
    Start ten at once: the next press adds none and shows the limit notice. Stop one and a new press can start another.
    With horse registrations enabled in Overlay → Sections, the newest two appear with their time left and "+8 more
    running" at ten. Delete the preset, restart, and check that its hotkey no longer starts registrations.
22. [ ] To-do: Weekly quests and Daily tasks start Off, with grey struck rows. Turn Weekly quests on, tick a Garmoth child;
    it moves to the bottom of its group and its parent shows partial. Tick the parent; all three children become done and
    the group moves down. Untick it; the saved order returns. Repeat with Space and check that focus stays on the row.
23. [ ] To-do: click row text to open the editor. Rename a row, Enter to add, Tab to make a child, Shift+Tab to move it out,
    Backspace on a blank row to remove it, and Alt+arrows or the grip to reorder. Close and reopen; names, order and
    checks survive. Make a new list with +, then close it untouched; it disappears.
24. [ ] Settings → To-do: change the daily time and weekly day, time and zone. Existing checks remain. At a reset boundary,
    checks clear even on an Off list; quitting before the boundary and reopening after it also clears them. The next reset
    is shown in local time in each column header.
25. [ ] Delete a default To-do list, restore it from Settings and check that its edited rows return. Delete a custom list,
    restart, and confirm it stays deleted.
26. [ ] Settings → Data → Export backup: save a ZIP outside Data, edit a timer and checklist, then Restore backup → Restore.
    The app restarts with the backed-up data; the previous Data folder is kept beside the restored one. Invalid ZIPs
    report an error without closing the app. Cancel a checked restore and confirm the current data stays.
27. [ ] With a newer bundled seed for either region, Settings → Bosses → Review timetable changes: compare the slot changes, keep a
    marked custom schedule Off, apply other changes and verify the boss's sound, voice, lead times and Alerts state stay.
    Restart and confirm the reviewed version no longer prompts. Reset bosses also accepts the bundled version.
28. [ ] Uninstall through setup with only JSON edits: the keep-data choice still appears, checked. Keep and reinstall into
    the same folder: edits return. Explicit deletion removes Data and restore recovery copies. Quiet uninstall keeps
    them unless DeleteData=1 is passed.
29. [ ] Create two countdowns, assign different hotkeys in their panels, then turn Alerts off. With BDO focused and the app
    in the tray, press one key: Ready → Running → Paused → Running; the other timer stays unchanged. Time freezes while
    paused and resumes with the saved time left. Reset from the tile; the next press starts the full duration. Repeat
    with the overlay off, and after restarting the app with one running and one paused timer.
30. [ ] Try assigning an overlay shortcut, Horse registration's shortcut, or another countdown's shortcut: “Used by another
    hotkey”, with the previous binding kept. Repeat from the overlay and Horse registration fields, including with the
    overlay or Show on hotkey off. While any hotkey field listens, no shortcut fires; Esc, clicking away, and closing
    the panel restore bindings. A shortcut held by another app shows “In use by another app”. Change or clear a
    countdown's shortcut, or delete the timer: the old key no longer controls it and can be assigned to another timer.
31. [ ] Pin the overlay with two running custom countdowns; pause one, rename one and resume it: rows stay in creation
    order and a paused row says “Paused”. Check List, Card and Bar, turn Custom timers off/on, and confirm Ready and
    completed timers leave the section. Pop-up alerts show each countdown only once. Outside preview, clicks pass
    through; the preview still drags. Horse registration presses still start independent runs.
32. [ ] Tray → Custom timers: check Start, Pause and Resume against each countdown's state, including Alerts off. Create,
    rename and delete a timer; reopen the menu and check that the entries follow. Each action controls only that timer.
33. [ ] Settings → About → Check for updates: Checking appears immediately and the button is disabled until completion.
    With an equal/newer installed version (or no stable releases), it shows Up to date. With an older installed version,
    it shows Update available and the numeric release version; View release opens that tag's page in the official repository.
    Repeat with `1.0.9` installed and `1.0.10` released, and with draft/prerelease releases: only the newer stable version is offered.
34. [ ] Disconnect the network and check manually: Couldn’t check appears within ten seconds, with no release action.
    Restore connectivity and retry; check while a short countdown runs and confirm its alerts still arrive. Close/reopen
    Settings during a check and confirm the current status returns without a second concurrent request. A controlled HTTP
    fixture/proxy returning 403, 429, 500 or malformed JSON should produce the same concise failure with no immediate retry.
35. [ ] Start a Release build normally and with `--minimized`: it stays responsive and no Windows update notification appears.
    When an update is available, open the app: a green update icon appears immediately before Overlay settings. Hover:
    “Update available, click to open GitHub”. Click: a small centered dialog shows the version, official release URL and
    “Download the .exe to update.” Cancel, Escape, the close button or clicking outside dismisses it without opening a browser;
    Open GitHub opens the shown release page and closes the dialog. A manual check finding an update also shows the icon.
    Up-to-date and failed checks show no icon. Restart within 24 hours, including after an offline failure or manual check:
    no automatic request. After 24 hours a startup can check again. A Debug build makes no automatic request, and its manual
    check still works. Verify that no installer or release asset was downloaded by any check.
36. [ ] Upgrade a copy of existing EU data containing disabled bosses, own alert settings, edited spawn times, skipped
    future spawns and an accepted baseline. EU stays selected, with the same bosses and choices; no new duplicate
    bosses or timetable changes appear. To-do checks, list order and next reset times stay as saved.
37. [ ] Settings → Bosses → Region → North America: the strip, grid, tiles and pinned/hotkey overlay show NA in local
    time. Verify Quint/Muraka at Thursday 14:00 and Saturday 17:00 Pacific, Vell at Wednesday 17:00 and Sunday 14:00.
    EU bosses do not alert or start overlay pop-ups. Custom countdowns and scheduled timers still alert at their own times.
38. [ ] Edit NA Kzarka's spawn times, alert leads, sound/voice and Alerts state; make different edits to EU Kzarka. Switch
    EU → NA → EU repeatedly, restart and return to NA: each configuration, skipped future spawn and accepted baseline
    returns, with 13 bosses per region. Export/restore a backup while NA is selected; both regions' choices return.
39. [ ] Open a timetable review or reset confirmation, then switch region: it closes and the verification label, source
    tooltip and reset actions follow the new region. Apply selected, Keep current times and Reset bosses affect
    only that region. Accept a newer EU timetable, switch to an unaccepted NA version and confirm NA still requests review.
40. [ ] With a boss lead just passed, switch away and back before the next scheduler tick: no old toast, sound, speech or
    overlay pop-up replays, while later leads still fire. Queue a boss alert behind long audio and switch regions:
    its pending audio is dropped, including after switching back. A shared custom timer still receives its alert.
41. [ ] Set personal daily/weekly to-do times, including a local-time daily reset, tick rows and record both next reset
    headers. Switch regions and restart before those boundaries: checks, schedules and next reset times are unchanged.
42. [ ] In a disposable dev environment, check regular Garmoth noon Pacific spawns around NA's 2026 DST boundaries:
    March 7 → 20:00 UTC, March 8 → 19:00 UTC, October 25 → 19:00 UTC, November 1 → 20:00 UTC. EU switches on
    March 29/October 25. For edited times in a spring gap, the app shifts forward; an autumn repeated time alerts once
    at its first instance. Check the [source notes](boss-region-sources.md) for separate maintenance exceptions.
43. [ ] Timers → New timer → One-time event: name it, set a date/time a few minutes ahead and choose its zone. Set own
    leads to 1 minute and At spawn, enable all channels and pin the overlay. Verify the timer counts down, sound,
    urgent toast and speech arrive at each lead, and the event appears once in List, Card and Bar. Turn Custom timers
    off: its Overlay alert still pops up within its configured window. After the occurrence, its tile stays Finished,
    it leaves the overlay, and no alerts repeat. Restart and confirm it remains Finished.
44. [ ] Edit that finished event's name or alert settings: it stays Finished. Edit its date/time into the future (also try
    changing the zone so its occurrence is future): it counts down and alerts once again. Delete it and restart:
    it stays deleted. Turn Alerts off or pause all alerts across another event: it still becomes Finished at its end.
45. [ ] Quit before an event, reopen after it (including less than 60 seconds late): one concise missed-event notice and
    a Finished tile, with no sound, speech or replayed leads. Quit/reopen again: no second notice. Repeat after sleep
    beyond the 60-second grace. Export/restore a finished event and confirm its state persists without another notice.
46. [ ] Weekly timer: set Start date to a matching weekday more than eight days away and End date to its following
    occurrence. Verify the tile counts down to the distant start; no earlier lead or overlay appears. Check both
    boundary occurrences alert, then the tile says Expired and subsequent weeks produce nothing. Clear either date
    and confirm the remaining limit works; clear both and confirm unlimited recurrence returns.
47. [ ] Enter an end date before the start, an impossible date (2026-02-30), and invalid time: concise errors appear and
    Done is disabled; correct them and Done returns. Close/reopen with invalid input: the last valid saved values
    remain. Restart and export/restore timers with both limits: dates, slots, zone, channels and leads stay saved.
48. [ ] In disposable data, compare one-time and single-day weekly schedules at Europe/Berlin 2026-03-29 02:30 and
    2026-10-25 02:30: the spring occurrence is 01:30 UTC and the autumn occurrence is 00:30 UTC, firing once.
    Check a weekly Pacific Friday 23:30 with the same start/end Friday: its Saturday occurrence in Europe is included.
    Upgrade existing JSON without date fields: countdowns, presets, boss profiles, weekly slots and alerts are retained.
49. [ ] Keyboard, each panel: open Settings, Overlay, boss, timer, New timer, Update and To-do from a focused control.
    Focus moves to a meaningful field/action, Tab and Shift+Tab stay inside, and dimmed screen/caption controls cannot
    activate. Escape and the close button dismiss; after the 120 ms fade, focus returns to the opener. Change panels
    quickly and close during opening; no stale focus or blank modal layer remains. Preserve Ctrl+C/X/V/A/Z, caret
    keys and native selection in text fields; Ctrl+Z outside an editor undoes the last eligible action. Hotkey capture
    retains its own Escape cancel behavior and existing system-wide shortcuts continue to work. Open Overlay and
    Settings with the mouse, close each panel and move the pointer away: their caption buttons keep no bright frame.
    Repeat with timer tiles, picture actions and to-do checkboxes; Tab still gives a visible keyboard focus cue.
50. [ ] Narrator: tabs, window caption buttons, panel Close/Done, tile actions, alert toggles, cycle selectors, date
    fields and Undo announce concise useful names, roles, state and values. Tile actions are reachable without hover;
    focus remains visible on each action. Change a selector with arrows/Home/End and hear the new value.
51. [ ] Windows High Contrast: switch on/off while the app and a panel are open; text, icons, borders, selected tabs,
    errors and focus indicators remain visible. Set Windows text size and app Text size to their largest supported
    values; screens and panels can scroll to every control without clipped actions or obscured fields.
52. [ ] Window geometry: use 1080p at 200% scaling, 100/150/200% displays, a negative-coordinate monitor and a layout
    with a gap. Restore saved partly-offscreen/oversized placements; the complete window fits a real work area with
    its caption and taskbar clear. Move across mixed-DPI screens, change display scale/resolution/taskbar position
    while running, unplug/reconnect the saved monitor (also while hidden to tray), then show the app. Maximize/restore,
    title double-click, Win+arrows and Windows 11 maximize-hover Snap remain usable. Overlay preview and List/Card/Bar
    stay on their monitor and preserve click-through behavior after scale or monitor changes.
53. [ ] Sleep and time changes: pause/resume around a boss lead, custom countdown, one-time event and to-do reset;
    sleep across each boundary and change the Windows time zone/clock. No burst of stale audio or duplicate alerts;
    countdown completion and missed-event handling follow steps 12/45 and checklist reset times remain correct.
54. [ ] Storage failures in a disposable installation: deny Data writes or fill a small test volume, edit settings,
    reset/delete a timer and delete a list. A concise actionable status appears; the previously saved state and UI
    remain consistent and restart retains it. Restore write access and retry successfully. Start with malformed JSON:
    recovery preserves the damaged file and logs the failure. Repeat with inaccessible/missing sound and picture
    files; timer scheduling continues.
55. [ ] Undo: delete a timer/list and reset a running/paused timer; the strip announces the action and Undo restores
    its state, order and hotkey. Check keyboard Ctrl+Z outside text fields, Dismiss, expiry and a newer undoable action.
    Keep a text edit's native Ctrl+Z, try undo after another state change and after a disk-write failure, and restart:
    no stale undo action or duplicate object returns. Destructive restore/reset still use their needed confirmation.
56. [ ] Install lifecycle with a disposable installation: signed setup verifies with Authenticode, first install and
    in-place upgrade preserve data/location, repair works, and uninstall/reinstall follow step 28. Test a path with
    spaces, launch-at-finish, running-app upgrade and offline use after installation. Start, timers, to-do and alerts
    work offline; updates fail concisely as in step 34. Local unsigned artifacts must be explicitly built with
    `pwsh scripts/publish.ps1 -AllowUnsigned` and are not a signed release.
57. [ ] Mouse wheel over the Bosses table, Timers, both To-do lists and each tall panel (Settings, Overlay, boss,
    timer, to-do list) scrolls that list; the boss strip, list headings and each panel's header and Done stay in place.
    Shrink the window to its minimum with the largest Text size: only then does the screen or panel scroll as a whole.
    Clicking anywhere in the dimmed area (beside, above or below a panel, near or far from it) closes it; clicking
    inside the panel, on its scroll bar, or in a list or calendar dropping out past its edge does not.
58. [ ] Timers: Guild bosses and Guild war appear after Horse registration as Not set and have no Delete. Open Guild
    bosses, add a day and time, and confirm only one weekly row is allowed. Remove it and confirm the tile returns to
    Not set. Add it again, restart, and check that its next weekly occurrence remains. Guild war accepts several
    day/time rows; check that the tile advances through them and each occurrence alerts at its configured lead times.
    Remove all rows to return to Not set.
59. [ ] Timers → Guild bosses: set its time 20 minutes ahead. Overlay → Guild bosses → 15 min before: the preview shows
    a dimmed Guild bosses row; the timer panel's Overlay over the game shows the same value, and changing it there
    changes the Overlay panel's. With Always show off and BDO in borderless, the overlay stays hidden until 15 minutes
    before, then pops up with Guild bosses and its time left in List, Card and Bar, and hides after the spawn. Skip next
    on the tile, Alerts Off, or Guild bosses Off: no pop-up.
60. [ ] Overlay → Sections → Local time, Server time and In-game time: all three share the top line with a monitor,
    globe and sun icon. In game, the in-game time matches the game's clock within a minute and the sun turns into a
    moon from 22:00 to 07:00. Server time → Europe opens Settings at Bosses; after choosing North America, the overlay's
    server time and the panel's link follow.
61. [ ] Bosses → Add boss: a "New boss" panel opens with its spawn times expanded in server time. Name it, set a time 20
    minutes ahead and a picture: its tile, the grid, the strip and the pinned overlay show it, and it alerts at its leads.
    A name another boss of the region has turns the field red and keeps the old name. Set an end date of yesterday: it
    leaves the grid, strip and tile's next spawn. Switch region: it shows only in the region it was added to.
62. [ ] Open a timetable boss: no Name or date rows, but its picture can be changed and removed. Remove it → Yes: the
    panel closes, the boss leaves the tile list and grid, and Undo brings it back with its alerts and skipped spawns.
    Remove it again, add a boss and name it like the removed one, then Settings → Bosses → Reset bosses: every timetable
    boss is back with the timetable's times, the added boss with that name took its place, and other added bosses stay.
63. [ ] Calendar: the month opens on today (filled) with today chosen (gold outline). ‹ › change month and Today returns.
    Day cells list own weekly timers, running countdowns, events and the weekly reset, with "+N more" past three, and
    "N boss spawns"; the day with the next boss spawn has a gold dot. Choose a day: its list shows every item in local
    time, coloured as in the week grid. Click a boss or timer: its panel opens. Right-click an upcoming boss or weekly
    timer → Skip this one: it is struck through here and in the Bosses grid; Unskip restores it. Turn each filter chip
    off and on; restart and confirm they are kept. New event on a future day opens a one-time event on that date; it
    is off for past days. Switch boss region: the calendar follows. Around 25 October, Berlin's clock change keeps
    Sunday and Monday spawns on their own days.
