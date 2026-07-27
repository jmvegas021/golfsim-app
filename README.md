# GSPro Reactive Lighting

Windows tray app for GSPro. Listens to Open Connect shot traffic and drives a WLED LED strip. Built for the same Windows PC as GSPro (e.g. ROG Xbox Ally X).

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
2. Start **GSPro**
3. **Start proxy** (or leave “Start proxy when app launches” enabled)
4. Point your launch monitor / Garmin→GSPro bridge at **`127.0.0.1:1921`**
5. Minimize to tray and play

Settings save next to the app in `config\appsettings.json`.

## How it connects

GSPro Open Connect is not a broadcast API. GSPro listens on `127.0.0.1:921`; your launch monitor sends `BallData` there. This app sits in the middle:

```
Launch monitor ──▶ :1921 (this app) ──▶ :921 (GSPro)
                 ◀─────────────────────◀
              tapped → feed + lights
```

## Ally X

1. Xbox button → **Desktop Mode**
2. Unzip / run `GsproLighting.exe`
3. Configure WLED + proxy once
4. Return to Xbox full-screen experience — the tray app keeps running

Keep Ally + WLED on the same Wi‑Fi.

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
| `GsproLighting.Gspro` | Proxy, parser, raw logger, fixtures |
| `GsproLighting.Wled` | DRGB UDP output + preview sweeps |

## Roadmap

See [docs/PRD.md](docs/PRD.md). Next: map live shots to effect colors automatically (v0.3).
