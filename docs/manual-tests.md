# Manual test checklist

This is the authoritative manual checklist. Run against disposable data; close only a dev process whose executable
is under `bin\Debug`. Record the tested commit, Windows version, displays/scales and results in the release notes.
Unchecked items are unperformed; automated checks do not establish Narrator, gaming, monitor, sleep, disk-failure or
installer behavior.

Automated checks: `dotnet test tests/BdoTimers.Core.Tests`, `dotnet build src/BdoTimers.App`, and
`pwsh scripts/test-windows.ps1`. Windows CI runs these without downloading the speech model or publishing an
installer. The WPF tests use actual controls on an STA dispatcher for panel focus, keyboard navigation, command
bindings and automation names. Release packaging and signing: [releasing.md](releasing.md).
1. [ ] First launch: the window opens on Today, with the next spawn, Coming up and the daily and weekly tasks in local time; Schedule
   shows this week's grid; a "priority notifications" toast appears once.
2. [ ] Settings (gear) → Test alert → Send: the alert sound, spoken "Test boss in 5 minutes", urgent toast.
3. [ ] Timers → New timer → Countdown: set Duration 0:02 and alerts "1, At spawn"; press the round play button on its card → alert
   at 1:00 and at 0:00, then it shows Ready again.
4. [ ] Start BDO fullscreen, repeat step 3: sound + speech play; toast breaks through.
5. [ ] Settings → Overlay: the overlay shows as a framed preview; drag it, close the panel; restart the app, open the panel:
   same place. Outside the panel, clicks pass through.
6. [ ] With BDO in borderless and the app in the tray, Ctrl+Shift+F8 pins and unpins the overlay; the panel's switch
   follows. Ctrl+Shift+F9 shows it for 10 s and a second press hides it early. With Always show off, a countdown set
   to Overlay 2 min before still pops it up.
   In Overlay settings, check Mouse proximity Off / Fade / Hide. Close the panel and approach each overlay edge:
   Fade dims the entire overlay, Hide disappears, and moving away restores it without flicker. Clicks still reach BDO.
   Repeat with List, Card and Bar, changed size, negative-position monitors and different display scales. Leave the
   pointer nearby until a hotkey show or pop-up expires: moving away must not bring the expired overlay back.
   Open the panel while proximity-hidden: the preview returns immediately and remains draggable in either mode.
   Restart: the chosen mode persists. Off keeps the normal appearance even with the pointer over the overlay.
   With the overlay showing, hold Ctrl+Shift (Overlay settings → Drag to move): the pointer over the overlay becomes the
   move cursor and the overlay can be dragged, in BDO too; let go and clicks pass through it again. Mouse proximity
   still applies while the keys are held: Fade keeps it faint under the pointer, Hide leaves nothing to grab, so use the
   panel's preview then. The new place survives a restart. Clear the field and the keys do nothing; set Alt alone, or
   Ctrl+Alt+D, and those keys do it.
7. [ ] Schedule → Bosses: right-click a future boss in the grid → Skip this spawn: struck through, no alert for it; Unskip
   restores it. On Today, Skip on the hero (on hover, or right-click) skips every boss of the next spawn at once; the
   hero shows them struck through and "Skipped", and Unskip brings them back.
8. [ ] Schedule → Following → switch a boss off: its row dims and says "Alerts off", the boss dims in the grid and leaves
   Today's hero and Coming up; switch it on again. Click its name: its panel opens.
9. [ ] Bell (top bar) or tray → Pause alerts for 1 hour: the bell turns amber and "Alerts paused" with Resume shows in the
   top bar; no alerts; Resume clears it. Pause alerts until resumed: the same, without a time.
10. [ ] Close window → app quits by default. Enable Settings → Close to tray → close window → stays in tray; launch the exe again → existing window comes to front at the same size and position. Tray → Quit always exits.
11. [ ] Settings → Start with Windows On, reboot → app starts minimized in the tray. Off → it no longer starts.
12. [ ] Start a countdown, quit, wait past its end, relaunch → one "ended while closed" toast.
13. [ ] Timer panel → click the picture (badge "Change picture") → Choose picture…: the card shows it, fading into black.
    Choose a file that isn't a picture, here and in Overlay → Picture: "Couldn't add …" shows and the picture stays.
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
20. [ ] Timers: Farm and Fishing come first and have no Delete. Hover idle Farm → the clock icon above its
    round button → pick a time 2 h ago → Start: it runs with 20:00:xx left and "Started <that time>". Fishing → clock → 1 h ago → Start: it counts from 01:00:00.
    Farm → clock → a time over 22 h ago: it starts overgrown, shows negative time and growth above 100% on the card
    and overlay. The displayed growth caps at 200%. Pause freezes the negative time; resume continues it. The square
    Stop on a started card resets a timer.
21. [ ] Timers: Horse registration is third, 10:00, and its (i) explains when to start. Its round button is a plus.
    Press Ctrl+Shift+F10 twice: the card stays one card, saying "2 of 10 active" with the two soonest clocks one under
    the other, both smaller than the single clock was, and no registration gets a card of its own, on Timers or in Today's
    Running (which lists Horse registration with the plus). A third press adds an ellipsis under the clocks, which turns gold
    with the card's border; the card still shows two clocks. Each press speaks
    "Horse registration time started". The first alerts at 1:00 and 0:00, then ends without stopping the second. Today's
    Coming up and the Month show one Horse registration entry, the next to end, and it opens the card's panel.
    Click the card: Running lists Horse 1, Horse 2 with their times, each with a square Stop. The card's own square Stop
    (on hover) ends the latest. Undo restores a stopped one.
    Start ten at once: the next press adds none and shows the limit notice. Stop one and a new press can start another.
    With horse registrations enabled in Overlay → Sections, the newest two appear with their time left and "+8 more
    running" at ten. Delete the preset, restart, and check that its hotkey no longer starts registrations.
22. [ ] To-do: Weekly quests and Daily tasks start Off, one dashed row each. Turn Weekly quests on with its switch: its card
    appears, with a progress line. Tick an Olvia Academy child; the line grows, it moves to the bottom of its group and its
    parent shows partial. Tick the parent; all four children become done and the group moves down.
    Untick it; the saved order returns. Repeat with Space and check that focus stays on the row.
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
    Restart and confirm the reviewed version no longer prompts. Reset spawn times also accepts the bundled version.
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
    When an update is available, open the app: a green dot sits on the gear (tooltip “Settings · update available”)
    and on About in Settings, whose page shows Update available, the version and View release; View release opens the
    shown release page. A manual check finding an update also shows the dots. Up-to-date and failed checks show none.
    Restart within 24 hours, including after an offline failure or manual check: no automatic request. After 24 hours a startup can check again. A Debug build makes no automatic request, and its manual
    check still works. Verify that no installer or release asset was downloaded by any check.
36. [ ] Upgrade a copy of existing EU data containing disabled bosses, own alert settings, edited spawn times, skipped
    future spawns and an accepted baseline. EU stays selected, with the same bosses and choices; no new duplicate
    bosses or timetable changes appear. To-do checks, list order and next reset times stay as saved.
37. [ ] Settings → Bosses → Region → North America: Today, the Week grid, the Following list and the pinned/hotkey overlay show NA in local
    time. Verify Quint/Muraka at Thursday 14:00 and Saturday 17:00 Pacific, Vell at Wednesday 17:00 and Sunday 14:00.
    EU bosses do not alert or start overlay pop-ups. Custom countdowns and scheduled timers still alert at their own times.
38. [ ] Edit NA Kzarka's spawn times, alert leads, sound/voice and Alerts state; make different edits to EU Kzarka. Switch
    EU → NA → EU repeatedly, restart and return to NA: each configuration, skipped future spawn and accepted baseline
    returns, with 13 bosses per region. Export/restore a backup while NA is selected; both regions' choices return.
39. [ ] Open a timetable review or reset confirmation, then switch region: it closes and the verification label, source
    tooltip and reset actions follow the new region. Apply selected, Keep current times and Reset spawn times affect
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
43. [ ] Timers → New timer → One-time event: name it, set a date/time a few minutes ahead and choose its zone by typing
    a city ("kyiv") on the open and the closed list; Backspace edits the search and Escape on the open list restores
    the earlier zone. Set own leads to 1 minute and At spawn, enable all channels and pin the overlay. Verify the timer
    counts down, sound, urgent toast and speech arrive at each lead, and the event appears once in List, Card and Bar.
    Turn Custom timers off: its Overlay alert still pops up within its configured window. After the occurrence, its
    tile stays Finished, it leaves the overlay, and no alerts repeat. Restart and confirm it remains Finished.
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
49. [ ] Keyboard, each panel: open Settings, Overlay, Following, boss, timer, New timer and To-do from a focused control.
    Focus moves to a meaningful field/action, Tab and Shift+Tab stay inside, and dimmed screen/caption controls cannot
    activate. Escape and the close button dismiss; after the panel has left, focus returns to the opener. Change panels
    quickly and close during opening; no stale focus or blank modal layer remains. Preserve Ctrl+C/X/V/A/Z, caret
    keys and native selection in text fields; Ctrl+Z outside an editor undoes the last eligible action. Hotkey capture
    retains its own Escape cancel behavior and existing system-wide shortcuts continue to work. Open Overlay and
    Settings with the mouse, close each panel and move the pointer away: their caption buttons keep no bright frame.
    Repeat with timer cards, picture actions and to-do checkboxes; Tab still gives a visible keyboard focus cue.
    Open Settings with the mouse, then close it with Escape: no focus frame appears on the Settings button. Typing
    in a mouse-focused search field also leaves focus frames hidden. Tab or keyboard button activation shows them.
    Tab to Timers, then click that same tab and click empty space: the focus frame disappears without changing the
    selected tab. Tab again: the frame returns outside the text. Move the mouse or scroll: the frame disappears;
    Tab brings it back. A stationary pointer during keyboard navigation keeps the frame visible.
    Repeat with names/links at the largest Text size;
    no focus frame crosses a label. Switch away from the app: no lingering keyboard frame remains.
50. [ ] Narrator: tabs, window caption buttons, panel Close/Done, tile actions, alert toggles, cycle selectors, date
    fields and Undo announce concise useful names, roles, state and values. Card actions are reachable without hover (Tab shows them);
    focus remains visible on each action. Change a selector with arrows/Home/End and hear the new value.
51. [ ] Windows High Contrast: switch on/off while the app and a panel are open; text, icons, borders, selected tabs,
    errors and focus indicators remain visible. Set Windows text size and app Text size to their largest supported
    values; screens and panels can scroll to every control without clipped actions or obscured fields. With app Text
    size back at 100%, step it to 150%: the window grows by the same factor within its screen, the tabs and caption
    buttons grow with the text and the caption still drags the window. Boss tile labels and clocks, week grid names
    and timer countdowns stay whole at any window width; a narrow tile shrinks them and a long name wraps. Return to
    100%: the window regains its earlier size.
52. [ ] Window geometry: use 1080p at 200% scaling, 100/150/200% displays, a negative-coordinate monitor and a layout
    with a gap. Restore saved partly-offscreen/oversized placements; the complete window fits a real work area with
    its caption and taskbar clear. Move across mixed-DPI screens, change display scale/resolution/taskbar position
    while running, unplug/reconnect the saved monitor (also while hidden to tray), then show the app. Maximize/restore,
    title double-click, Win+arrows and Windows 11 maximize-hover Snap remain usable. Overlay preview and List/Card/Bar
    stay on their monitor and preserve click-through behavior after scale or monitor changes.
    Click Maximize/Restore repeatedly: each click changes the window size once and no white native button appears.
    Press Maximize, move away and release: it does not maximize. After a background inspection, bring the app forward
    from the taskbar, Alt+Tab and tray; repeat while minimized and after hiding to tray. It stays usable and can move
    above ordinary windows. Repeat Maximize/Restore at each Text size and with a panel open (caption stays disabled).
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
57. [ ] Mouse wheel over the Week grid, Coming up, Timers, both To-do lists and each tall panel (Settings, Overlay,
    boss, timer, to-do list) scrolls that list; the Schedule header, list headings and each panel's header and Done stay in place.
    Shrink the window to its minimum with the largest Text size: only then does the screen or panel scroll as a whole.
    Clicking anywhere in the dimmed area (beside, above or below a panel, near or far from it) closes it; clicking
    inside the panel, on its scroll bar, or in a list or calendar dropping out past its edge does not.
58. [ ] Timers: Guild bosses appears after Horse registration as Off and has no Delete; there is no Guild war tile.
    Open Guild bosses: Active Off hides Weekly time and Time zone. Turn it On: one Monday 20:00 row with no remove
    or Add time. Change the time and Save; turn Active Off and Save (tile: Off), then On: the time is kept. Discard an
    Active edit: the saved state stays. Restart and check that its next weekly occurrence remains. Data from an earlier
    version drops a Guild war without times; one with times stays, accepts several day/time rows and can be deleted.
59. [ ] Timers → Guild bosses: turn Active On, set its time 20 minutes ahead and Save. Overlay → Guild bosses → 15 min before: the preview shows
    a dimmed Guild bosses row; the timer panel's Overlay over the game shows the same value, and changing it there
    changes the Overlay panel's. With Always show off and BDO in borderless, the overlay stays hidden until 15 minutes
    before, then pops up with Guild bosses and its time left in List, Card and Bar, and hides after the spawn. Skip next
    on the tile, Active Off, Alerts Off, or Guild bosses Off: no pop-up.
60. [ ] Overlay → Sections → Local time, Server time and In-game time: all three share the top line with a monitor,
    globe and sun icon. In game, the in-game time matches the game's clock within a minute and the sun turns into a
    moon from 22:00 to 07:00. Server time → Europe opens Settings at Bosses; after choosing North America, the overlay's
    server time and the panel's link follow.
61. [ ] Overlay → Look → Outline: Off removes the clock box, timer chip borders and preview frame immediately;
    On restores their faint neutral outlines in List, Card and Bar. With Off and background opacity at zero, the
    preview remains draggable. Close the panel and restart: the selected outline setting remains saved.
62. [ ] Overlay → Card: the clocks sit above the card in a raised section with smoothly curved shoulders and no seam.
    Toggle each clock and then all clocks off: the section fits the enabled clocks and disappears when none remain.
    Check colour and picture backgrounds, opacity and scale; Outline follows the joined shape in preview. With the
    background and outline off, drag the clock section and card body; both move the overlay together.
63. [ ] Edit Volume, speech speed, overlay size and either opacity, then immediately change another setting, preview an
    alert or close the panel. Every slider keeps its edit and previews use the new values. Type a timer name, duration
    or custom voice line and immediately press Done or Escape: reopening shows the final valid value. Blank names,
    invalid durations, duplicate/invalid weekly times and blank voice lines disable Done; fixing them enables it.
    Close/Escape with an invalid draft retains the last valid saved value. Delete a timer immediately after editing
    its duration; Undo restores the final edit. A horse run finishing during an edit closes without an error.
64. [ ] Tray → Custom timers: hover and keyboard navigation open the submenu and every enabled timer action works.
    Shrink the window at the largest text size and drag/page a horizontal scrollbar in both directions. Binary
    settings toggle with one click or Space and Narrator reports their state. Voice/sound dropdowns list all choices;
    time-zone typing still matches words such as Kyiv. Ctrl+F and keyboard hotkey capture show a focus cue; mouse
    interaction hides it. Setup: a long install root scrolls inside its field while the BdoTimers suffix stays visible,
    and Tab to Launch BDO Timers shows its focus cue.
65. [ ] Schedule → Following → Add boss: a "New boss" panel opens with its spawn times expanded in server time. Name it, set a time 20
    minutes ahead and a picture: its Following row, the grid, Today and the pinned overlay show it, and it alerts at its leads.
    A name another boss of the region has turns the field red and keeps the old name. Set an end date of yesterday: it
    leaves the grid and Today, and its row loses the next spawn. Switch region: it shows only in the region it was added to.
66. [ ] Open a timetable boss: no Name or date rows, but its picture can be changed and removed. Remove it → Yes: the
    panel closes, the boss leaves the Following list and grid, and Undo brings it back with its alerts and skipped spawns.
    Remove it again, add a boss and name it like the removed one, then Settings → Bosses → Reset bosses: every timetable
    boss is back with the timetable's times, the added boss with that name took its place, and other added bosses stay.
67. [ ] Schedule → Calendar: the month opens on today (filled) with today chosen (gold outline). ‹ › change month and Today returns.
    Day cells show the day's number large and light, and under it list own weekly timers, running countdowns, events and
    the weekly reset by name (the time is the tooltip), with "+N more" past three, and "N bosses"; the day with the
    next boss spawn has a gold dot. Choose a day: its list shows every item in local
    time, coloured as in the week grid. Click a boss or timer: its panel opens. Right-click an upcoming boss or weekly
    timer → Skip this one: it is struck through here and in the Week grid; Unskip restores it. Turn each filter chip
    off and on; restart and confirm they are kept. New event on a future day opens a one-time event on that date; it
    is off for past days. Switch boss region: the month follows. Around 25 October, Berlin's clock change keeps
    Sunday and Monday spawns on their own days.
68. [ ] War of the Roses appears once in Timers, with Applications close and Battle in the same panel. EU defaults
    are Sunday 15:05 and 17:00 Berlin; NA defaults are 13:05 and 15:00 Pacific. Repeat is 2, From week of is
    2026-09-20, and Schedule → Calendar shows both on 4 and 18 October, neither on 11 October. Opening and closing its panel
    leaves the saved schedule untouched. Switch region: defaults move, edited slots/repeat/date limits stay.
69. [ ] Edit a War of the Roses time, label, repeat, anchor and date limits; restart and confirm they persist. Reset
    to EU/NA times → No keeps edits; Yes restores the current region's slots, labels, repeat, anchor, date limits and
    time zone in the open panel. Name, picture, Alerts on/off and alert settings stay. Edit a restored row and confirm
    Save keeps it. Change region while the panel is open: its reset link names the current region.
70. [ ] A weekly timer accepts Repeat every 1–52 weeks; above 1, From week of appears. Try 0, 53, blank and text,
    then an invalid anchor date and reversed date limits. Fields show errors and Save stays off until all errors are
    corrected; editing another valid field must not clear an existing error. Pick 3 weeks and any Wednesday anchor:
    the whole Monday–Sunday week runs, the next two skip, then it repeats. Repeat 1 hides the anchor. Confirm start
    and end dates still bound the results, and the schedule survives restart and backup/restore.
71. [ ] Add labels to two times in one timer. The tile says Next <label>, Month cells and day rows show
    <timer> · <label>, and the overlay pop-up, notification and speech identify the time that fires. Blank labels
    show the timer's name only. Two different labels at the same day/time are rejected; fixing the duplicate saves
    both. A timer in a DST spring gap retains its label when the occurrence moves forward.
72. [ ] Turn on a 10-minute overlay pop-up for a fortnightly timer whose next occurrence is more than 8 days away:
    it appears at that occurrence's window, including after restart. Skip it or turn Alerts off: no pop-up. Check
    EU's 25 October and NA's 1 November clock changes separately; scheduled times remain in their own server clock.
    War of the Roses without a custom picture shows the placeholder and adds no missing-picture errors to the log.
73. [ ] Edit a timer's name or voice line. Generate prepares all selected alert times and the spawn line; Save waits
    with a loading animation, then keeps the edits. Reopen and Save unchanged wording: saved audio is reused. Leave
    by clicking outside, Escape, the close button or closing the window: Keep editing retains the draft; Discard
    keeps the saved timer. Change voice or speed in Settings and Save; alerts use the new audio. A failed generation
    leaves the draft open for retry. Start a countdown by hotkey while its editor is open, edit its duration and Save:
    its end follows the new duration without restarting it. On a fresh install, default boss and preset lines play
    from the bundled files. Saved custom lines still work after more than 30 days without use.

74. [ ] Time fields (Guild bosses, One-time event, Settings → To-do): type 9 and a space: it reads 09: with the caret after
    the colon, and 3 then gives 09:3. Type 9 and press Enter: it reads 09:00, is saved and the panel closes.
    Type 134, 13 40 or 13.40 and click another field: 13:40; 14 reads 14:00, 24 and 2 4 read 02:40.
    Type 97 and press Enter: it stays red and is not saved. Discard a completed time edit: the saved time stays.
75. [ ] Panels: the boss, timer, New timer, Following and to-do list panels open as a drawer from the right over a dimmed
    window; Esc, the dimmed area and ✕ close it, and the discard question shows inside the drawer. Settings opens as a
    two-pane sheet: categories on the left, Ctrl+F searches every category, and Overlay in the list opens the Overlay
    panel. At Text size 150% the drawer and the sheet fit the window; with Windows animation effects off they appear without sliding.
76. [ ] Timers: each countdown and stopwatch card shows its round Start / Pause at rest, in the same gold while running;
    the triangle and the two bars sit in the middle of the circle at 100%, 125% and 150% display scale. Hovering a
    card shows Started earlier above it and, once it has been started, a square Stop below it; Tab reaches both and shows
    them. A weekly card has Skip next / Unskip next in the middle instead. Cards have no menu; the border turns gold on
    hover and stays gold while Started earlier is open. In the week grid and on Today, right-click a boss: its name stays
    lit while the menu is open. New timer is the button in the header.
77. [ ] Following: switch a boss off and on; the list is busy while its voice lines are made, and a failed save puts the
    switch back with a note. The name opens the boss panel; Done closes the list.
78. [ ] To-do: an Off list is one dashed row with a switch that turns it on; a card's ⋯ (on hover) opens its editor, as the
    name does; the editor's Active switch turns it off again and the card collapses to the dashed row.
79. [ ] Today: the hero counts down to the next spawn, in the text colour, gold within 30 minutes, amber within 10 and ember
    within 1; "Then" names the following spawn and the alert times, "Previous" the last one. A name opens that boss. Coming
    up lists the next 24 hours under a gold rule with the time now: bosses a filled dot, timers a ring, resets a small dot;
    bosses that spawn together are one line with a dot between their names, each name a button that opens that boss and,
    on a right click, skips that boss's spawn; a boss with alerts off is missing, a skipped spawn is struck through, and the
    times to each update as time passes.
    Running shows each started or paused timer with a ring that fills, its clock and Start / Pause, and "All timers ›"
    opens Timers; with none running the panel is gone. Daily tasks lists the open tasks of the active daily lists (at most
    six), ticking one removes it and moves the line; Weekly tasks does the same for the weekly lists, a task with
    sub-tasks on one line saying how far along it is (ticking it ticks them all); its name or its arrow opens the
    sub-tasks under it, each ticked there, and it stays open as they are ticked. A task's text opens nothing; the heading
    with its count is the button, hover lights it: with one list on it opens that list's editor, with several it opens
    To-do. The panel is gone while no weekly list is on. Either panel says "+n more ›" when tasks don't fit, and that opens To-do. Below 820 px wide the screen is one
    column: hero, Running, Coming up, Daily tasks, Weekly tasks.
80. [ ] The top bar's next-boss chip shows on Schedule, Timers and To-do (not on Today) with the next spawn's names and
    clock in the same colours as the hero, and a click goes to Today. Its names are trimmed to the room between the tabs
    and the buttons and it is gone when there is under about 170 px of it (a narrow window, or the paused notice taking
    the room) and when no boss is followed. The paused notice shows from 830 px wide; below that the bell's slash says it.
81. [ ] Overlay → Look → Boss icons: turn on; previous and next bosses and world-boss pop-ups show outline faces in
    List, Card and Bar. Paired bosses show two icons; an unfamiliar added boss keeps its name. Skipped bosses stay dim.
    Farm, Fishing and custom timers keep their labels. Hover an icon in preview: its boss name appears. Change Size:
    icons stay sharp. Close the panel and restart: the setting persists; turn it off to restore boss names.
82. [ ] Garmoth tracker: Settings → Bosses → Garmoth tracker starts off, and Today has no Garmoth panel. Turn it on and Save:
    a Garmoth panel with 1 2 3 shows under the weekly tasks, 0/3. Press 2: 1 and 2 fill, 2/3, and Garmoth still shows in
    the hero, Coming up and alerts. Press 3: 3/3 and "Back" with the weekly reset; Garmoth is gone from the hero, Coming up
    and the overlay, no alert sounds for his spawns, and his cells in Schedule and the Calendar are grey, while the spawn
    just killed stays as Previous. Press 3 again: 2/3 and he is back. Mark all three, set Settings → To-do → Weekly reset a
    minute ahead and Save: at the reset the count clears and Garmoth returns. Turn the tracker off and Save: the panel goes
    and Garmoth is as before. The weekly quests have no "Boss's Roar — Garmoth" row, in an old data folder too after the
    first start. Below 820 px the panel follows Weekly tasks in the one column.
83. [ ] Top bar, the bell: a click opens the alerts menu under it and the bell stays lit; click the bell again and the menu
    closes and does not open again. A click elsewhere and Esc close it too. Pause alerts for 1 hour from it: the bell
    shows the slash, and the menu's Resume alerts brings it back.
