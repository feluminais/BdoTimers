# BDO Timers

Tray app for Black Desert Online (EU) world-boss spawns and your own timers.

- Bosses screen: previous, next and following spawn at a glance, over a week grid in your local time
- Timers screen: Farm (22-hour crop countdown) and Fishing (stopwatch) on top, then your own countdowns and weekly timers
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert in a natural offline
  voice (Kokoro), optional on-screen overlay
- Can start with Windows, minimized to the tray (off until you turn it on in Settings)

## Install
`pwsh scripts/publish.ps1` builds `publish/BdoTimers-Setup-<version>.exe`. Run it; no admin rights needed.

- Next shows the install location, `%LocalAppData%\Programs` by default; the app always gets its own `BdoTimers` folder
  inside it, shown in gold after the path. The install is per-user, so a folder that needs admin rights (Program Files) is refused with a note.
- Adds a Start Menu shortcut and can launch the app when setup finishes. Start with Windows stays off until you turn
  it on in Settings; uninstall removes it.
- Everything the app keeps (timers, settings, your sounds and pictures, logs) is in `Data` inside its folder; the app
  warns and exits if it can't write there.
- Run the setup again (or Uninstall in Settings → Apps) to see where it's installed and to repair, move or remove it.
  Moving takes the data along; uninstall deletes it, first offering to open the folder if you added sounds or pictures.
- Newer builds upgrade in place, reuse the chosen folder, and close the running app first.
- Silent: `BdoTimers-Setup-<version>.exe /quiet InstallRoot=D:\Games` and `/quiet /uninstall`.

Bump `<Version>` in `Directory.Build.props` for each release you hand out.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Priority notifications).
- The overlay only appears over the game in borderless window mode.

## Develop
See CLAUDE.md for commands. `scripts/get-voice.ps1` fetches the voice model (132 MB) that the setup ships.

## License
GPL-3.0 (see LICENSE). Bundled components and their licenses are listed in THIRD-PARTY-NOTICES.txt. BDO Timers is not
affiliated with Pearl Abyss; the boss, farm and fishing pictures are Black Desert game artwork © Pearl Abyss and are not
covered by the GPL.

## Manual test checklist
1. First launch: Bosses screen shows the strip and this week's grid in local time; a "priority notifications" toast appears once.
2. Settings (gear) → Test alert → Send: the alert sound, spoken "Test boss in 5 minutes", urgent toast.
3. Timers → New timer → Countdown: set Duration 0:02 and alerts "1, At spawn"; hover the tile, press play → alert at 1:00
   and at 0:00, then it shows Ready again.
4. Start BDO fullscreen, repeat step 3: sound + speech play; toast breaks through.
5. Set the countdown's Overlay to 2 min before, BDO in borderless: overlay appears, clicks pass through to the game.
6. Settings → Position overlay: drag, click "Save overlay position"; restart app; overlay reappears at the saved spot.
   Closing Settings while positioning also saves and restores click-through.
7. Right-click a future boss in the grid → Skip this spawn: struck through, no alert for it; Unskip restores it.
8. Bosses, under the table → click a boss tile → panel: set Alerts to Off → the tile says "Alerts off", the boss dims
   in the grid and leaves the strip.
9. Tray → Pause alerts for 1 hour: "Alerts paused" shows in the top bar; no alerts; Resume clears it.
10. Close window → stays in tray; launch the exe again → existing window comes to front at the same size and position.
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
    Farm → clock → a time over 22 h ago: "Would have ended at …" and Start stays off. The square stops a timer.
