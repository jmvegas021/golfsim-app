# PRD: GSPro Reactive Lighting App

## 1. Overview
A Windows background app that taps GSPro Open Connect traffic for live shot data and drives a WLED-controlled LED strip to react to shot shape, quality, and outcome in real time. Runs on the ROG Xbox Ally X, on the same machine as GSPro.

## 2. Goals
- React to every shot within a few hundred ms of ball flight data arriving
- Distinguish shot shape (left/center/right, draw/fade/hook/slice) via color and sweep position
- Distinguish shot quality (smash factor, carry) via brightness/intensity
- Distinguish putts from full shots
- React to hole outcomes (made putt, hazard/OB) — research risk, see Section 7
- Zero manual input during play — user only sees lights, GSPro stays the primary display

## 3. Non-goals (v1)
- No support for sims other than GSPro
- No mobile app / remote control (desktop config UI only)
- No cloud sync, accounts, or multiplayer-specific effects
- No course-geometry-aware effects (e.g., "you're near water" before the shot)

## 4. Users
- Single-player practice/play sessions in one bay

## 5. System Architecture

```
┌─────────────┐     TCP/JSON      ┌──────────────────────┐     UDP (WLED)     ┌────────────┐
│ Launch Mon. │ ───(port 1921)──▶ │  Lighting App (Ally)  │ ─────────────────▶ │ WLED strip │
│  (R50/etc)  │                   │  - TCP proxy + logger │                    │ controller │
└─────────────┘                   │  - Effect engine      │                    └────────────┘
                                  │  - Config UI          │
                                  └──────────┬───────────┘
                                             │ forward :921
                                             ▼
                                      ┌─────────────┐
                                      │    GSPro    │
                                      │ Open Connect│
                                      └─────────────┘
```

**Correction:** Open Connect is LM→GSPro for BallData. GSPro only replies with 200/201. A second client on 921 cannot observe shots — this app must **proxy**.

## 6. Features & Effect Mapping
| Trigger | Data source | Effect |
|---|---|---|
| Ball ready | LaunchMonitorBallDetected | Idle glow |
| Pure strike | Smash factor | Green sweep |
| Shot shape | HLA + spin axis | Sweep origin + bend |
| Big shot | Carry | Brighter/faster sweep |
| Mishit | Low smash / short carry | Dull/red, short sweep |
| Putt | Low ball speed | Rolling dot |
| Made putt / hazard | Undocumented (Section 7) | Celebrate / red pulse |
| New hole | Shot number reset | Idle glow |

## 7. Key Risk: Undocumented GSPro Events
Official Open Connect does **not** document water / OB / made putt. Capture via Open Connect **proxy** → `logs/gspro-raw-*.jsonl` on Windows (Ally); export zip and share manually — no cloud/email from the app. See [`docs/SPIKE.md`](SPIKE.md). Wire Celebrate / Hazard live **only** if a stable signal appears; otherwise Preview-only. Do not invent heuristic penalty detection in the current release.

## 8. Tech Stack
.NET 8 (C#) / WinForms on **Windows only** (GSPro is Windows-only). Self-contained win-x64 single-file publish for the Ally X or any GSPro PC.

## 9. Milestones
1. Spike — raw capture
2. v0.1 — parsed shot console log
3. v0.2 — WLED UDP test effect + **tray/settings UI**
4. v0.3 — R50 Connect auto-watch (log/network → feed)
5. v0.4 — Connect ball-metrics → Shot feed + WLED effects ← **current**
6. v0.5 — hazard/made-putt effects
6. v1.0 — packaged installer, autostart

(Config UI was pulled forward from the original v0.5.)

## 10. Open Questions
- Does R50 expose club data (smash factor), or ball-only?
- Multiplayer per-player colors?
- Strip layout: single strip now, surround later?
