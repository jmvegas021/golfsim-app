## Overview

See root README. This document captures the Section 7 spike plan and the Open Connect directionality fix.

## Protocol facts (from GSPro Open Connect v1)

| Direction | Content |
|---|---|
| LM → GSPro | Shot JSON (`BallData`, `ClubData`, heartbeats, ball-detected) |
| GSPro → LM | `200` ack, `201` player info (`Handed`, `Club`), `5xx` errors |

Documented response codes only: **200, 201, 501/5xx**. Outcome events (made putt, water, OB) are **not** documented.

## Why proxy (not client)

Connecting as a second client to `:921` only behaves like another launch monitor. You can *send* shots; you do not *receive* other LM shots. GSPro typically accepts one LM connection.

**Proxy** is the workable tap: LM → app → GSPro, with both directions logged.

## Spike checklist (on Ally with real GSPro)

1. Publish with `./scripts/publish-ally.sh`, copy to Ally
2. Start GSPro, confirm Connect is up on 921
3. Run `GsproLighting.App.exe proxy`
4. Retarget LM bridge to `127.0.0.1:1921`
5. Capture baselines: fairway drive, iron, putt
6. Capture outcomes: water, OB, bunker if distinguishable, made putt, penalty re-drop
7. Inspect `logs/gspro-raw-YYYYMMDD.jsonl`
8. Search for `unknown` keys / unexpected `Code` values
9. Record findings in this file under **Results**

### Offline validation

```bash
dotnet run --project src/GsproLighting.App -- replay
```

Fixture `05-water-spike.json` makes the mock reply with undocumented `Code: 250` + `Outcome` so the logger’s unknown-field detection can be verified without GSPro.

## Results

_Fill in after the Ally evening session._

| Scenario | Unexpected Code? | Unknown JSON keys | Notes |
|---|---|---|---|
| Normal drive | | | |
| Made putt | | | |
| Water | | | |
| OB | | | |
| New hole / shot reset | | | |

## Fallback (if nothing exposed)

Heuristic: same lie / replay-from-spot after a short delay ⇒ likely penalty. Flag as best-effort in v0.4.
