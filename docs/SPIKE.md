## Overview

Windows-only capture spike for undocumented GSPro Open Connect **outcome** events (water, OB, made putt). Mac cannot run real GSPro — use this doc as the process for an Ally X / Windows session, then fill **Results** later.

See root README for install/run. Product context: [PRD §7](PRD.md).

## API reality (locked)

Official docs: [GSPro Open Connect v1](https://gsprogolf.com/GSProConnectV1.html).

| Direction | Documented content |
|---|---|
| LM → GSPro | Shot JSON (`BallData`, optional `ClubData`, ready / ball-detected / heartbeat flags) |
| GSPro → LM | Response codes **200** (OK), **201** (player info), **501/5xx** (failure) |

**Not documented** (and not publicly specified): water, out of bounds, bunker, made putt, new hole, lie after settle, or any dedicated “outcome” channel.

Docs say more response codes “will be added.” Until capture proves otherwise, treat Celebrate / Hazard as **research**, not guaranteed live triggers.

## Why Open Connect proxy (required for this spike)

| Path | What you get | Outcome packets? |
|---|---|---|
| Native R50 → Connect log tail | Ball metrics, ready / not-ready (`r50-log-*.jsonl`) | **No** — no GSPro→LM reply codes |
| Open Connect **proxy** LM→`:1921`→GSPro `:921` | Both-direction TCP JSON (`gspro-raw-*.jsonl`) | **Maybe** — only place undocumented codes/fields would show |

A second TCP client on `:921` acts like another launch monitor; it does **not** see your R50/LM shots. The lighting app must **proxy**.

**Spike note:** Water/OB/made-putt signals (if any) only appear in `gspro-raw-YYYYMMDD.jsonl` when traffic goes through the proxy.

## Log files (local only)

Everything is next to the install/exe — **no cloud upload, no email send from the app**.

| File | When | Contents |
|---|---|---|
| `{install}/logs/gspro-raw-YYYYMMDD.jsonl` | Open Connect **proxy** running | Both-direction TCP JSON, response `code`, `unknown` extension fields |
| `{install}/logs/r50-log-YYYYMMDD.jsonl` | R50 Connect log tail | Interesting Connect lines (not sufficient for outcome spike) |
| `{install}/crash.log` | Unhandled errors | Exception dumps |

Heartbeats are skipped unless `Logging.LogHeartbeats` is true.

### How to view / share

1. **Live feed** tab — readable `[Shot]` / `[Ready]` lines while playing (not a full raw dump).
2. **Open `gspro-raw-YYYYMMDD.jsonl`** in VS Code / Notepad++ / Notepad — search `"code"`, `"unknown"`, `"Outcome"`, etc. Append-only JSONL (one object per line).
3. **Export logs…** (Live feed) — zip recent `gspro-raw-*`, `r50-*`, and `crash.log` → **you** attach that zip in email/Discord yourself.
4. **Open logs folder** — Explorer to the install `logs\` directory.

No SMTP, Gist, auto-send, or cloud sync.

## Ally / Windows checklist

Do this on the **same Windows PC as GSPro** (e.g. ROG Ally X Desktop Mode). Cannot be completed on macOS.

1. **Publish or build** (on Windows):
   ```powershell
   .\scripts\publish-ally.ps1
   ```
   Or: `dotnet run --project src\GsproLighting.Ui`
2. **Start GSPro** — confirm Open Connect listens on port **921**.
3. **Start GSPro Lighting** — ensure Open Connect **proxy** is enabled and listening on **1921**.
4. **Retarget LM / Garmin bridge** (or any Open Connect client under test) to `127.0.0.1:1921` (not `:921` directly).
5. **Play scenarios** (one session, note approximate times):
   - Fairway drive (baseline)
   - Iron / normal approach
   - **Water**
   - **OB**
   - **Made putt**
   - Bunker or penalty re-drop if distinct and easy to trigger
6. **Inspect** `{install}\logs\gspro-raw-YYYYMMDD.jsonl`:
   - Unexpected `Code` values (not 200 / 201 / 5xx)
   - Unknown JSON keys (parser extracts extensions into `unknown` / Extensions)
7. **Export zip** from Live feed (or copy the `logs\` folder) and share **manually** if review is off the Ally.
8. **Fill the Results table** below (same session or after reviewing the zip).

### Fixture validation (no GSPro / no LM)

Offline check that unknown-field capture still works — does **not** replace the Windows GSPro spike:

```powershell
dotnet run --project src\GsproLighting.App -- replay
```

Fixture `fixtures\shots\05-water-spike.json` makes the mock reply with undocumented `Code: 250` + `Outcome`.

## Results

_Status: waiting on a real Windows + GSPro capture session. Leave cells blank until filled._

| Scenario | Unexpected Code? | Unknown JSON keys | Notes |
|---|---|---|---|
| Normal drive | | | |
| Made putt | | | |
| Water | | | |
| OB | | | |
| Bunker / penalty re-drop | | | |
| New hole / shot reset | | | |

**Capture date / build / notes:** _(fill after session)_

## Celebrate / Hazard decision (after Results)

| Finding | Action |
|---|---|
| Stable, reliable `Code` or JSON field for celebrate / hazard | Live-wire Celebrate / Hazard slots in the WLED sink |
| Nothing useful in `gspro-raw-*.jsonl` | Keep Celebrate / Hazard as **Preview-only** for this release |

Do **not** invent heuristic “must have been a penalty” detection in this release (keeps scope honest). Heuristic fallback remains a later / optional idea only if product revisits PRD risk.

### Doc/process status (`outcome-spike`)

- **Done (this repo):** spike process, Ally checklist, log/export rules, Results table, Celebrate/Hazard gate.
- **Blocked until Windows:** real proxy capture + filled Results.
- **Blocked until Results:** live wiring of Celebrate/Hazard (another agent may ship Preview-only UI/effects meanwhile).
