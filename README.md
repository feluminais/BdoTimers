# BDO Timers

Tray app for Black Desert Online (EU) world-boss spawns and your own timers.

- Built-in EU boss timetable (editable; Settings → Reset boss timetable restores it)
- Scheduled timers (weekly times) and countdowns (farm sessions, buffs), optional auto-repeat
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert, optional on-screen overlay
- Starts with Windows, minimized to the tray

## Install
`pwsh scripts/publish.ps1` builds `publish/BdoTimers-Setup-<version>.msi`. Run it; no admin rights needed.

- Installs for the current user; the wizard lets you pick the folder (default `%LocalAppData%\Programs\BdoTimers`).
  Pick a folder you can write to without admin rights: Program Files won't work. Upgrades reuse the chosen folder.
- Adds a Start Menu shortcut and, if the box on the last page is ticked, starts the app.
- Starts with Windows (toggle in Settings). Uninstall from Settings → Apps; that also removes the autostart entry.
- Installing a newer build over an older one upgrades in place and closes the running app first.
- Data lives in `%AppData%\BdoTimers` and is kept on uninstall.

Bump `<Version>` in `Directory.Build.props` for each release you hand out.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Open Windows notification settings).
- The overlay only appears over the game in borderless window mode.

## Develop


## Manual test checklist
1. First launch: main window opens, Upcoming lists bosses, a "priority notifications" toast appears once.
2. Settings → Send test alert: chime, spoken "Test boss in 5 minutes", urgent toast.
3. Add a 2-minute countdown with alerts "1, 0": Start → alert at 1:00 and at 0:00, then it resets (or restarts if auto-repeat).
4. Start BDO fullscreen, repeat step 3: sound + speech play; toast breaks through.
5. Enable overlay on a countdown (show 2 min before), BDO in borderless: overlay appears, clicks pass through to the game.
6. Settings → Position overlay: drag, click again; restart app; overlay reappears at the saved spot.
7. Skip an occurrence on Upcoming: no alert for it.
8. Tray → Pause alerts for 1 hour: banner shows; no alerts; Resume clears it.
9. Close window → stays in tray; launch exe again → existing window comes to front.
10. Reboot → app starts minimized in the tray.
11. Start a countdown, quit, wait past its end, relaunch → one "ended while closed" toast.
