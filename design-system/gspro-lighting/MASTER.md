# Design System Master — GSPro Lighting

> WinForms night-bay sports-tech utility. Overrides auto-search defaults for product fit.

**Project:** GSPro Lighting  
**Product:** Golf simulator bay lighting control (desktop / Ally X)  
**Mood:** Premium night bay · sports-tech · OLED-friendly

---

## Pattern

Modular settings dashboard with dense, scannable surfaces (bento-density). Hero brand in chrome header. Preview tab as the primary testing lab after Effects.

## Effects & lighting authorship

- Lighting colors are **product-authored defaults**, not a user palette
- Effects tab: live status + runtime actions + read-only **EffectStateLegend**
- **Preview** tab is the testing lab (on-screen + WLED, does not Save)
- No per-phase color editors / ColorDialog swatch cards

## Style

**Dark OLED sports-tech** — charcoal greens, amber accent, status green. Depth via gradients, subtle borders, hover/focus — not flat gray chrome, not cyberpunk neon purple.

### Avoid
- AI purple / indigo gradients
- Cream + terracotta cliché
- Broadsheet / newspaper dense columns
- Emoji icons
- Flat design without depth
- Stock Windows gray chrome

---

## Color Palette (night bay)

| Role | Hex | Notes |
|------|-----|-------|
| Background | `#0B100E` | Deep charcoal green |
| Background mid | `#101714` | Gradient stop |
| Panel | `#161F1A` | Cards / surfaces |
| Panel raised | `#1C2821` | Hover / selected |
| Border | `#2A3A32` | Quiet edges |
| Border focus | `#D4A017` | Amber focus ring |
| Text | `#F1F5F2` | Primary (≥7:1 on bg) |
| Muted | `#9AABA2` | Secondary labels |
| Accent / CTA | `#D4A017` | Amber gold |
| Accent hover | `#E6B42A` | |
| Ready | `#3DDC84` | Status green |
| Not ready | `#E5533D` | Alert red |
| Console | `#070A08` | LED / feed wells |
| Waiting | `#B47814` | Amber dim |

## Typography (WinForms)

| Role | Family | Fallback |
|------|--------|----------|
| Heading | Bahnschrift SemiBold Condensed | Segoe UI Variable Display, Segoe UI |
| Body | Segoe UI Variable | Segoe UI |
| Mono | Cascadia Mono | Consolas |

Sports condensed headings (Barlow Condensed intent) → Bahnschrift on Windows.

## Spacing

`4 / 8 / 12 / 16 / 24 / 32` — Ally-friendly; avoid large empty voids. Section rhythm 16–24px.

## Interaction

- Touch targets ≥ 44×44px
- Hover/focus 150–200ms feel (timer-based invalidate)
- Visible amber focus rings
- Cursor hand on all clickables
- No emoji icons — geometric painted marks only
- Respect reduced motion when feasible (shorter animation frames)

## Components

- **Header:** Brand-hero GSPro Lighting + amber→green stripe
- **Tabs:** Owner-draw, 44px tall, amber underline when selected
- **Cards:** Gradient panel fill, 1px border, amber on hover/selected
- **Buttons:** Owner-draw primary amber / secondary panel
- **Chips:** Pill status with high-contrast fill
- **LED preview:** Console well, glowing pixels, hold-after-play
- **Inputs:** Dark panels, amber focus border

## Preview tab behavior

- Hold end color after animation until another state or Stop
- Stop → hold ready/idle green (not clear-to-black)
- Never call global Save from preview
