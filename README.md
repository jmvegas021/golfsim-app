# GSPro Reactive Lighting

Windows tray app that taps GSPro Open Connect shot traffic and drives a WLED LED strip. Targets the ROG Xbox Ally X running GSPro.

## Current milestone

**Tray UI + settings + WLED preview** (v0.2 foundation pulled forward). Full shot→effect engine still next.

## What you get in the GUI

- System tray icon (double-click or menu → Open settings)
- **Live shot feed** — ball ready, shots, player/ack messages as they arrive
- **Preferences** — WLED IP/port/LED count/brightness, proxy ports, effect colors, putt/smash thresholds
- **Test lights** / **Idle glow** — UDP preview to the strip without hitting a ball
- Start/stop proxy from the window or tray menu

Settings persist to `config/appsettings.json`.

## Architecture note

GSPro Open Connect is **not** a broadcast API. This app is a **bidirectional proxy**:

```
Launch monitor ──▶ :1921 (this app) ──▶ :921 (GSPro)
                 ◀─────────────────────◀
              tapped → feed + (soon) lights
```

## Quick start (Mac / offline console)

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet run --project src/GsproLighting.App -- replay
```

## Publish for Ally X

```bash
./scripts/publish-ally.sh
```

Then on the Ally:

1. Run `GsproLighting.exe` (tray app)
2. Set your WLED IP → **Test lights**
3. Start GSPro, point LM bridge at `127.0.0.1:1921`
4. Watch the live feed; leave the window minimized to tray while playing

Console spike tools are also published under `dist/ally/console/`.

## Project layout

| Project | Role |
|---|---|
| `GsproLighting.Core` | Models, config store, shot feed |
| `GsproLighting.Gspro` | Proxy, parser, raw logger, mock/replay |
| `GsproLighting.Wled` | DRGB UDP output + preview sweeps |
| `GsproLighting.Ui` | WinForms tray + settings (Ally app) |
| `GsproLighting.App` | Console host for Mac spike / replay |

## Roadmap

See [docs/PRD.md](docs/PRD.md). Next: map live shots to effect colors automatically (v0.3).
