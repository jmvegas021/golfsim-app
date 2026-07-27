## Overview

Windows-only spike plan for undocumented GSPro Open Connect outcome events. See root README for install/run.

## Protocol facts (GSPro Open Connect v1)

| Direction | Content |
|---|---|
| LM → GSPro | Shot JSON (`BallData`, `ClubData`, heartbeats, ball-detected) |
| GSPro → LM | `200` ack, `201` player info (`Handed`, `Club`), `5xx` errors |

Documented response codes only: **200, 201, 501/5xx**. Outcome events (made putt, water, OB) are **not** documented.

## Why proxy (not client)

A second TCP client on `:921` behaves like another launch monitor — it does **not** see your R50/LM shots. The lighting app must **proxy**: LM → app → GSPro, logging both directions.

## Spike checklist (Windows PC / Ally X + real GSPro)

1. On Windows, publish or build:
   ```powershell
   .\scripts\publish-ally.ps1
   ```
   Or run from source: `dotnet run --project src\GsproLighting.Ui`
2. Start **GSPro** (Desktop Mode on Ally). Confirm Open Connect is on port **921**.
3. Start **GSPro Lighting** (`GsproLighting.exe`) — proxy should listen on **1921**.
4. Retarget your LM / Garmin bridge to `127.0.0.1:1921`.
5. Capture baselines: fairway drive, iron, putt.
6. Capture outcomes: water, OB, bunker (if distinct), made putt, penalty re-drop.
7. Open `logs\gspro-raw-YYYYMMDD.jsonl` in Notepad or VS Code.
8. Search for `"unknown"` keys / unexpected `Code` values.
9. Record findings under **Results** below.

### Fixture validation (no GSPro / no LM)

From PowerShell in the repo:

```powershell
dotnet run --project src\GsproLighting.App -- replay
```

Fixture `fixtures\shots\05-water-spike.json` makes the mock reply with undocumented `Code: 250` + `Outcome` so unknown-field detection can be verified offline.

## Results

_Fill in after a Windows session with GSPro._

| Scenario | Unexpected Code? | Unknown JSON keys | Notes |
|---|---|---|---|
| Normal drive | | | |
| Made putt | | | |
| Water | | | |
| OB | | | |
| New hole / shot reset | | | |

## Fallback (if nothing exposed)

Heuristic: same lie / replay-from-spot after a short delay ⇒ likely penalty. Flag as best-effort in v0.4.
