# Release verification

## Build gates

Run the Core tests, Windows UI smoke tests and app build on a clean Windows checkout. The GitHub Windows workflow runs these gates for pushes and pull requests; it uploads test results without publishing an installer.

Run Windows UI tests with `pwsh scripts/test-windows.ps1 -Configuration Release`. The runner uses an undisplayed Windows desktop so test windows and focus stay separate from the active desktop. It does not send keyboard or mouse input.

Use [manual-tests.md](manual-tests.md) as the single release checklist. Record the tested commit, Windows version, display configuration and results. Unchecked items have not been verified.

## Signed releases

Install Windows SDK SignTool and place a trusted code-signing certificate with its private key in the current user's `My` certificate store. Keep private keys and signing credentials outside Git.

```powershell
$env:BDOTIMERS_SIGNING_THUMBPRINT = '<certificate thumbprint>'
pwsh scripts/publish.ps1
```

The pipeline signs the app executable and owned assemblies, the setup UI and MSI, then both the detached Burn engine and complete bundle. Every signature is verified and timestamped with SHA-256. The setup gets a SHA-256 checksum alongside it. Failed signing prevents release output.

The Burn signing sequence follows [WiX's signing guidance](https://docs.firegiant.com/wix/tools/signing/). Certificate selection and timestamping use [Windows SignTool](https://learn.microsoft.com/en-us/windows/win32/seccrypto/signtool).

For local testing before a signing identity is available:

```powershell
pwsh scripts/publish.ps1 -AllowUnsigned
```

This explicitly produces an unsigned test setup. It does not establish a trusted publisher. Public release signing has not been verified until a trusted signing identity is configured and the resulting setup is checked on a clean Windows machine.

## Performance

```powershell
pwsh scripts/measure-performance.ps1
pwsh scripts/measure-performance.ps1 -Quick
```

The script measures a fresh dev copy with its own `Data` under `bin/Debug/performance/<run>/app`. It suppresses startup notices and update checks, uses a separate single-instance name, and synthesizes a short phrase without playing it. Reports in `obj/performance` contain startup readiness, a one-second CPU sample, working set/private memory and first speech readiness. Full runs wait 190 seconds after synthesis to observe the voice model's idle timeout. Quick runs skip that wait. Each run has an empty speech cache; the report identifies whether speech was generated or cached.

These measurements describe the tray process and speech generation, not window rendering, audible latency or game performance. Settings → Copy diagnostics provides the same local metrics and last known problem; it copies only on request and sends nothing over the network.

Initial investigation targets on a typical desktop: tray ready within 2 seconds, idle CPU below 0.5% of the whole machine, and generated speech ready within 5 seconds. Treat them as investigation thresholds, not universal pass/fail promises. Record hardware, power mode, build configuration and cache state when comparing runs. Investigate growth that persists after the voice idle timeout and regressions between equivalent runs.

### Local baseline — 2 October 2026

A fresh Debug copy on Windows 10.0.26200.0 with .NET 10.0.12 produced these measurements. Speech was generated with an empty cache; hardware and power mode were not recorded, so this is a local observation rather than a portable benchmark.

| Measurement | Result |
| --- | ---: |
| Tray readiness | 897 ms |
| Idle CPU, one second, whole machine | 0.06% |
| Idle working set / private memory | 130.2 / 64.5 MB |
| First generated speech readiness | 2,591 ms |
| After speech working set / private memory | 353.8 / 297.4 MB |
| After 190 seconds working set / private memory | 188.1 / 95.6 MB |

The report recorded no app faults. The voice idle timeout released most of its added private memory; the working set remained above startup. Keep the full report locally under `obj/performance` and compare equivalent runs before treating retained memory as a leak.
