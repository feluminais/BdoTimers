# BDO Timers

Tray app for Black Desert Online (EU) world-boss spawns and your own timers.

- Bosses screen: previous, next and following spawn at a glance, over a week grid in your local time
- Timers screen: Farm (22-hour growth estimate that continues through overgrowth to 200%), Fishing (stopwatch) and
  Horse registration (up to ten independent 10-minute waits from the game's registration notice to sale) on top,
  then your own countdowns and weekly timers
- To-do screen: daily and weekly checklists with child rows, shared reset times and your own lists
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert in a natural offline
  voice (Kokoro), an in-game overlay you can pin or call up with a hotkey (clock, bosses, farm, fishing, horse registrations)
- Can start with Windows, minimized to the tray (off until you turn it on in Settings)

## Install
`pwsh scripts/publish.ps1` builds `publish/BdoTimers-Setup-<version>.exe`. Run it; no admin rights needed.

- Next shows the install location, `%LocalAppData%\Programs` by default; the app always gets its own `BdoTimers` folder
  inside it, shown in gold after the path. The install is per-user, so a folder that needs admin rights (Program Files) is refused with a note.
- Adds a Start Menu shortcut and can launch the app when setup finishes. Start with Windows stays off until you turn
  it on in Settings; uninstall removes it.
- Everything the app keeps (timers, to-do lists in `todos.json`, settings, your sounds and pictures, logs) is in `Data` inside its folder; the app
  warns and exits if it can't write there.
- Run the setup again (or Uninstall in Settings → Apps) to see where it's installed and to repair, move or remove it.
  Moving takes the data along; uninstall deletes it, first offering to open the folder if you added sounds or pictures.
- Newer builds upgrade in place, reuse the chosen folder, and close the running app first.
- Silent: `BdoTimers-Setup-<version>.exe /quiet InstallRoot=D:\Games` and `/quiet /uninstall`.

Bump `<Version>` in `Directory.Build.props` for each release you hand out.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Priority notifications).
- The overlay only appears over the game in borderless window mode.
- Overlay settings are behind the screen icon in the top bar or in the tray menu; drag the overlay while the panel is open.
- To-do lists start Off. Turn one on, tick its boxes, and click its text to edit. Use + by Weekly or Daily to make a list;
  Settings controls the reset time for all lists of that kind and can restore deleted default lists.
- Open the Horse registration tile to set its start hotkey. Each press starts a separate timer and speaks
  “Horse registration time started”. The overlay's Sections include horse registrations; it shows the newest two and
  counts any others that are running.

## Develop
See CLAUDE.md for commands. `scripts/get-voice.ps1` fetches the voice model (132 MB) that the setup ships.

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
6. Set an Always show hotkey (Ctrl+Shift+O), BDO in borderless: the hotkey pins and unpins the overlay and the panel's
   switch follows. Set Show on hotkey (F9, 10 s): F9 shows it for 10 s, F9 again hides it early. With Always show off,
   a countdown set to Overlay 2 min before still pops it up.
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
19. Settings → Bosses → Reset all boss alert settings → Reset: every pencil is gone, bosses with alerts off are on
    again, spawn times unchanged.
20. Timers: Farm and Fishing come first and have no Delete. Hover idle Farm → clock icon → pick a time 2 h ago → Start:
    it runs with 20:00:xx left and "Started <that time>". Fishing → clock → 1 h ago → Start: it counts from 01:00:00.
    Farm → clock → a time over 22 h ago: it starts overgrown, shows negative time and growth above 100% on the tile
    and overlay. The displayed growth caps at 200%. Pause freezes the negative time; resume continues it. The square
    resets a timer.
21. Timers: Horse registration is third, 10:00, and its (i) explains when to start. Open its panel and set a start
    hotkey. Press it twice: two numbered registration tiles appear with independent countdowns, and each press speaks
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
