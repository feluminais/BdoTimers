# BDO Timers

Tray app for Black Desert Online (EU) world-boss spawns and your own timers.

- Built-in EU boss timetable (editable; Settings → Reset boss timetable restores it)
- Scheduled timers (weekly times) and countdowns (farm sessions, buffs), optional auto-repeat
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert, optional on-screen overlay
- Starts with Windows, minimized to the tray

## Install
`pwsh scripts/publish.ps1` produces a self-contained `publish/` folder (not a single exe: the Windows App SDK
rejects single-file publish without MSIX tooling). Zip the whole folder to move it to another machine, extract it
somewhere permanent, and keep all files next to `BdoTimers.exe`.

Run `BdoTimers.exe` once. It registers itself to start with Windows (toggle in Settings).
Data lives in `%AppData%\BdoTimers`.

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
