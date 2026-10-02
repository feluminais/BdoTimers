# BDO Timers

Windows tray app for Black Desert Online (EU and North America): world-boss spawns, your own timers and to-do lists,
with alerts that reach you in game.

- **Bosses**: previous, next and following spawn at a glance, over a week grid in your local time
- **Timers**: Farm growth (through overgrowth to 200%), Fishing stopwatch, Horse registration (up to ten 10-minute
  waits), plus your own countdowns, weekly timers, guild bosses and one-time events
- **To-do**: daily and weekly checklists with child rows that clear at the game's reset
- **Alerts**: sound, urgent Windows notification (gets through gaming Do Not Disturb), a spoken alert in an offline
  voice (Kokoro) and an in-game overlay you can pin, call up with a hotkey or have pop up before guild bosses
- Optional start with Windows, minimized to the tray; local backup and restore

## Download
Get `BdoTimers-Setup-<version>.exe` from [Releases](https://github.com/feluminais/BdoTimers/releases) and run it.
Needs 64-bit Windows 10 or 11; no admin rights and no separate .NET install. The setup isn't code-signed, so SmartScreen
may warn about an unknown publisher (More info → Run anyway).

- Installs for your user only, into `%LocalAppData%\Programs\BdoTimers` unless you choose another folder.
- Everything the app keeps (timers, lists, settings, your sounds and pictures, logs) is in `Data` beside the app.
- Run the setup again, or use Windows Settings → Apps, to repair or uninstall. Uninstall keeps your data unless you
  clear "Keep my timers, lists and settings".
- Silent: `/quiet InstallRoot=D:\Games` installs, `/quiet /uninstall` removes (add `DeleteData=1` to delete data).

## Does it touch the game?
No. BDO Timers never opens the game's process, reads its memory, injects anything or sends keyboard or mouse input.

- The overlay is an ordinary always-on-top, click-through window, so it shows over BDO in borderless window mode only.
- Hotkeys use Windows' standard `RegisterHotKey`, like any other app's shortcuts.
- The only network request is the update check against this repository's GitHub releases. The app never downloads or
  installs anything; it links to the release page.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Notifications → Set priority notifications) so alerts
  get through while you play.
- Overlay settings are behind the screen icon in the top bar or in the tray menu; drag the overlay while the panel is
  open.
- Default shortcuts: Ctrl+Shift+F8 pins the overlay, Ctrl+Shift+F9 shows it for 10 seconds and Ctrl+Shift+F10 starts a
  Horse registration timer. Change or clear them in Overlay settings and on the Horse registration tile. A countdown can
  have its own start/pause hotkey, set in its panel.
- Settings → Bosses → Region switches between Europe (`Europe/Berlin`, the default) and North America
  (`America/Los_Angeles`). Each region keeps its own alert settings and edited spawn times.
  [Timetable sources](docs/boss-region-sources.md).
- Timers → New timer → Guild boss adds a weekly timer for your guild's summons. Overlay → Guild bosses sets how long
  before each one the overlay pops up (Off, or 5 to 60 minutes).
- When a release changes spawn times, Settings → Bosses lets you review the changes before they apply.
- Settings → Data exports or restores a backup ZIP of your timers, lists, settings, sounds and pictures.
- Settings → About → Check for updates. Release builds also check once a day at startup and show a green icon in the
  top bar when a newer version is out.

## Build from source
See [CONTRIBUTING.md](CONTRIBUTING.md).

## License
GPL-3.0 (see LICENSE). Bundled components and their licenses are listed in THIRD-PARTY-NOTICES.txt. BDO Timers is not
affiliated with Pearl Abyss; the boss, farm, fishing and horse pictures are Black Desert game artwork © Pearl Abyss and
are not covered by the GPL.
