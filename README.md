# BDO Timers

Tray app for Black Desert Online (EU) world-boss spawns and your own timers.

- Bosses screen: previous, next and following spawn at a glance, over a week grid in your local time
- Custom screen: picture tiles for your own countdowns (buffs, farm sessions) and weekly timers
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert, optional on-screen overlay
- Starts with Windows, minimized to the tray

## Install
`pwsh scripts/publish.ps1` builds `publish/BdoTimers-Setup-<version>.msi`. Run it; no admin rights needed.

- Installs for the current user into a `BdoTimers` folder inside the folder you pick (default
  `%LocalAppData%\Programs`). Pick a folder you can write to without admin rights: Program Files won't work.
  Upgrades reuse the chosen folder.
- Adds a Start Menu shortcut and, if the box on the last page is ticked, starts the app.
- Starts with Windows (toggle in Settings). Uninstall from Settings → Apps; that also removes the autostart entry.
- Installing a newer build over an older one upgrades in place and closes the running app first.
- Data lives in `%AppData%\BdoTimers` and is kept on uninstall.

Bump `<Version>` in `Directory.Build.props` for each release you hand out.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Priority notifications).
- The overlay only appears over the game in borderless window mode.

## Develop
See CLAUDE.md for commands.

## Manual test checklist
1. First launch: Bosses screen shows the strip and this week's grid in local time; a "priority notifications" toast appears once.
2. Settings (gear) → Send test alert: chime, spoken "Test boss in 5 minutes", urgent toast.
3. Custom → New timer → Countdown: set Duration 0:02 and alerts "1, At spawn"; hover the tile, press play → alert at 1:00
   and at 0:00, then it shows Ready again (or restarts if "Restart when it ends" is on).
4. Start BDO fullscreen, repeat step 3: sound + speech play; toast breaks through.
5. Set the countdown's Overlay to 2 min before, BDO in borderless: overlay appears, clicks pass through to the game.
6. Settings → Position overlay: drag, click "Save overlay position"; restart app; overlay reappears at the saved spot.
   Closing Settings while positioning also saves and restores click-through.
7. Right-click a future boss in the grid → Skip this spawn: struck through, no alert for it; Unskip restores it.
8. Click a boss name → panel: set Follow to Off → the boss dims in the grid and leaves the strip.
9. Tray → Pause alerts for 1 hour: "Alerts paused" shows in the top bar; no alerts; Resume clears it.
10. Close window → stays in tray; launch the exe again → existing window comes to front at the same size and position.
11. Reboot → app starts minimized in the tray.
12. Start a countdown, quit, wait past its end, relaunch → one "ended while closed" toast.
13. Custom panel → click the picture → Choose picture…: the tile shows it, fading into black.
