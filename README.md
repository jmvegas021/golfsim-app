# GSPro Reactive Lighting

Windows tray app for GSPro. Auto-watches **Garmin R50 → GSPro Connect** logs/network traffic and drives a WLED LED strip. Also supports an optional Open Connect TCP proxy for fixtures / bridged LMs. Built for the same Windows PC as GSPro (e.g. ROG Xbox Ally X).

**GSPro is Windows-only — this app is too.**

## Easiest way to run (no build)

> **Do not use the green “Code → Download ZIP” button** if you just want to run the app. That ZIP is source code and needs a build.

1. Open **[Releases](https://github.com/jmvegas021/golfsim-app/releases)**
2. Download **`GsproLighting-windows-x64.zip`** (under Assets)
3. Unzip anywhere (Desktop, Documents, USB, Ally…)
4. **Double-click `GsproLighting.exe`**

That’s it. A tray icon appears; double-click it (or use the menu) for settings.

Optional: pin `GsproLighting.exe` to the taskbar or drop a shortcut in `shell:startup` so it launches with Windows.

## If you cloned / downloaded source

You need either:
- The **Release ZIP** above (recommended), or
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
4. Hit balls — the live feed should show `[LOG]` / `[NET]` / shot lines within ~1s
5. Minimize to tray and play

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
2. Unzip / run `GsproLighting.exe`
3. Configure WLED once; leave auto-watch on
4. Start GSPro + Connect, hit balls, watch the live feed
5. Return to Xbox full-screen experience — the tray app keeps running

Keep Ally + WLED (+ R50 hotspot/LAN) on the same network path you normally use for Connect.

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
