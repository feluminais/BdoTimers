# BDO Timers

Tray app for Black Desert Online (EU and North America) world-boss spawns and your own timers.

- Bosses screen: previous, next and following spawn at a glance, over a week grid in your local time
- Timers screen: Farm (22-hour growth estimate that continues through overgrowth to 200%), Fishing (stopwatch),
  Horse registration (up to ten independent 10-minute waits from the game's registration notice to sale),
  Guild bosses (one weekly time) and Guild war (multiple weekly times), then your own countdowns, weekly timers and
  dated one-time events. Set the guild times yourself.
- To-do screen: daily and weekly checklists with child rows, shared reset times and your own lists
- Alerts: sound, urgent Windows notification (gets through gaming Do Not Disturb), spoken alert in a natural offline
  voice (Kokoro), an in-game overlay you can pin or call up with a hotkey (clock, bosses, farm, fishing, horse registrations,
  custom countdowns and upcoming one-time events)
- Can start with Windows, minimized to the tray (off until you turn it on in Settings)
- Local backup and restore, and a review of timetable changes included in newer releases

## Install
`pwsh scripts/publish.ps1 -CertificateThumbprint <thumbprint>` builds `publish/BdoTimers-Setup-<version>.exe`. Run it; no admin rights needed.

- Next shows the install location, `%LocalAppData%\Programs` by default; the app always gets its own `BdoTimers`
  folder inside it. The install is per-user, so a folder that needs admin rights (Program Files) is refused.
- Adds a Start Menu shortcut and can launch the app when setup finishes.
- Everything the app keeps (timers, to-do lists, settings, your sounds and pictures, logs) is in `Data` inside its
  folder; the app warns and exits if it can't write there.
- Run the setup again (or Uninstall in Settings → Apps) to see where it's installed and to repair or remove it.
  Uninstall keeps your data by default. Clear "Keep my timers, lists and settings" to delete it, including the recovery
  copies made by backup restore. Reinstall into the same folder to use the kept data.
- Newer builds upgrade in place, reuse the chosen folder, and close the running app first.
- To change the install location, export a backup first and restore it in the new installation, or close the app
  and copy the existing `Data` folder into the new `BdoTimers` folder. Setup does not move personal data.
- Silent: `BdoTimers-Setup-<version>.exe /quiet InstallRoot=D:\Games` and `/quiet /uninstall`.
  Quiet uninstall keeps data; `/quiet /uninstall DeleteData=1` explicitly deletes it.

The setup's version is `1.0.<number of commits>`. Publishing requires full Git history; each descendant commit gets
a higher version. For a shallow clone, run `git fetch --unshallow` first.

Settings → About → Check for updates checks the [official GitHub releases](https://github.com/feluminais/BdoTimers/releases).
It shows Checking, Up to date, Update available (with the version and View release), or Couldn’t check.
View release opens GitHub so you can download and run the setup yourself. The app never downloads or installs updates.
Release builds also check in the background on startup, at most once per 24 hours. A newer stable release shows a green
update icon before Overlay settings in the top bar, with no Windows update notification. Click it to see a small dialog
over the app with the version, official release URL, Open GitHub and instructions to download the .exe to update.
Failed and manual attempts count toward the daily limit, saved in
`Data/update-check.json`. Drafts, prereleases, equal versions and older versions are ignored. Offline or failed checks
leave timers running, time out after ten seconds, and can be retried manually. Debug builds only check on request.

## Tips
- Add BDO Timers to Windows priority notifications (Settings → Priority notifications).
- The overlay only appears over the game in borderless window mode.
- Overlay settings are behind the screen icon in the top bar or in the tray menu; drag the overlay while the panel is open.
- Default shortcuts: Ctrl+Shift+F8 pins the overlay, Ctrl+Shift+F9 shows it for 10 seconds, and Ctrl+Shift+F10 starts a
  Horse registration timer. They work while the app is in the tray. Change or clear them in Overlay settings and the
  Horse registration tile if they conflict with your game bindings. Existing saved shortcuts stay as set.
- Custom countdowns have an optional hotkey in their timer panel: Ready → Start, Running → Pause, Paused → Resume.
  It works with BDO focused and the app in the tray, including with Alerts off or the overlay off. Reset stays on the
  tile. Change or clear the hotkey in the panel; shortcuts already assigned anywhere in BDO Timers are refused.
  Tray → Custom timers also offers Start, Pause or Resume. Overlay → Sections → Custom timers shows running and
  paused countdowns in creation order, with paused times marked “Paused”.
- Timers → New timer → One-time event: set the name, date (`YYYY-MM-DD`), time (`HH:mm`) and time zone, then choose
  alert channels and lead times. The event appears on the Timers screen and in the overlay's Custom timers section;
  an enabled Overlay alert also brings it into pop-ups. It stays visible as Finished after its occurrence and never
  repeats. Edit its date, time or time zone to a future occurrence to use it again, or delete it. Reopening after a
  missed event shows one concise missed-event notice, with no replayed lead alerts or notices on later launches.
- Weekly timers have optional Start date and End date in their own time zone; blank means no limit. Both dates are
  inclusive, even when the occurrence falls on a different date in your local time. After the last allowed occurrence,
  the timer shows Expired and stops scheduling. An end before the start is refused. Events and weekly timers use the
  same DST rules: spring gaps shift forward and an autumn repeated time uses its first instance.
- To-do lists start Off. Turn one on, tick its boxes, and click its text to edit. Use + by Weekly or Daily to make a list;
  Settings controls the reset time for all lists of that kind and can restore deleted default lists.
- Settings → Data → Export backup saves timers, lists, settings, sounds and pictures in one ZIP outside `Data`.
  Restore backup checks the file before asking to replace current data and restart. The previous folder is kept beside
  `Data` as `Data.before-restore-<timestamp>-<id>`. Running timers and checklist resets reconcile with the current time.
- Settings → Bosses → Region chooses Europe or North America for boss screens, the overlay and boss alerts. Each
  region keeps its own Alerts on/off choices, alert settings, edited spawn times, skipped spawns and accepted timetable.
  Switching back restores them; existing installations start in EU with their saved configuration. Custom timers keep
  their schedules and time zones, and to-do checks and reset schedules stay as saved. Already-due boss alerts and
  overlay pop-ups are skipped when switching; future alert leads still fire.
- Settings → Bosses shows the selected timetable's verification date (source URLs in its tooltip). When its spawn times
  change, review the added, removed and changed bosses, then Apply selected or Keep current times. Custom spawn times
  are marked and Off initially. Alert settings and the timers you created are kept. Timetable updates arrive with app
  releases and do not require an online feed.
- NA uses Pacific time (`America/Los_Angeles`, PST/PDT); EU uses `Europe/Berlin` (CET/CEST). Their DST transitions
  occur on different dates. The bundles represent the regular weekly timetable. Short server-maintenance shifts around
  DST are not applied automatically; see [verified sources and exceptions](docs/boss-region-sources.md).
- Open the Horse registration tile to change its start hotkey. Each press starts a separate timer and speaks
  “Horse registration time started”. The overlay's Sections include horse registrations; it shows the newest two and
  counts any others that are running.

## Develop
Core and WPF smoke tests also run in Windows CI. Build and tests do not require the voice
model. For signing and explicit unsigned local builds, see [docs/releasing.md](docs/releasing.md).
`scripts/get-voice.ps1` fetches the voice model (132 MB) that the setup ships.
Manual test checklist: [docs/manual-tests.md](docs/manual-tests.md).

## License
GPL-3.0 (see LICENSE). Bundled components and their licenses are listed in THIRD-PARTY-NOTICES.txt. BDO Timers is not
affiliated with Pearl Abyss; the boss, farm, fishing, horse and guild pictures are Black Desert game artwork © Pearl Abyss and
are not covered by the GPL.
