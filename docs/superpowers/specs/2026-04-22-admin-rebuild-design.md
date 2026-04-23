# Admin Panel Rebuild — Design Spec

**Status:** Design approved (user, 2026-04-22, brainstorm Q&A). Next: writing-plans.
**Branch:** `phase-6-admin-rebuild` (created from `main` HEAD `75322f3`, the Phase 6.9 + 5 hotfix-rounds end state).
**Phase ref:** Phase 6 Item 10 — admin panel UI rebuild + SignalR hub + repo migration. Phase 6.11 (queued separately) = Low findings cherry-pick + new features (role-change UI, TOTP backup codes, etc.). Phase 6.12+ = native mobile app.

## Context

Phase 6.8 + 6.9 closed all Critical and High audit findings (and 26 Medium + 4 Low cherry-picks). Admin panel is now functionally complete but has structural debt:

- **`admin-panel-work/src/app/page.tsx` is a 1527-line monolith** containing all 12 tabs as inline React components.
- **No mobile responsive support** — sidebar fixed at 200px, tables overflow, forms collide on narrow screens (CTP-3 deferred).
- **Admin panel source lives on origin** (`/root/admin-panel/`), pulled to local `admin-panel-work/` (gitignored) for editing — during the Phase 6.9 hotfix session, source files (`api.ts`, `signalr.ts`) regressed to pre-hotfix state at least once, requiring a re-apply rebuild. Bringing the source into the main repo eliminates this risk.
- **Backend compatibility aliases** added during Phase 6.8/6.9 (dual `activeDevices`+`deviceCount` projection, dual `ip-whitelist`+`whitelist` route, audit_log → login_attempts shape adapter in `api.ts`) are dead-code debt that the rebuild can retire.
- **SignalR hub is gated off** in frontend (`SIGNALR_ENABLED = false` in `signalr.ts`) because the backend `/hubs/admin` endpoint doesn't exist. The dashboard's "Live" indicator is currently optimistic-fake.
- **License key format** `c8a91e2d4f7b3091b2a45e8c3d6f9012` (32-char hex) is hard to read/copy (T3.6 Low cherry-forward).

Phase 6.10 (this spec) ships a **visual + structural rebuild + SignalR backend hub + repo migration** in one branch.

## Scope

### In scope — Phase 6.10 (this phase)

**Structural:**
- Decompose `src/app/page.tsx` (1527 lines) into per-tab React component files (`src/pages/UsersPage.tsx`, `LicensesPage.tsx`, etc. — 12+ files), each ≤ 400 lines.
- Bring admin panel source INTO main repo at `admin-panel/` (root-level dir). End the `admin-panel-work/` gitignored-scratch pattern.
- Apply hybrid visual style (see D1) across all 12 tabs.
- Mobile responsive: bottom-tab nav + grid sheet (see D2).
- Frontend test harness: Vitest + React Testing Library (see D7).

**Backend:**
- Implement `AdminHub` SignalR hub at `/hubs/admin` — broadcasts `UserRegistered`, `UserLogin`, `Payment`, `CrashReport`, `Telemetry` events to admin-only group. Frontend `SIGNALR_ENABLED` flag flipped to `true`.
- Retire backend compat aliases:
  - `AdminLicenseController.List`: drop `activeDevices` (keep `deviceCount` only — frontend converges on one name)
  - `AdminIpWhitelistController`: drop `[Route("api/admin/whitelist")]` (keep `[Route("api/admin/ip-whitelist")]` only — frontend uses canonical)
  - `AdminAuditLogController.LegacyAlias` (`/api/admin/audit/login-attempts` redirect): can stay since it's harmless, but frontend no longer needs the adapter (audit_log columns rendered natively)
- Audit Log tab native redesign — display `{actor, action, target, time}` columns directly (no shape transform).
- License key format `AC-XXXX-XXXX-XXXX-XXXX` (T3.6) — backend issues new format; validation regex accepts both new + 32-char legacy for backward compat. No DB migration; existing keys keep their format.

**Operational:**
- Build pipeline: `npm run build` invocable from main repo root (e.g. `cd admin-panel && npm run build`).
- Deploy: scp pattern continues for now (atomic file-replace at `/var/www/admin-panel/`); pipeline-driven CI deploy is out of scope (Phase 6.11+).
- PWA stretch (~1-2h work — manifest.json + iOS meta tags + service worker stub) — installable home-screen icon, no offline yet.

### Out of scope — deferred

- **Phase 6.11** (queued separately, brainstorm later): 15 deferred Low findings + new features (role-change UI T2.7, TOTP backup codes T3.18, blockchain explorer link T3.8, online/offline device indicator T3.9, PII erasure UI T3.15, etc.) + CI/CD deploy pipeline + audit_log retention dashboard + SignalR enhancements (presence, typing indicators, etc.)
- **Phase 6.12+** (future direction, no firm date): Native mobile app — Capacitor wrap of the rebuilt admin panel OR React Native rewrite. Spec'd separately when the time comes.
- **6.10 explicitly NOT included**: New backend features, new admin tabs, theme switcher (light mode), accessibility audit beyond ≥44px tap targets + keyboard nav, i18n / localization for admin panel, role-based access control beyond current admin-only check, GDPR/compliance UI surfaces.

## Design decisions

### D1 — Visual style: hybrid Glass + Terminal Operator

Approved via brainstorm Q1 + iteration. Synthesized from:

**From "A" (current evolved):**
- Glass cards: `bg-white/[0.04] backdrop-blur-xl border border-white/[0.06] rounded-2xl`
- Cyan + purple gradient accent (`linear-gradient(135deg, #06b6d4, #8b5cf6)`)
- Ambient radial glow corners (cyan top-left, purple bottom-right)
- Soft-rounded pills + buttons
- Primary CTA: gradient fill + glow shadow (`box-shadow: 0 4px 20px rgba(6,182,212,0.25)`)
- Page title: gradient text (linear-gradient(135deg, #ededef, #a0a0a8))

**From "D" (terminal tech):**
- Monospace typography (`'JetBrains Mono', ui-monospace`) for: data values, table content, labels, button text, sidebar nav, search placeholder, breadcrumb, KPI numbers
- Sans-serif (Outfit) for: page titles only
- `$ refresh` / `$ new user` command-prefix on action buttons
- `> edit` / `> rm` prefix on row-action buttons
- ALLCAPS outlined status pills (`PRO`, `ENT`, `FREE`)
- Breadcrumb path style: `~/admin/users` (terminal pwd-flavored)
- Snake-case sidebar labels: `crash_reports`, `audit_log`, `ip_whitelist`
- Dashed table row borders (`border-bottom: 1px dashed rgba(255,255,255,0.05)`)

**Component library:** stay on Tailwind utility classes + custom shared primitives in `admin-panel/src/components/`. No design-system framework added (Radix Themes, etc.) — the existing Tailwind + lucide-react + recharts stack is preserved.

### D2 — Mobile responsive: bottom-tab nav + grid sheet hybrid

Approved via brainstorm Q3 + iteration.

**Desktop (≥768px):** unchanged — left sidebar with 12 tabs, content area to the right.

**Mobile (<768px):**
- Sidebar replaced by **bottom tab bar** with 4 primary tabs + "more" gradient button: `dash · users · licenses · pay · more`. Bar is 64px tall, glass backdrop, fixed bottom.
- Tap "more" → **bottom sheet rises from bottom** (80% screen height, glass, rounded top corners), showing all 12 tabs as **2-column card grid** with **live mini-stats** (e.g. `crashes: 12 today` with red border alert, `users: 1,247 +47 this week`, `revenue: $4.8k mtd`).
- Tables → mobile **card list** (each row becomes a stacked card with email + tier pill + date below).
- Forms: stack vertically (label above input, no side-by-side).
- Action buttons: ≥44px min-height + min-width (T1.6 carry-forward).
- Top bar: page-name breadcrumb + 1-2 icon-buttons (search, refresh).

**Breakpoint:** Tailwind's `md:` (768px) is the divider — desktop nav above, mobile below.

### D3 — Per-tab decomposition

Replace `src/app/page.tsx` monolith with this structure:

```
admin-panel/
├─ src/
│  ├─ app/
│  │  ├─ layout.tsx                  (root layout — fonts, theme provider)
│  │  ├─ page.tsx                    (≤ 100 lines — auth gate + AdminApp wrapper that mounts current page)
│  │  └─ globals.css                 (Tailwind directives + custom utilities)
│  ├─ pages/                         (12 tab components — one per file)
│  │  ├─ DashboardPage.tsx           (KPI cards + recent activity + revenue chart)
│  │  ├─ UsersPage.tsx
│  │  ├─ LicensesPage.tsx
│  │  ├─ SubscriptionsPage.tsx
│  │  ├─ PaymentsPage.tsx
│  │  ├─ DevicesPage.tsx
│  │  ├─ CrashReportsPage.tsx
│  │  ├─ TelemetryPage.tsx
│  │  ├─ AuditLogPage.tsx            (NATIVE redesign — actor/action/target/time columns)
│  │  ├─ IpWhitelistPage.tsx
│  │  ├─ UpdatesPage.tsx
│  │  └─ ConfigurationPage.tsx
│  ├─ components/                    (shared primitives)
│  │  ├─ Sidebar.tsx                 (desktop nav + mobile bottom-tab bar + sheet)
│  │  ├─ TopBar.tsx                  (breadcrumb + actions)
│  │  ├─ KpiCard.tsx
│  │  ├─ DataTable.tsx               (responsive: table on desktop, card list on mobile)
│  │  ├─ StatusBadge.tsx             (port from page.tsx, unchanged statuses)
│  │  ├─ ConfirmDialog.tsx           (port from Phase 6.9, refine if needed)
│  │  ├─ PaginationLabel.tsx         (port from Phase 6.9)
│  │  ├─ PageHeader.tsx              (gradient title + subtitle + actions slot)
│  │  ├─ EmptyState.tsx
│  │  ├─ LoginScreen.tsx
│  │  └─ MobileSheet.tsx             (the "more" bottom sheet renderer)
│  ├─ hooks/
│  │  ├─ useDebouncedValue.ts        (port)
│  │  ├─ useSignalR.ts               (NEW — wraps the existing signalr client with React lifecycle)
│  │  └─ useMediaQuery.ts            (NEW — for responsive logic if needed)
│  └─ lib/
│     ├─ api.ts                      (compat-layer-removed; one shape per endpoint)
│     ├─ signalr.ts                  (SIGNALR_ENABLED = true once backend hub lands)
│     ├─ format.ts                   (formatCurrency + formatBytes + formatDate + formatLicenseKey NEW)
│     └─ types.ts                    (NEW — shared TypeScript interfaces for API responses)
└─ ... (package.json, next.config.js, tsconfig.json, tailwind.config.js, public/, etc.)
```

Each tab page file ≤ 400 lines. Shared logic via hooks + components.

### D4 — Repo migration: `admin-panel/` at root

Move `/root/admin-panel/` contents from origin → `admin-panel/` in main repo. Delete `admin-panel-work/` from local + remove from `.gitignore`. Origin's `/root/admin-panel/` stays as a deploy mirror but no longer authoritative — main repo is.

Build flow:
- Local dev: `cd admin-panel && npm install && npm run dev` → http://localhost:3000
- Production build: `cd admin-panel && npm run build` → produces `admin-panel/out/`
- Deploy: scp `admin-panel/out/.` to origin `/var/www/admin-panel/` (same as Phase 6.9 — no CI yet, that's Phase 6.11)

`.gitignore` additions: `admin-panel/node_modules/`, `admin-panel/.next/`, `admin-panel/out/`. Remove `admin-panel-work/` line.

### D5 — Backend SignalR hub (AdminHub)

Implement `src/Backend/AuraCore.API/Hubs/AdminHub.cs`:

```csharp
[Authorize(Roles = "admin")]
public class AdminHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        await base.OnConnectedAsync();
    }
}
```

Register in `Program.cs`: `builder.Services.AddSignalR();` + `app.MapHub<AdminHub>("/hubs/admin");` + add `IHubContext<AdminHub>` injection where events fire.

**Events broadcast (existing frontend listeners — see signalr.ts `on(...)` calls):**

| Event | Trigger | Payload | Source |
|---|---|---|---|
| `UserRegistered` | New user signup completes | `{email, id, createdAt}` | `AuthController.Register` |
| `UserLogin` | Login attempt completes | `{email, success, ipAddress, createdAt}` | `AuthController.Login` (after rate-limit + TOTP verdict) |
| `Payment` | Stripe webhook processed `checkout.session.completed` OR crypto verified | `{email, amount, currency, plan, createdAt}` | `StripeController.HandleCheckoutCompleted` + `CryptoController.AdminVerifyPayment` |
| `CrashReport` | Crash report submitted | `{type, version, deviceId, createdAt}` | `CrashReportController.Submit` |
| `Telemetry` | Telemetry batch ingested | `{count, deviceId, createdAt}` | `TelemetryController.ReceiveBatch` (only emits if batch ≥ 10 events to avoid spam) |
| `AdminCount` | Optional — admin connections changed | `{count}` | Hub `OnConnected/OnDisconnected` |

Each emit pattern: `await _hub.Clients.Group("admins").SendAsync("EventName", payload, ct);`

Frontend `signalr.ts` flips `SIGNALR_ENABLED = true`. Existing `on('UserRegistered', ...)` etc. listeners in `DashboardPage.tsx` activity feed start receiving real events.

**CORS:** AllowCredentials already in place from Phase 6.9 hotfix. Hub endpoint inherits same policy.

### D6 — Audit Log tab native redesign

Replace the Phase 6.9 transform-layer hack in `api.ts` (`getLoginAttempts` adapter that maps audit_log → login_attempts shape) with native rendering:

**New AuditLogPage columns:** `Actor (email + id) · Action · Target (type/id) · IP · Time`

Stats KPIs change from login-attempt-shaped to audit-shaped: `Total mutations · Today · This week · Top action (last 7d)`.

`api.ts.getAuditLog()` (renamed from `getLoginAttempts`) returns native shape directly:
```typescript
{ items: AuditLogEntry[], total: number, page: number, pages: number }
```

Adapter code in current `api.ts` deleted.

### D7 — Test framework: Vitest + React Testing Library

Install:
- `vitest` + `@vitejs/plugin-react` + `@testing-library/react` + `@testing-library/jest-dom` + `jsdom`
- `vitest.config.ts` at `admin-panel/` root with React plugin + jsdom env

**Test scope** (write tests for):
- Critical components (ConfirmDialog, DataTable responsive switch, MobileSheet, PageHeader)
- Custom hooks (useDebouncedValue, useSignalR)
- `api.ts` adapter functions (smoke: returns expected shape on mocked fetch)
- `format.ts` helpers (currency, bytes, date, licenseKey)

**Skip tests for:**
- Per-page integration tests (would require full mock + are large) — leave to Playwright in Phase 6.11+
- Visual regression (Storybook etc. — out of scope)

**Target:** ~30-50 new frontend tests. Backend tests +5 for AdminHub registration / event emit pattern via mock. Total post-6.10: backend ~78-80 + frontend ~30-50 = ~110-130 net new admin-relevant tests on top of 2347 baseline → target ~2400-2420.

### D8 — License key format (T3.6)

**New issued keys:** `AC-XXXX-XXXX-XXXX-XXXX` (16 hex chars, dash-separated).

`AdminSubscriptionController.Grant` + any other key-issue path generates: `"AC-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant().Insert(4, "-").Insert(9, "-").Insert(14, "-")` (or equivalent).

**Backend validation** (`License.Key` setter or DB constraint): `^(AC-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}-[A-F0-9]{4}|[a-f0-9]{32})$` — accepts both new + 32-char legacy.

**No DB migration** — existing license rows keep their old format. Display-side: format-as-is (don't pretty-print legacy keys, they're already accepted).

### D9 — PWA stretch task

Install in 6.10 if Wave 1-3 land on time (i.e., real stretch). Wave 5 task:
- Create `admin-panel/public/manifest.json`: name, short_name, icons, display=standalone, theme_color (#06b6d4), background_color (#08080c)
- Create `admin-panel/public/icon-192.png` + `icon-512.png` (generate from existing logo SVG if available; use placeholder otherwise)
- Add to `app/layout.tsx` head: `<link rel="manifest" href="/manifest.json">`, `<meta name="theme-color">`, iOS meta tags (`apple-mobile-web-app-capable`, `apple-touch-icon`)
- Skip service worker for now (no offline support in 6.10 — Phase 6.11+ if needed)

If 6.10 timeline tightens, drop PWA → 6.11 starter task.

## Architecture — Wave breakdown

**Wave 1 — Repo migration + scaffolding (foundation):**
- scp `/root/admin-panel/` from origin → local `admin-panel/`
- Move into git tracking; `.gitignore` updates
- Verify `npm install` + `npm run build` work from new path
- Commit baseline before any code changes

**Wave 2 — Component decomposition (per-tab split):**
- Extract 12 tabs from `page.tsx` monolith → `src/pages/*.tsx`
- Extract shared primitives → `src/components/*.tsx`
- Extract hooks → `src/hooks/*.ts`
- Update `src/app/page.tsx` → ≤ 100-line auth gate + AdminApp wrapper that imports + mounts current tab
- Backend untouched in this wave

**Wave 3 — Visual style application + mobile responsive:**
- Apply hybrid Glass + Terminal style (D1) to all 12 tabs
- Implement mobile responsive (D2): bottom tab bar + grid sheet + DataTable card-list mode
- License key format display (D8 frontend side)

**Wave 4 — Backend SignalR hub + frontend re-enable:**
- Implement AdminHub + event emits at trigger sites (D5)
- Frontend `SIGNALR_ENABLED = true`
- DashboardPage activity feed receives live events
- Backend tests for AdminHub registration

**Wave 5 — Audit Log tab native redesign + License key format issuance + Compat retirement + Tests + PWA:**
- AuditLogPage native columns (D6) + api.ts adapter removal
- License key issuance new format (D8 backend side)
- Backend compat alias retirement (drop activeDevices, drop /api/admin/whitelist alias-route)
- Frontend test harness setup + write tests (D7)
- PWA stretch (D9 — if time permits)

**Wave 6 — Final deploy + ceremonial:**
- Build admin-panel locally
- Backup prod admin-panel + scp new build
- Backend release publish + scp + restart (one deploy for SignalR hub + AuditLog API + retired aliases + license key validation)
- Smoke test: SignalR connection from prod admin panel browser, audit_log live capture, license issuance new format, mobile viewport on real device
- Memory file + MEMORY.md pointer + ceremonial merge to main + push (user-gated)

## Testing strategy

- **Frontend** (Vitest + RTL): ~30-50 tests across components + hooks + lib helpers. Setup in Wave 5.
- **Backend** (xUnit): ~5 tests for AdminHub event-emit pattern via mock IHubContext. Existing 2347 stays green.
- **Skipped:** per-page integration (Playwright — Phase 6.11+); visual regression; mobile-device-emulator tests (manual smoke instead).
- **Target:** 2347 → ~2400-2420 (+55-75 net).

## Deployment flow

Two-deploy pattern (parallel to Phase 6.9):

**Mid deploy (after Wave 4):** backend with SignalR hub. Backend-only, low risk. Smoke: connect to `wss://api.auracore.pro/hubs/admin` from browser console with admin JWT.

**Final deploy (after Wave 5):** admin-panel frontend rebuild. Frontend-only, atomic file replace at `/var/www/admin-panel/`. Smoke: full UI walkthrough on desktop + mobile viewport.

## Future direction (out of 6.10 scope)

### Phase 6.11 (queued — separate brainstorm later)
- 15 deferred Low findings (T3.1, T3.3, T3.5, T3.6 [resolved here], T3.8, T3.9, T3.10, T3.14, T3.15, T3.18, T3.19)
- New features: role-change UI (T2.7), TOTP backup codes (T3.18), blockchain explorer link (T3.8), online/offline device indicator (T3.9), PII erasure UI surface (T3.15)
- CI/CD deploy pipeline (GitHub Actions → R2 / S3 host?)
- audit_log retention dashboard (admin can browse + filter all-time)
- SignalR enhancements: presence, typing indicators, multi-admin awareness

### Phase 6.12+ — Native mobile app (no firm date — user direction)
Two paths to evaluate when the time comes:

| Path | Effort | Tradeoff |
|---|---|---|
| **Capacitor wrap** | 1-2 days | Wraps existing rebuilt admin panel. iOS + Android native shells. Web → native gap small. App Store distribution. |
| **React Native rewrite** | 2-3 weeks | Ground-up native UI in RN. Better performance + native feel. More effort + duplicate code paths. |

Capacitor likely the right call given Phase 6.10 already builds a clean responsive web admin. Spec'd separately when scheduled.

## Open questions / known risks

- **SignalR + nginx WebSocket proxy:** must verify nginx config at `/etc/nginx/sites-available/api.auracore.pro` upgrades the WebSocket connection (`proxy_set_header Upgrade $http_upgrade; proxy_set_header Connection "upgrade";`). If missing, Wave 4 ops task adds the directives + reloads nginx.
- **Frontend test runtime:** Vitest with jsdom may not fully simulate browser CSS — visual breakage from refactor not caught by tests. Manual smoke after Wave 3 mandatory.
- **Repo migration git conflicts:** `admin-panel/` will be a large initial commit (1.5k+ lines spread across many files post-decomp). Reviewers see one commit with 30+ files — acceptable for a "migration in" commit.
- **License key backward-compat:** if any external integration (third-party tooling) parses the key format strictly with `^[a-f0-9]{32}$`, the new `AC-XXXX-...` format breaks it. No known external consumer right now (it's admin-issued only); flag as risk if any external partner emerges.
- **Breaking the audit_log adapter:** removing the api.ts transform requires AuditLogPage component to be ready for native shape FIRST. If backend deploy lands before frontend update, the broken state lasts until Wave 6 final deploy. Mitigation: deploy frontend + backend together in Wave 6 (no mid-deploy of compat-retirement until both sides ready).

## Decision log

| Decision | Chosen | Rejected | Why |
|---|---|---|---|
| Scope depth | B (core + SignalR) | A (core only) / C (+Low cherry-pick) / D (+features) | Brainstorm Q2 — SignalR rebuild moment is natural; Low + features deserve their own polished phase (queued as 6.11) |
| Visual direction | Hybrid A (glass) + D (terminal monospace) | A only / B (Linear-flat) / D only | Brainstorm Q1 — user picked elements from A + D; combined yields premium + operator vibe |
| Mobile pattern | Hybrid 2+3 (bottom tabs + grid sheet) | 1 (drawer) / 2 only / 3 only | Brainstorm Q3 — bottom tabs for hot tabs (4) + grid sheet beauty for the other 8 |
| Repo location | `admin-panel/` at root | `src/Frontend/AdminPanel/` / `src/UI/AdminPanel/` | Brainstorm batch — root-level is simplest + most visible |
| Test framework | Vitest + RTL | Playwright / both | Brainstorm batch — component-level fast feedback; E2E in Phase 6.11+ |
| License key format | `AC-XXXX-XXXX-XXXX-XXXX` (16 hex) | Status quo (32-hex raw) / `AC-XXXXXXXXXX-XXXX` other splits | Brainstorm batch — 4-block dash separation balances readability + brevity |
| PWA timing | 6.10 stretch task | 6.11 starter | User chose stretch — small enough; native app deferred to 6.12+ |

## Non-goals

- Light theme / theme switcher (Phase 6.4 added system-theme to desktop app; admin panel intentionally stays dark for operator focus)
- Internationalization (admin panel English-only; landing + desktop have TR support already)
- Role-based access beyond current admin-only check (RBAC = Phase 6.11+)
- Server-side rendering (Next.js used in static-export mode — keep it; no SSR for admin)
- Backend feature additions (no new endpoints beyond AdminHub + AuditLog rename)
- Storybook / Chromatic visual regression
- Accessibility audit beyond ≥44px tap targets + keyboard nav (Phase 6.11+ does WCAG audit)

## Success criteria

Phase 6.10 is DONE when:
- `admin-panel/` directory exists in main repo with all source tracked
- `admin-panel-work/` deleted from local + .gitignore
- `src/app/page.tsx` ≤ 100 lines (auth gate + AdminApp wrapper); 12 tabs in `src/pages/*.tsx` each ≤ 400 lines
- All 12 tabs visually match the hybrid Glass + Terminal style (consistent components, monospace data, gradient titles)
- Mobile (<768px) viewport: bottom-tab nav functional, "more" sheet opens with 12 grid cards + live mini-stats, tables become card lists, forms stack
- SignalR hub at `wss://api.auracore.pro/hubs/admin` accepts admin connections; DashboardPage activity feed receives live `UserRegistered`/`UserLogin`/`Payment`/`CrashReport`/`Telemetry` events end-to-end
- AuditLogPage shows native columns (actor/action/target/time); api.ts adapter for `getLoginAttempts` removed
- Backend compat aliases dropped: `activeDevices` field removed from AdminLicenseController.List projection; `[Route("api/admin/whitelist")]` alias removed from AdminIpWhitelistController
- New license keys issued in `AC-XXXX-XXXX-XXXX-XXXX` format; backend validates both formats
- Vitest test suite passes with ~30-50 frontend tests; backend +5 tests for AdminHub
- 2347 → ~2400-2420 total tests, 0 failed, 0 skipped
- PWA installable on iOS/Android (Add to Home Screen → standalone fullscreen) — if 6.10 stretch task lands
- Memory file written + MEMORY.md pointer updated
- Branch merged to main via `--no-ff` (ceremonial) + pushed to origin (user-gated)

**Spec end.** Writing-plans skill invoked next.
