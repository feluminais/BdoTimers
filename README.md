# BDO Timers

Tray app for Black Desert Online (EU) world-boss spawns and your own timers.

- Bosses screen: previous, next and following spawn at a glance, over a week grid in your local time
- Custom screen: picture tiles for your own countdowns (buffs, farm sessions) and weekly timers
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert, optional on-screen overlay
- Can start with Windows, minimized to the tray (off until you turn it on in Settings)

## Install
`pwsh scripts/publish.ps1` builds `publish/BdoTimers-Setup-<version>.exe`. Run it; no admin rights needed.

- Install puts BDO Timers in `%LocalAppData%\Programs\BdoTimers`. Options lets you pick another folder; the app always
  gets its own `BdoTimers` folder inside it. Folders that need admin rights (Program Files) won't work.
- Adds a Start Menu shortcut and can launch the app when setup finishes. Start with Windows stays off until you turn
  it on in Settings; uninstall removes it.
- Run the setup again (or Uninstall in Settings → Apps) to repair or remove it. Data in `%AppData%\BdoTimers` is kept.
- Newer builds upgrade in place, reuse the chosen folder, and close the running app first.
- Silent: `BdoTimers-Setup-<version>.exe /quiet InstallRoot=D:\Games` and `/quiet /uninstall`.

Bump `<Version>` in `Directory.Build.props` for each release you hand out.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Priority notifications).
- The overlay only appears over the game in borderless window mode.

## Develop


## Manual test checklist
1. First launch: Bosses screen shows the strip and this week's grid in local time; a "priority notifications" toast appears once.
2. Settings (gear) → Test alert → Send: the alert sound, spoken "Test boss in 5 minutes", urgent toast.
3. Custom → New timer → Countdown: set Duration 0:02 and alerts "1, At spawn"; hover the tile, press play → alert at 1:00
   and at 0:00, then it shows Ready again (or restarts if "Restart when it ends" is on).
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
13. Custom panel → click the picture (badge "Change picture") → Choose picture…: the tile shows it, fading into black.
14. Boss panel → Sound: stepping plays nothing; ▶ plays the choice. + → pick a WAV or MP3: it's selected and listed in
    Settings → Your sounds. A file that isn't audio shows "Couldn't play …".
15. Settings → Your sounds → ✕: the sound is gone; timers that used it show Default.
