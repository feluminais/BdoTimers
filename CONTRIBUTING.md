# Contributing

## Requirements
- Windows 10 or 11, x64. The app is WPF; Core and its tests also build on Linux and macOS.
- .NET SDK 10.0.401 or a later 10.0 band (see `global.json`).
- PowerShell 7.4 for the scripts.

## Layout
- `src/BdoTimers.Core`: models, schedule maths, scheduler, saved data, boss timetables (`Data/`), update check.
  No UI dependencies.
- `src/BdoTimers.App`: the WPF app: screens, view models, alert channels (sound, toast, speech), overlay, tray.
- `tests/BdoTimers.Core.Tests`: xUnit tests for Core, with a fake clock.
- `installer/`: `Msi` (WiX package), `SetupUi` (the setup window, .NET Framework 4.8), `Bundle` (the setup exe).
- `docs/manual-tests.md`: checks unit tests can't reach (overlay, hotkeys,
  sound, setup).
- `lib/sherpa-onnx`: the speech wrapper DLL (see its README). `scripts/`: publish and voice download.

## Commands (repo root)
- `dotnet test tests/BdoTimers.Core.Tests`: Core tests.
- `dotnet build src/BdoTimers.App`: the app. It keeps its data in `Data\` beside its exe (dev build:
  `src\BdoTimers.App\bin\Debug\Data`), never in `%AppData%`. Debug builds have their own single-instance names, so
  they run beside an installed copy.
- `pwsh scripts/get-voice.ps1`: fetches the Kokoro voice model (132 MB, git-ignored, checked by SHA-256). Without it
  the app runs but doesn't speak.
- `pwsh scripts/publish.ps1`: tests, then builds `publish/BdoTimers-Setup-<version>.exe`: app → `installer/Msi` →
  `installer/SetupUi` → `installer/Bundle`. The version is `1.0.<number of commits>`, so it needs full Git history
  (`git fetch --unshallow` in a shallow clone).

## Code rules
- Core has no UI dependencies; all time maths goes through `IClock` and is unit-tested.
- State is immutable records; change it only via `PersistentState<T>.Update` or the store helpers (`TimerStore`,
  `TodoStore`). A new saved field gets a value in `JsonRoundTripTests`' full sample; data that older versions saved
  differently gets a `DataMigrations` or `TodoMigrations` step.
- Alert channels never throw into the scheduler; failures are logged via `Log.Error`.
- The app is GPL-3.0 (sherpa-onnx builds in espeak-ng): every dependency must be GPL-compatible and listed in
  THIRD-PARTY-NOTICES.txt. That is why notifications use Windows' own toast API, not the Windows App SDK.
- `installer/SetupUi` also compiles the app files its csproj links (theme, ArtImage, Ui.cs, Autostart.cs, …) for .NET
  Framework 4.8, so those use only APIs it has.
- Comments state what is and why; no history narration.
- UI copy stays minimal: concise labels and controls, no explanatory paragraphs or helper text that repeats their
  meaning. A short tooltip only for non-obvious details; errors, status and needed confirmations stay concise.
- The in-game overlay stays unobtrusive: no gold outline or bright frame, faint neutral lines at most.
- Commit messages: conventional style (`feat:`, `fix:`, `docs:` …), with a body that says why when it isn't obvious.

## Pull requests
- CI runs the Core tests and builds the app on Windows.
- For overlay, hotkey, sound or setup changes, run the matching steps in `docs/manual-tests.md` and add steps for new
  behaviour.
- Timetable changes: edit the region's JSON in `src/BdoTimers.Core/Data/`, its `verifiedOn` and sources, and
  `docs/boss-region-sources.md`. A new region is a JSON file there plus its entry in `Seed/BossRegions.cs`.

