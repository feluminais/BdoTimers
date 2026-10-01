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
- Run the setup again (or Uninstall in Settings → Apps) to see where it's installed and to repair or remove it.
  Uninstall deletes the data, first offering to open the folder if you added sounds or pictures.
- Newer builds upgrade in place, reuse the chosen folder, and close the running app first.
- Silent: `BdoTimers-Setup-<version>.exe /quiet InstallRoot=D:\Games` and `/quiet /uninstall`.

The setup's version is `1.0.<number of commits>`, so each commit you build gets a higher one.

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
`scripts/get-voice.ps1` fetches the voice model (132 MB) that the setup ships.
Manual test checklist: [docs/manual-tests.md](docs/manual-tests.md).

## License
GPL-3.0 (see LICENSE). Bundled components and their licenses are listed in THIRD-PARTY-NOTICES.txt. BDO Timers is not
affiliated with Pearl Abyss; the boss, farm, fishing and horse pictures are Black Desert game artwork © Pearl Abyss and
are not covered by the GPL.
