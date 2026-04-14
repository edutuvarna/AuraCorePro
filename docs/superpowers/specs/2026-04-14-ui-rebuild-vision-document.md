# AuraCorePro UI Rebuild — Vision Document

**Date:** 2026-04-14
**Status:** Approved
**Scope:** North Star document for the full UI rebuild. Captures philosophy, design tokens, principles, and navigation model. Short and stable — implementation details belong to phase specs.

---

## 1. Purpose

The UI rebuild aligns AuraCorePro with 2026 design trends, neutralizes the "consumer optimization tool" feel by introducing a named AI identity (CORTEX), and reorganizes 26 modules into 5-6 job-based categories. The outcome is a cross-platform desktop app that feels modern, intelligent, and approachable to new users while keeping power-user depth accessible.

This document is the **North Star** — it defines *what* we are building and *why*. Each phase spec defines *exactly how*.

---

## 2. Design Philosophy

### Target user

**Hybrid** — Dashboard simple and hero-focused for casual users, "Advanced" section exposes power-user tools. Surface area reveals itself progressively.

### Visual direction

**Modern Glassmorphism, cross-platform.**

- Subtle translucent surfaces (backdrop blur), soft radial glow in dark theme, neon accent color usage.
- Windows: Mica backdrop via platform integration.
- macOS: Vibrancy blur via platform integration.
- Linux: Solid neutral dark (#0A0A10) with gradient fallback.
- Neutral dark base — no blue tint. `#0A0A10` / `#0E0E14` / `#12121A` surfaces.
- Soft bloom on accent colors for active states (animated on scan/optimize completion).

### Tone

Intelligent, quiet, competent. Not "gamer neon", not "consumer antivirus". The AI is present but not boastful. Named product (CORTEX) earns trust through visible pattern-learning and predictions, not marketing copy.

---

## 3. Design Principles (the rules)

1. **Gauges over numbers** — live visual state trumps static tiles. Every primary metric (CPU, RAM, GPU, Disk, Health) is a circular gauge.
2. **Every dashboard has a hero CTA** — one dominant action ("Smart Optimize Now"). Secondary actions are clearly secondary.
3. **AI insights must be actionable** — "Abnormal spike · Brave + Spotify consuming 42% at idle" beats "CPU high". Insights always name the cause and propose a fix.
4. **Job-based naming everywhere** — "Debloat" not "Bloatware Removal", "Clean" not "Junk Cleaner". 1-2 words per navigation item.
5. **Power tools stay accessible but out of the way** — Advanced section below divider, collapsible, not hidden.
6. **Cross-platform first** — every design decision works on Windows, macOS, and Linux. Platform-specific modules auto-filter. No Windows-only visual assumptions.
7. **CORTEX is ambient, not gated** — CORTEX appears throughout the app as status indicators, pattern learning messages in the status bar, dashboard insights, and contextual recommendations. The `AI Features` sidebar entry is where users *control* CORTEX (toggles, settings, chat, recommendations), but the AI *experience* happens everywhere — not only on that page.

---

## 4. Brand Identity

### Name hierarchy

- **AuraCore Pro** — product name
- **CORTEX** — AI subsystem brand name (all caps, often without additional styling)
- Example taglines: `"AuraCore Pro • Powered by Cortex"`, `"Think smarter with Cortex"`

### AI brand appearance

- In sidebar: `✦ AI Features` with `CORTEX` chip tag (purple)
- In dashboard header: `"Cortex is monitoring · Auto-detected: ..."`
- In status bar: `"✦ Cortex · Learning your patterns (day 3)"`
- In insights card: `"✦ Cortex Insights"` title
- In hero CTA: `"✦ CORTEX RECOMMENDS"` kicker label
- AI toggle chip: `"Cortex AI · ON"` in header right side

---

## 5. Design Tokens

### Color palette (dark theme, primary)

| Token | Value | Usage |
|-------|-------|-------|
| `BgDeep` | `#0A0A10` | App background, inside gauge circles |
| `BgSurface` | `#0E0E14` | Sidebar base, cards fallback |
| `BgCard` | `rgba(255,255,255,0.025)` | Card surface over Mica/blur |
| `BgCardElevated` | `rgba(255,255,255,0.04)` | Hover/active card |
| `BorderSubtle` | `rgba(255,255,255,0.06)` | Card borders |
| `BorderEmphasis` | `rgba(255,255,255,0.08)` | Interactive borders |
| `TextPrimary` | `#F0F0F5` | Headings, primary body |
| `TextSecondary` | `#E8E8F0` | Secondary body |
| `TextMuted` | `#888899` | Labels, metadata |
| `TextDisabled` | `#555570` | Disabled, section headers |

### Accent palette

| Token | Value | Usage |
|-------|-------|-------|
| `AccentTeal` | `#00D4AA` | Primary brand, CTAs, health, active nav |
| `AccentTealLight` | `#6CE0C0` | Hover, sub-text |
| `AccentTealDim` | `rgba(0,212,170,0.08)` | Card backgrounds |
| `AccentPurple` | `#B088FF` | CORTEX, RAM, AI features |
| `AccentPurpleDeep` | `#8B5CF6` | Logo gradient, secondary AI |
| `AccentPurpleDim` | `rgba(139,92,246,0.06)` | AI Features section bg |
| `AccentAmber` | `#F59E0B` | Warnings, Disk, Gaming mode |
| `AccentPink` | `#EC4899` | GPU, avatar gradient |

### Semantic

| Token | Value | Usage |
|-------|-------|-------|
| `StatusSuccess` | `#00D4AA` | Healthy, excellent, connected |
| `StatusWarning` | `#F59E0B` | Spike, warning |
| `StatusError` | `#EF4444` | Critical, error |
| `StatusInfo` | `#B088FF` | AI insights, predictions |

### Typography

Font stack: `"Segoe UI Variable", -apple-system, "SF Pro Text", "Segoe UI", system-ui, sans-serif`

| Token | Size | Weight | Usage |
|-------|------|--------|-------|
| `TextDisplay` | 24px | 600 | Page title (rarely used) |
| `TextHeading` | 18px | 600 | Section titles, dashboard title |
| `TextSubheading` | 14px | 600 | Card titles |
| `TextBody` | 12px | 400 | Body text |
| `TextBodySmall` | 11px | 400 | Secondary text |
| `TextLabel` | 10px | 600 | Uppercase labels, metadata (letter-spacing: 1px, text-transform: uppercase) |
| `TextCaption` | 9px | 400 | Small caption, badges |

Line heights: body text 1.5, headings 1.2.

### Spacing scale

`4, 6, 8, 10, 12, 14, 16, 18, 20, 24, 32` px. No arbitrary values.

### Radius scale

| Token | Value | Usage |
|-------|-------|-------|
| `RadiusXs` | 4px | Chips, tags |
| `RadiusSm` | 6px | Small buttons, inline elements |
| `RadiusMd` | 8px | Standard cards, buttons |
| `RadiusLg` | 12px | Large cards, gauge containers |
| `RadiusXl` | 14px | Hero cards, featured surfaces |
| `RadiusFull` | 50% | Avatars, gauges |

### Shadows / Glow

| Token | Value | Usage |
|-------|-------|-------|
| `GlowTeal` | `0 0 20px rgba(0,212,170,0.2)` | Gauge ring (teal) |
| `GlowPurple` | `0 0 20px rgba(176,136,255,0.2)` | Gauge ring (purple) |
| `GlowAmber` | `0 0 20px rgba(245,158,11,0.18)` | Gauge ring (amber) |
| `GlowPink` | `0 0 20px rgba(236,72,153,0.2)` | Gauge ring (pink) |
| `GlowHealth` | `0 0 28px rgba(0,212,170,0.4)` | Health score glow (stronger) |
| `GlowHero` | `0 0 32px rgba(0,212,170,0.1)` | Hero CTA ambient |

### Icon system

- **Library:** Lucide Icons (1400+ icons, MIT, SVG, cross-platform)
- **Avalonia integration:** `Projektanker.Icons.Avalonia.MaterialDesign` or similar XAML-friendly icon package. If Lucide isn't available, fallback is Fluent UI System Icons (also MIT).
- **Style:** Line-based, `stroke-width: 2`, default size 14-16px (18px for sidebar active, 20px for gauge header)
- **Fill icons** reserved for: Health (heart), Smart Optimize (zap lightning), AI Features (star/sparkle) — all semantically "filled" states

**Key icon mappings:**

| Concept | Lucide name |
|---------|-------------|
| Dashboard | `layout-dashboard` |
| CPU | `cpu` |
| RAM | `memory-stick` |
| GPU | `circuit-board` |
| Disk | `hard-drive` |
| Health | `heart` (filled) |
| Smart Optimize | `zap` |
| Clean Junk | `sparkles` |
| Optimize RAM | `rotate-ccw` |
| Gaming | `gamepad-2` |
| Security | `shield` |
| Apps & Tools | `package` |
| AI Features / CORTEX | `star` (filled) |
| Settings | `settings` |
| Prediction | `target` |
| Pattern learned | `trending-up` |
| Warning | `alert-triangle` |

---

## 6. Navigation Model

### Sidebar structure

```
AuraCore  PRO • CORTEX
────────────────────────
[user chip]  admin@aura...  ADMIN

◆ Dashboard                      (active, teal accent)

[⚡ Smart Optimize]              (pinned hero, sidebar-width card)

⚡ Optimize
✦ Clean & Debloat
🎮 Gaming
🛡 Security
📦 Apps & Tools

✦ AI Features    [CORTEX]        (purple accent)

── ADVANCED ────────────
⚙ Registry (deep)
⚙ Process Monitor
⚙ Tweaks & Fonts
⚙ ... (collapsible, +N more)

──────── (bottom)
⚙ Settings
```

### Module → Category mapping

| Category | Modules |
|----------|---------|
| **Optimize** | RAM Optimizer, Startup Optimizer, Network Optimizer, Battery Optimizer, Storage Compression |
| **Clean & Debloat** | Junk Cleaner, Disk Cleanup, Privacy Cleaner, Registry Cleaner (basic), Bloatware Removal, App Manager |
| **Gaming** | Gaming Mode, FPS Tweaks, Gaming-specific startup/network profiles |
| **Security** | Defender Manager, Firewall Rules, File Shredder, Hosts Editor |
| **Apps & Tools** | App Installer, Driver Updater, Service Manager, ISO Builder, Disk Health, Space Analyzer |
| **AI Features** | AI Insights, Recommendations, Smart Schedule, AI Chat [Experimental] |
| **Advanced** | Registry Optimizer (deep), Env Variables, Symlink Manager, Process Monitor, Font Manager, Context/Taskbar/Explorer Tweaks, Autorun Manager, Wake-on-LAN, Network Monitor, DNS Benchmark |

### Platform filtering

Modules auto-filter by platform at runtime.

- Linux: Gaming Mode (if gaming tweaks irrelevant), Defender Manager, ISO Builder, Registry, Drive Letter handling, AppInstaller (Windows-specific), Wake-on-LAN (if unsupported) — all hidden.
- macOS: Defender Manager, Registry (all), ISO Builder, Windows-specific tweaks — all hidden.
- Categories stay consistent by name — only contents change. If a category ends up empty on a platform, the category itself is hidden.

---

## 7. Dashboard Reference Layout

The Dashboard is the only screen fully specified in this Vision Document. Other screens (AI Features, module pages) will be designed in their respective phase specs.

### Structure

```
[Header]  Dashboard                           [LIVE] [Cortex AI · ON]
          Cortex is monitoring · Auto-detected: Gaming session at 21:00

[Row 1: Gauges]  CPU · RAM · GPU · DISK · HEALTH
                 (5 circular gauges; GPU hides if not detected)

[Row 2]  [Hero CTA — CORTEX RECOMMENDS]    [Cortex Insights — 3 items]
         Smart Optimize Now                  · Abnormal spike
         +15% performance                    · Pattern learned
         [Optimize →] [Review]               · Prediction

[Row 3]  [System Info]                      [Quick Actions 2×2]
         OS, CPU, GPU, RAM, Uptime           Clean · Optimize · Gaming · Security

[Status bar]  ● Ready · ✦ Cortex · Learning your patterns (day 3)    RAM: 228 MB · ● Connected
```

### GPU detection rules

- **Integrated + discrete GPU:** Show discrete only (active).
- **Integrated only:** Show integrated GPU.
- **No GPU detected:** Hide GPU gauge; dashboard becomes 4-column (CPU, RAM, Disk, Health).
- **User preference:** Settings → "Show Uptime instead of GPU" toggle (defaults to off).

### Gauges

- Container: `BgCard`, 12px radius, 12px padding, `BorderSubtle` border.
- Gauge: conic-gradient ring, 56px outer / 44px inner, proper glow (`GlowTeal`/`GlowPurple`/etc.).
- Number centered, `TextHeading` weight 700, 14px.
- Sub-label: 7-9px muted text (e.g. `/ 31.3 GB`, `68°C`, `% unit`).
- Footer: AI insight badge (e.g. `⚠ Spike`, `127 days`, `41% · healthy`), `TextCaption`.
- Health gauge is elevated — amber accent fill (`AccentTeal` at 8% opacity), `GlowHealth` boost.

### Hero CTA

- Background: linear gradient `teal → purple`, 15% opacity over 10% opacity.
- Radial glow in top-right corner (teal, 25% opacity fading out).
- Kicker label: `✦ CORTEX RECOMMENDS` in purple, 10px uppercase letter-spacing.
- Title: 17px weight 600, primary text.
- Body: 11px secondary text, line-height 1.5, performance estimate in teal bold.
- Primary button: solid teal with `GlowHero`, 8px padding, teal→deep-bg text.
- Secondary button: transparent `BgCardElevated`, `BorderSubtle`.

### Cortex Insights card

- Background: `AccentPurpleDim`, purple `BorderSubtle`, 14px radius.
- Title: `✦ Cortex Insights` (purple), right-aligned "Updated 2m ago" meta.
- 3 rows max: each with colored icon, title, description.
- Icon colors: amber (warnings), teal (pattern learned), purple (predictions).

### System Info

- Card with 6 rows: OS, CPU, GPU, RAM, Uptime, Platform.
- Left column: muted label. Right column: primary value.
- "Platform: ✓ Cross-platform" in teal.

### Quick Actions

- 2×2 grid of action buttons, each tinted by accent (teal/purple/amber/teal).
- 8px radius, 8×10 padding, icon + title + description line.

### Header indicators

- `LIVE` chip: teal dot (glowing), teal text, teal subtle background.
- `Cortex AI · ON` chip: purple star icon, purple text, purple subtle background. Chip is tappable/clickable → toggles global Cortex AI off/on.

### Status bar

- Height: ~26px, translucent background with backdrop blur.
- Left: `● Ready`, `✦ Cortex · Learning your patterns (day N)` (purple).
- Right: `RAM: XXX MB`, `● Connected` (teal).

---

## 8. Component Library Inventory

Components needed across the rebuild, to be built in Phase 1 (Design System):

| Component | Description | Phase |
|-----------|-------------|-------|
| `Gauge` | Circular conic-gradient progress, size variants, color variants, optional center text/subtext, optional AI insight footer | 1 |
| `GlassCard` | Card with backdrop blur, border, radius variants, optional glow | 1 |
| `HeroCTA` | Large call-to-action card with kicker, title, body, primary+secondary buttons, radial glow | 1 |
| `InsightCard` | Vertical list of insight rows (icon + title + description) with color coding | 1 |
| `QuickActionTile` | Small action tile with accent tint, icon, label, sub-label | 1 |
| `SidebarNavItem` | Nav item with icon, label, optional trailing chip, active/hover/muted states | 1 |
| `SidebarSectionDivider` | Horizontal dividers with inline label (e.g. `── ADVANCED ──`) | 1 |
| `StatusChip` | Compact chip with dot/icon + label, 4 color variants (teal/purple/amber/red) | 1 |
| `Toggle` | On/off toggle switch, used in AI Features page | 1 |
| `AccentBadge` | Small badge like `[CORTEX]`, `[ADMIN]`, `[Experimental]` | 1 |
| `UserChip` | Avatar + email + role badge (sidebar top) | 1 |
| `AppLogoBadge` | Logo + product name + "PRO · CORTEX" tagline | 1 |

These are implemented once in Phase 1 and reused across all phases. Module pages and AI Features page in later phases compose these primitives.

---

## 9. Platform Integration

### Windows 11+

- Window: Mica backdrop (requires `SystemBackdrop="Mica"` on `MainWindow`).
- Title bar: optional custom chrome with Windows traffic lights; or default chrome with Mica extended to title bar.
- Fluent acrylic for dropdowns/context menus.

### macOS 12+

- Window: Vibrancy blur via `TransparencyLevelHints="Mica,AcrylicBlur,Blur"` (Avalonia cross-platform hints — framework picks best available).
- Native macOS traffic lights.
- No custom chrome unless needed.

### Linux (X11 / Wayland)

- Solid `BgDeep` background with subtle radial gradient (CSS-equivalent in XAML).
- No translucency (platform unreliable). Maintains visual consistency via gradient + glow alone.
- Standard window decorations.

### Theme

- **Dark-only** as default and primary theme. No light theme in v1 — can be added later as a lower priority.
- `RequestedThemeVariant="Dark"` in `App.axaml`.

---

## 10. Phased Implementation Plan

This vision document is the root. Each phase below gets its own spec + plan + implementation.

| Phase | Scope | Estimated effort | Dependencies |
|-------|-------|------------------|--------------|
| **1** | **Design System** — All tokens defined in `AuraCoreTheme.axaml` v2. All components from §8 built. Primitives testable in isolation. | ~1 week | Nothing (foundation) |
| **2** | **Sidebar Restructure + Dashboard Redesign** — Sidebar refactored to 7 categories + Advanced divider. Dashboard rebuilt using components from Phase 1. | ~1-2 weeks | Phase 1 |
| **3** | **AI Features Consolidation** — Single `AIFeaturesView` page containing Insights + Recommendations + Smart Schedule + Chat [Experimental]. Toggle-first design per module. CORTEX branding complete. | ~1 week | Phase 1 |
| **4** | **Module Pages Refactor** — 26 module pages migrate to card-based layout using components. Inline hero card + data cards pattern. Batched: ~5-7 modules per sub-phase. | ~3-4 weeks | Phases 1-2 |
| **5** | **Advanced / Polish** — Settings, Onboarding, Login cohesion. Animations (scan/optimize completion bloom). Platform-specific refinements. | ~1 week | Phase 1-4 |

Between each phase: a retrospective against this Vision Document to check if any decisions need updating.

---

## 11. Success Criteria

The rebuild is complete when:

- [ ] Sidebar has 7 categories + Advanced divider. 26 modules mapped per §6.
- [ ] Dashboard matches §7 reference layout on Windows, macOS, and Linux.
- [ ] CORTEX branding visible in sidebar, header chip, status bar, insights card.
- [ ] All cards use `GlassCard` primitive (no ad-hoc card styles).
- [ ] All icons are Lucide SVG (no Unicode glyphs, no emoji in core UI).
- [ ] GPU gauge auto-hides when no GPU detected.
- [ ] Cross-platform build smoke-tests pass on Win11, macOS 14, Ubuntu 24.04.
- [ ] Velotic-style AI Features page exists with toggle-first layout.

---

## 12. Out of Scope

This vision document and the phased rebuild **do not** cover:

- Light theme (v2+).
- Localization beyond existing EN/TR (v2+).
- Mobile/tablet layouts (app is desktop-only).
- Admin panel UI (separate project, already deployed).
- Backend/API changes.
- Adding new features that didn't exist before. We are re-skinning and reorganizing, not expanding scope.
- Marketing website / landing page (separate project at auracore.pro).

---

## 13. Reference Mockups

Interactive HTML mockups from brainstorming (for reference during phase implementation):

- `/.superpowers/brainstorm/<session>/content/visual-direction.html` — 3 visual direction options (Clean Minimal / Fluent / Neon Gaming). Chosen: Modern Glassmorphism.
- `/.superpowers/brainstorm/<session>/content/sidebar-structure.html` — Current vs Proposed sidebar comparison + category mapping table.
- `/.superpowers/brainstorm/<session>/content/full-dashboard-mockup-v4.html` — **Final Dashboard reference**. Matches §7 layout exactly. Use this as the visual source of truth for Phase 2 implementation.

---

## Appendix: Decisions Log

| # | Decision | Date |
|---|----------|------|
| 1 | Target user: Hybrid (both casual and power user) | 2026-04-14 |
| 2 | Visual direction: Modern Glassmorphism, neutral dark, cross-platform (not Windows-only Fluent) | 2026-04-14 |
| 3 | Sidebar: 7 categories (Dashboard + Optimize + Clean&Debloat + Gaming + Security + Apps&Tools + AI Features) + Advanced divider | 2026-04-14 |
| 4 | AI brand name: CORTEX (replacing generic "AuraCore AI") | 2026-04-14 |
| 5 | Icon library: Lucide (line-based SVG, 2px stroke) | 2026-04-14 |
| 6 | Dashboard: gauge row (5 items) + hero CTA + insights + system info + quick actions | 2026-04-14 |
| 7 | GPU detection rules: auto-hide if none detected | 2026-04-14 |
| 8 | Dark theme only in v1 (light theme deferred) | 2026-04-14 |
| 9 | Accent colors: teal `#00D4AA` primary, purple `#B088FF` for AI/RAM, amber `#F59E0B` for warnings/disk/gaming, pink `#EC4899` for GPU | 2026-04-14 |
| 10 | Phased delivery: 5 phases, each with own spec + plan | 2026-04-14 |
