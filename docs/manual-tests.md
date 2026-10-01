# Manual test checklist

1. First launch: Bosses screen shows the strip and this week's grid in local time; a "Let alerts through while
   gaming" toast appears once.
2. Settings (gear) → Test alert → Send: the alert sound, spoken "Test boss in 5 minutes", urgent toast. Send again:
   the speech now follows the sound at once, read from `Data\speech`.
3. Timers → New timer → Countdown: set Duration 0:02 and alert times 1 and At spawn; hover the tile, press play →
   alert at 1:00 and at 0:00, then it shows Ready again.
4. Start BDO fullscreen, repeat step 3: sound + speech play; toast breaks through.
5. Top bar → Overlay: the overlay shows as a framed preview; drag it, close the panel; restart the app, open the panel:
   same place. Outside the panel, clicks pass through.
6. Set an Always show hotkey (Ctrl+Shift+O), BDO in borderless: the hotkey pins and unpins the overlay and the panel's
   switch follows; clicking into the game keeps the overlay above it. Set Show on hotkey (F9, 10 s): F9 shows it for
   10 s, F9 again hides it early. With Always show off, a countdown set to Overlay 2 min before still pops it up.
7. Right-click a future boss in the grid → Skip this spawn: struck through, no alert for it; Unskip restores it.
8. Bosses, under the table → click a boss tile → panel: set Alerts to Off → the tile says "Alerts off", the boss dims
   in the grid and leaves the strip.
9. Tray → Pause alerts for 1 hour: "Alerts paused" shows in the top bar; no alerts; Resume clears it.
10. Close window → app quits by default. Enable Settings → Close to tray → close window → stays in tray; launch the
    exe again → existing window comes to front at the same size and position. Tray → Quit always exits.
11. Settings → Start with Windows On, reboot → app starts minimized in the tray. Off → it no longer starts.
12. Start a countdown, quit, wait past its end, relaunch → one toast "Countdown ended at HH:mm.". Start another, put
    the PC to sleep past its end, wake it → the same toast, and no alert sound.
13. Timer panel → click the picture (badge "Change picture") → Choose picture…: the tile shows it, fading into black.
14. Boss panel → Sound: stepping plays nothing; ▶ plays the choice. + → pick a WAV or MP3: it's selected and listed in
    Settings → Your sounds. A file that isn't audio shows "Couldn't play …".
15. Settings → Your sounds → ✕: the sound is gone; timers that used it show Default.
16. Boss panel → Voice shows what it says; Custom voice line… → + Name / + Time left insert at the caret, ▶ speaks the
    line, Reset restores "Kzarka in 5 minutes".
17. Settings → change the default alert times → bosses without their own times follow. In a boss panel, change a chip
    → "Use default" appears and the boss gets a pencil in the table; Use default → it follows again.
18. Boss panel → Spawn times opens the list below it; the same header closes it.
19. Settings → Bosses → Reset all boss alert settings → Reset: every pencil is gone, bosses with alerts off are on
    again, spawn times unchanged. Give a boss its own sound and set another's Alerts to Off, then Reset spawn times to
    the EU timetable → Reset: both settings stay, and an edited spawn time is back to the timetable's.
20. Timers: Farm and Fishing come first and have no Delete. Hover idle Farm → clock icon → pick a time 2 h ago → Start:
    it runs with 20:00:xx left and "Started <that time>". Fishing → clock → 1 h ago → Start: it counts from 01:00:00.
    Farm → clock → a time over 22 h ago: it starts overgrown, shows negative time and growth above 100% on the tile
    and overlay. The displayed growth caps at 200%. Pause freezes the negative time; resume continues it. The square
    resets a timer.
21. Timers: Horse registration is third, 10:00, and its (i) explains when to start. Open its panel and set a start
    hotkey. Press it twice: two numbered registration tiles appear with independent countdowns, and each press speaks
    "Horse registration time started". The first alerts at 1:00 and 0:00, then disappears without stopping the second.
    Start ten: the next press adds none and shows the limit toast. Stop one and a new press can start another. With
    horse registrations on in Overlay → Sections, the newest two appear with their time left, and a line counts the
    other eight. Turn the overlay Off: the hotkey still starts registrations. Delete the preset, restart, and check
    that its hotkey no longer starts registrations.
22. To-do: Weekly quests and Daily tasks start Off, with grey struck rows. Turn Weekly quests on, tick a Garmoth child;
    it moves to the bottom of its group and its parent shows partial. Tick the parent; all three children become done
    and the group moves down. Untick it; the saved order returns. Repeat with Space and check that focus stays on the
    row.
23. To-do: click row text to open the editor. Rename a row, Enter to add, Tab to make a child, Shift+Tab to move it
    out, Backspace on a blank row to remove it, and Alt+arrows or dragging to reorder. Close and reopen; names, order
    and checks survive. Make a new list with +, then close it untouched; it disappears.
24. Settings → To-do: change the daily time and weekly day, time and zone. Existing checks remain. At a reset
    boundary, checks clear even on an Off list; quitting before the boundary and reopening after it also clears them.
    The next reset is shown in local time in each column header.
25. Delete a default To-do list, restore it from Settings and check that its edited rows return. Delete a custom list,
    restart, and confirm it stays deleted.
