# GSPro Reactive Lighting

Windows tray app for GSPro. Auto-watches **Garmin R50 → GSPro Connect** logs/network traffic and drives a WLED LED strip. Also supports an optional Open Connect TCP proxy for fixtures / bridged LMs. Built for the same Windows PC as GSPro (e.g. ROG Xbox Ally X).

**GSPro is Windows-only — this app is too.**

## Easiest way to run (no build)

> **Do not use the green “Code → Download ZIP” button** if you just want to run the app. That ZIP is source code and needs a build.

### Recommended (auto-updates via Velopack)

1. Open **[Releases](https://github.com/jmvegas021/golfsim-app/releases)**
2. Download **`GsproLighting-win-Setup.exe`**
3. Run Setup once (installs under your user profile)
4. Launch **GSPro Lighting** — tray icon appears

Later updates: **Settings → Updates → Check for updates**, or tray **Check for updates…**. When ready, **Install update & restart**.

See [SETUP-UPDATES.txt](SETUP-UPDATES.txt) for Ally-oriented steps and how developers cut releases.

### Portable zip (still supported)

1. Download **`GsproLighting-windows-x64.zip`**
2. Unzip anywhere → double-click **`GsproLighting.exe`**
3. Settings → Updates still works (zip-based updater)

Optional: pin the app to the taskbar or drop a shortcut in `shell:startup`.

## If you cloned / downloaded source

You need either:
- The **Setup.exe** / Release ZIP above (recommended), or
- The [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

On Windows, double-click **`Run GSPro Lighting.bat`**. If no `.exe` is built yet and no SDK is installed, it opens the Releases page for you.

Or from PowerShell (SDK required):

```powershell
.\scripts\publish-ally.ps1
.\dist\ally\GsproLighting.exe
```

## First-time setup (once)

1. Set your **WLED IP** → **Test lights**
2. Start **GSPro** + **GSPro Connect** (Garmin R50) as usual
3. Start **GSPro Lighting** — **R50 auto-watch is on by default** (no AppData digging, no port retarget for native Connect)
4. Hit balls — Connect log ball metrics become clean `[Shot]` / `[Putt]` / `[Ready]` feed lines and drive WLED (pure / mishit / putt / ready glow)
5. Minimize to tray and play

**R50 Connect logs → lights:** the app tails GarminR50Form lines (`readyForShot`, `Logging ball data IMMEDIATELY…`, carry/sidespin JSON — including multiline payloads) and maps them to `[Shot]` / `[Putt]` / `[Ready]` plus your Effect colors. Ball-marker `[LOG]` lines stay visible; `[NET]` peer keepalives are quiet.

Status text shows discovered Connect logs and any R50 peer (`Watching: N log files · R50 peer …`). Raw captures land in `logs\`.

Settings save next to the app in `config\appsettings.json`.

### Optional: Open Connect proxy

Only needed if you use an LM/bridge that speaks Open Connect TCP (or fixture replay). Point that client at **`127.0.0.1:1921`**. Native R50 → Connect v1.8.8 does **not** use this path.

## How it connects

**Native R50 (default):**

```
Garmin R50 ──Wi‑Fi──▶ GSPro Connect v1.8.8 ──▶ GSPro
                         │
                         ├─ AppData logs  ──tail──▶ live feed + WLED
                         └─ TCP peers     ──watch─▶ live feed (+ limited net capture)
```

**Open Connect proxy (optional):**

```
Launch monitor ──▶ :1921 (this app) ──▶ :921 (GSPro)
                 ◀─────────────────────◀
              tapped → feed + lights
```

## Ally X + R50

1. Xbox button → **Desktop Mode**
2. Install via **Setup.exe** from Releases (preferred) or unzip the portable ZIP
3. Configure WLED once; leave auto-watch on
4. Start GSPro + Connect, hit balls — expect `[Ready]` then `[Shot]`/`[Putt]` and matching WLED flashes
5. Use **Settings → Updates** (or tray **Check for updates…**) when a new release ships
6. Return to Xbox full-screen experience — the tray app keeps running

Keep Ally + WLED (+ R50 hotspot/LAN) on the same network path you normally use for Connect.

## Auto-updates (developers)

Version is centralized in `Directory.Build.props` (`0.4.3`, etc.).

```bash
# macOS host → win-x64 + Velopack
export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
./scripts/package-release.sh 0.4.3
./scripts/publish-github-release.sh 0.4.3
```

App update feed: `https://github.com/jmvegas021/golfsim-app` (Velopack `GithubSource` → public `releases.win.json`). The repo must stay **public** so the updater can download release assets without a token. Portable installs fall back to downloading `GsproLighting-windows-x64.zip`.

## Console tools (optional debugging)

```powershell
dotnet run --project src\GsproLighting.App -- proxy
dotnet run --project src\GsproLighting.App -- replay
```

`replay` injects fixture shots with no GSPro/LM required.

## Project layout

| Project | Role |
|---|---|
| `GsproLighting.Ui` | WinForms tray + settings (**main app**) |
| `GsproLighting.App` | Console host (`proxy` / `mock` / `replay`) |
| `GsproLighting.Core` | Models, config store, shot feed |
| `GsproLighting.Gspro` | Proxy, Connect discovery/watchers, parsers |
| `GsproLighting.Wled` | DRGB UDP output + shot effect sink |

## Roadmap

See [docs/PRD.md](docs/PRD.md).
