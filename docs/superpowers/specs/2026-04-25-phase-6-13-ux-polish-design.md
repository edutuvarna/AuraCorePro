# Phase 6.13 — UX Polish + FE Defense — Design

**Date:** 2026-04-25
**Author:** Brainstormed in collaborative dialogue (Phase 6.12 closeout session)
**Status:** Draft, ready for writing-plans skill in a new session
**Successor to:** [Phase 6.12 Security Hardening (merged at `197275f`)](../../../memory/project_phase_6_item_12_security_hardening_complete.md)

## Goal

Polish the admin-panel UX surface and close the FE defense-in-depth gaps that accumulated as deferred items across Phase 6.11 + 6.12. Six small items, no new features, no architectural rewrites. Theme: every gap a real user has hit (or will hit on first encounter).

## Non-goals

- **No feature expansion** — TOTP backup codes, bulk role change, force-logout-all, audit-log retention dashboard, ASP.NET Core RateLimiter hot-reload all stay in bucket C (Phase 6.14).
- **No CAPTCHA migration** — self-host premium stack stays in bucket B-variant for Phase 6.14+.
- **No major npm dep additions** — Radix UI / shadcn primitives evaluated and rejected for Item 4 (over-investment for one CSS rule).
- **No new email templating engine** — Razor/Scriban deferred (engineering investment too large for polish bucket).

## Scope

Six polish items locked during the brainstorming dialogue:

| # | Item | Approach | Effort |
|---|---|---|---|
| **6.13.1** | Duplicate toast fix | `Connecting` state guard in `signalr.ts` | XS |
| **6.13.2** | RoleChangePage permission gate restore | `useRole() === 'superadmin'` early return + `<LockedTabPlaceholder>` | XS |
| **6.13.3** | Scope-limited FE nav-lock | Hide all tabs except allowed + sticky banner explaining why | M |
| **6.13.4** | Native `<select>` dark styling | `color-scheme: dark` CSS rule | XS |
| **6.13.5** | admin-panel nginx CSP | Strict tailored CSP header in `auracore-admin` server-block | S |
| **6.13.6** | Admin invitation deep-link routing | Hash detection in `page.tsx` `Home` → mount `RedeemInvitationPage` | XS |

Total estimated effort: 3-5 days for a solo dev. Item 3 (scope-limited nav-lock) is the largest because it needs prop plumbing + a new banner component.

## Locked design decisions

These were resolved during the dialogue:

- **D1: Phase theme.** Polish + FE defense-in-depth only. No feature expansion (deferred to Phase 6.14, bucket C).
- **D2: Item 1 — duplicate toast.** Minimal `Connecting` state guard in `startConnection()`. Both `ActivityFeedProvider` and `PermissionNotificationsProvider` continue to call `startConnection` independently; the second mount short-circuits via the new guard. Centralized SignalR provider (Option B) explicitly rejected for scope efficiency — re-evaluate when a third provider needs SignalR.
- **D3: Item 2 — RoleChangePage gate.** Role-based `useRole() === 'superadmin'` early return. NOT permission-based `<PermissionGate permission="tab:rolechange">` because the backend `[Authorize(Roles="superadmin")]` is hard-coded and a permission-grant pattern would be misleading defense (UI shown but every action 403s).
- **D4: Item 3 — scope-limited nav-lock.** Hide all tabs except the single allowed one (Enable 2FA / Change Password / etc.) + sticky banner at top explaining context. Disable-with-lock-icon variant rejected as "information overload" for a transient/forced state. Banner WHY explicit ("Complete 2FA setup to access other features") rather than letting the user infer from the empty sidebar.
- **D5: Item 4 — select styling.** `color-scheme: dark` CSS one-liner. Custom dropdown component (~80-120 lines) and Radix UI `<Select>` (npm dep + ~25-40 KB bundle) both rejected as over-investment for the polish bucket. Trade-off accepted: OS dark scheme colors, not exact brand cyan/purple — sufficient for an internal admin panel.
- **D6: Item 5 — CSP.** Strict tailored CSP for admin-panel with immediate enforcing mode (no report-only window). Phase 6.12 already proved the same pattern works for landing-page. Backup → edit → `nginx -t` → reload → smoke. Rollback via `auracore-admin.bak-pre-csp-{stamp}`.
- **D7: Item 6 — invitation routing.** Hash-based detection in `page.tsx` `Home`. Detect `window.location.hash.startsWith('#/invite')` BEFORE the auth check. If matched, mount `RedeemInvitationPage` directly (it parses its own hash params). No new routing library required; respects the static-export Next.js model.

## Architecture

### Item 1 — `signalr.ts` Connecting guard

File: `admin-panel/src/lib/signalr.ts`

The current `startConnection()` early-returns only on `Connected`. Add a check for `Connecting` so a second concurrent provider mount doesn't overwrite the in-flight connection:

```typescript
export function startConnection() {
  if (!SIGNALR_ENABLED) return;
  if (conn?.state === signalR.HubConnectionState.Connected
      || conn?.state === signalR.HubConnectionState.Connecting) return;  // ← new
  if (!getToken()) return;
  conn = new signalR.HubConnectionBuilder()
    .withUrl(API + "/hubs/admin", { accessTokenFactory: () => getToken() || "" })
    .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
    .configureLogging(signalR.LogLevel.Warning).build();
  Object.keys(L).forEach(k => L[k].forEach(f => conn!.on(k, f as any)));
  conn.start().catch(e => console.warn("SignalR:", e));
}
```

Why this works: `ActivityFeedProvider` mounts first → state goes `Connecting`. `PermissionNotificationsProvider` mounts microseconds later → state still `Connecting` (start hasn't completed) → new guard returns early → no overwrite, no second handler-registration round, no duplicate emission.

### Item 2 — RoleChangePage role-based gate

File: `admin-panel/src/views/RoleChangePage.tsx`

Add at the top of the component (above any other logic):

```tsx
import { useRole } from '@/lib/roleContext';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';

export function RoleChangePage() {
  const role = useRole();
  if (role !== 'superadmin') {
    return <LockedTabPlaceholder
      title="Role Change"
      message="This page is restricted to superadmin role. Backend will reject any role-change action regardless of UI access."
    />;
  }
  // ... existing component body
}
```

`<LockedTabPlaceholder>` already exists from Phase 6.11 T21. Reuse, don't re-implement.

### Item 3 — Scope-limited FE nav-lock

Files:
- Modify: `admin-panel/src/app/AdminPanel.tsx` — add `scope?: 'normal' | '2fa-setup-only' | 'change-password'` prop, scope-aware `groups` computation, banner rendering
- Modify: `admin-panel/src/app/page.tsx` — pass `scope` from `postLoginView` state to `AdminPanelInner`
- Create: `admin-panel/src/components/ScopeLimitedBanner.tsx` — sticky-top banner

```tsx
// AdminPanel.tsx changes (sketch)
interface AdminPanelProps {
  onLogout: () => void;
  role: UserRole;
  initialPage?: Page;
  currentUserEmail?: string;
  scope?: 'normal' | '2fa-setup-only' | 'change-password';  // ← new
}

const SETUP_2FA_GROUPS: NavGroup[] = [
  { title: 'Setup', items: [{ id: 'enable2fa', icon: ShieldCheck, label: 'Enable 2FA' }] },
];

const CHANGE_PW_GROUPS: NavGroup[] = [
  { title: 'Setup', items: [{ id: 'changePw', icon: Key, label: 'Change Password' }] },
];

export function AdminPanelInner({ onLogout, role, initialPage, currentUserEmail, scope = 'normal' }: AdminPanelProps) {
  // ...
  const groups = scope === '2fa-setup-only' ? SETUP_2FA_GROUPS
    : scope === 'change-password' ? CHANGE_PW_GROUPS
    : role === 'superadmin' ? [...ADMIN_NAV_GROUPS, ...SUPERADMIN_EXTRA_GROUPS]
    : ADMIN_NAV_GROUPS;

  return (
    <RoleContext.Provider value={role}>
      <ActivityFeedProvider>
        <PermissionNotificationsProvider>
          <Toaster ... />
          {scope !== 'normal' && <ScopeLimitedBanner scope={scope} onLogout={onLogout} />}
          <div className="flex h-screen overflow-hidden">
            <Sidebar
              groups={groups}
              activePage={page}
              onSelect={(p) => setPage(p as Page)}
              onLogout={onLogout}
              currentUserEmail={email}
              onOpenMyPermissions={scope === 'normal' && role === 'admin' ? () => setPage('myPerms') : undefined}
            />
            <main className="flex-1 overflow-y-auto">
              <div className="max-w-[1400px] mx-auto p-6 lg:p-8 pb-20 md:pb-0"><ActivePage /></div>
            </main>
          </div>
        </PermissionNotificationsProvider>
      </ActivityFeedProvider>
    </RoleContext.Provider>
  );
}
```

```tsx
// ScopeLimitedBanner.tsx (full sketch)
import { ShieldAlert, LogOut } from 'lucide-react';

interface Props {
  scope: '2fa-setup-only' | 'change-password';
  onLogout: () => void;
}

export function ScopeLimitedBanner({ scope, onLogout }: Props) {
  const message = scope === '2fa-setup-only'
    ? 'Complete two-factor authentication setup to access the rest of the panel.'
    : 'Change your password to access the rest of the panel.';
  return (
    <div className="sticky top-0 z-40 bg-amber-500/10 border-b border-amber-500/30 px-4 py-3 flex items-center justify-between">
      <div className="flex items-center gap-2 text-sm text-amber-200">
        <ShieldAlert className="w-4 h-4 shrink-0" />
        <span>{message}</span>
      </div>
      <button onClick={onLogout} className="btn-ghost-sm flex items-center gap-1.5 text-xs">
        <LogOut className="w-3.5 h-3.5" />
        Sign out
      </button>
    </div>
  );
}
```

```tsx
// page.tsx changes (sketch)
return <AdminPanelInner
  role={role}
  onLogout={handleLogout}
  initialPage={postLoginView ?? 'dashboard'}
  scope={postLoginScope}  // ← new state, set alongside postLoginView in onLogin callback
/>;
```

### Item 4 — `color-scheme: dark` CSS

File: `admin-panel/src/app/globals.css`

Add at the top of the file (or near other root-level rules):

```css
:root {
  color-scheme: dark;
}
```

Setting it on `:root` (not just `select`) signals to the entire browser that this app is dark-themed, so OS native UI elements (scrollbars, form controls, dropdown options) all use dark mode rendering. Cleaner than `select { color-scheme: dark; }` only.

### Item 5 — admin-panel nginx CSP

File on origin: `/etc/nginx/sites-enabled/auracore-admin`

Add (or replace if any existing) the `Content-Security-Policy` `add_header` directive inside the `server` block:

```nginx
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://challenges.cloudflare.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: https:; connect-src 'self' https://api.auracore.pro wss://api.auracore.pro; frame-src https://challenges.cloudflare.com; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://api.auracore.pro" always;
```

Rationale per directive:
- `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://challenges.cloudflare.com` — Next.js hydration requires `'unsafe-inline'`; Turnstile WebAssembly requires `'wasm-unsafe-eval'`; Turnstile script from CF.
- `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com` — Tailwind utility classes inline; Google Fonts CSS @import.
- `font-src 'self' https://fonts.gstatic.com` — Google Fonts files.
- `img-src 'self' data: https:` — admin-panel may render data-URI icons + remote thumbnails.
- `connect-src 'self' https://api.auracore.pro wss://api.auracore.pro` — API + SignalR. Turnstile siteverify happens backend-side, not browser, so no CF in connect-src.
- `frame-src https://challenges.cloudflare.com` — Turnstile widget iframe.
- `object-src 'none'` — block plugin embedding.
- `frame-ancestors 'none'` — clickjacking defense.
- `base-uri 'self'` — prevent `<base>` tag attacks.
- `form-action 'self' https://api.auracore.pro` — admin-panel submits to its own API only.

Backup before edit: `cp /etc/nginx/sites-enabled/auracore-admin /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-$(date +%Y%m%d%H%M%S)`. Test: `nginx -t`. Reload: `systemctl reload nginx`. Verify live: `curl -sS -D - https://admin.auracore.pro/ -o /dev/null | grep -i content-security-policy`.

### Item 6 — Invitation deep-link routing

File: `admin-panel/src/app/page.tsx`

Add hash detection BEFORE the existing auth check:

```tsx
import { RedeemInvitationPage } from '@/views/RedeemInvitationPage';

export default function Home() {
  // ...existing state...
  const [redeemInvite, setRedeemInvite] = useState(false);

  useEffect(() => {
    if (typeof window !== 'undefined' && window.location.hash.startsWith('#/invite')) {
      setRedeemInvite(true);
    }
  }, []);

  // ...existing checking + auth flow...

  if (checking) return /* ...existing... */;

  // Phase 6.13.6 — redeem-invitation deep-link, mount RedeemInvitationPage
  // before forcing the user to LoginScreen. The page parses its own hash
  // params and submits to /api/auth/redeem-invitation; on success it
  // redirects to '/' which clears the hash and triggers normal login flow.
  if (redeemInvite && !authenticated) return <RedeemInvitationPage />;

  if (!authenticated) return <LoginScreen onLogin={...} />;

  return <AdminPanelInner ... />;
}
```

`RedeemInvitationPage` already exists from Phase 6.11 T31 (`admin-panel/src/views/RedeemInvitationPage.tsx`). Its `useEffect` parses `window.location.hash` for `?token=...&email=...` — already correct. The fix is solely in `page.tsx` to mount it.

## Testing

| Item | Type | New tests | Notes |
|---|---|---|---|
| 1 | FE unit | 1-2 | `signalr.ts` `Connecting` guard idempotency. Mock signalR.HubConnectionBuilder. Assert second `startConnection()` call during `Connecting` state does not overwrite `conn` reference. |
| 2 | FE unit | 1 | `RoleChangePage` rendered with `RoleContext.Provider value='admin'` → asserts `<LockedTabPlaceholder>` text visible. |
| 3 | FE unit | 2-3 | `AdminPanelForTest` w/ `scope='2fa-setup-only'` → only Enable 2FA tab in Sidebar. Banner rendered. Same for `'change-password'`. Banner not rendered when `scope='normal'`. |
| 4 | Manual smoke | 0 | 5 sayfa, 6 select açılır menüde dark style görünüyor (Chromium + Firefox + Edge). |
| 5 | Manual smoke + curl probe | 0 | `curl -sS -D - https://admin.auracore.pro/ -o /dev/null \| grep -i content-security-policy` returns the new header. Browser smoke: admin login + 2FA + permission flows still work end-to-end. |
| 6 | Manual smoke | 0 | New admin invite → mail link → RedeemInvitationPage → password set → login flow. Negative case: unauth user navigates `https://admin.auracore.pro/` (no hash) → still goes to LoginScreen (no regression). |

Backend test count unchanged (203/203). FE test count: 59 → 62-65 (+3-6 depending on test granularity). Approx total: ~63 FE tests.

## Operational steps

### Deploy plan

Single-phase rollout (no backwards-compat window required since no breaking API change):

1. **admin-panel rebuild + deploy**
   ```bash
   cd admin-panel && npm run build 2>&1 | tail -5 && cd ..
   STAMP=$(date +%Y%m%d%H%M%S)
   ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "cp -a /var/www/admin-panel /var/www/admin-panel.bak-$STAMP"
   scp -i ~/.ssh/id_ed25519 -r admin-panel/out/* root@165.227.170.3:/var/www/admin-panel/
   ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "chown -R deploy:deploy /var/www/admin-panel"
   ```

2. **nginx CSP add (Item 5)**
   ```bash
   ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 "cp /etc/nginx/sites-enabled/auracore-admin /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-$STAMP && # ... edit add CSP ... && nginx -t && systemctl reload nginx"
   ```

3. **Smoke tests (manual + curl)**
   - admin.auracore.pro CSP header live
   - admin login still works (Turnstile still loads)
   - 2FA setup flow + change password flow show scope-limited sidebar + banner
   - Permission request → approve → toast count = 1
   - Native `<select>` opens with dark dropdown
   - Invitation email link opens RedeemInvitationPage

### Rollback path

| Failure | Rollback |
|---|---|
| admin-panel JS bug breaking login | `scp ... admin-panel.bak-{stamp}/* /var/www/admin-panel/` |
| CSP too strict — something blocked | `cp /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-{stamp} /etc/nginx/sites-enabled/auracore-admin && systemctl reload nginx` |
| RoleChangePage breaking superadmin access | Same admin-panel rollback (no backend involvement) |

No DB changes. No env var changes. No backend deploy. Lowest-risk phase since Phase 6.10.

## Future work / out-of-scope carry-forward

Phase 6.14 (bucket C) and beyond:
- **TOTP backup / recovery codes** — backend new entity + endpoint + email + UI. Bound to 2FA UX but feature-tier.
- **Bulk role change** — toggle multiple admins promote/demote in one transaction.
- **Active-session monitor + force-logout-all** — superadmin-side session kill for compromised accounts.
- **ASP.NET Core RateLimiter hot-reload** — Phase 6.12 T37 UI edits persist but pipeline not yet rebuilt on change.
- **Audit-log retention/archival dashboard** — Phase 6.11 T37.1 handles `revoked_tokens` + expired invitations; audit_log retention deferred per spec D9.
- **Self-host CAPTCHA premium stack** (Altcha + FingerprintJS + Spamhaus + behavioral) — Phase 6.14+ if dependency-independence prioritized.
- **Razor-style email templating** (Scriban / Fluid / Razor) — replaces hand-rolled `ApplyPlaceholders`. Engineering investment too large for polish bucket.
- **Centralize SignalR provider** — currently both `ActivityFeedProvider` and `PermissionNotificationsProvider` independently call `startConnection`. The Item 1 guard fix is sufficient for now; centralization to a single `<SignalRProvider>` at AdminPanel root is a cleaner architecture but not justified by current scope (only 2 consumers).
- **Radix UI / shadcn primitive migration** — if more dropdowns / dialogs / tooltips become needed, evaluate adding `@radix-ui/react-*` deps; for now `color-scheme: dark` is sufficient.
- **Custom dropdown component** — for any future select that needs exact brand colors (cyan/purple), build a custom Combobox at that point.
- **CSP report-only mode trial run** — Phase 6.13 ships strict immediate-enforcing CSP. If future CSP changes are riskier, evaluate `Content-Security-Policy-Report-Only` for 1-2 day observation before enforcing.

## Known risks

- **Item 1 fix doesn't repro automatically** — duplicate toast bug only reproduces with two providers mounting in `Connecting` window race. Local dev StrictMode masks the prod-only path. Manual smoke is the gate.
- **Item 3 banner styling collision** — sticky banner at `top-0 z-40` could overlap with future fixed headers. Spec uses `z-40`; ensure no other element claims `z-40+` in admin-panel.
- **Item 4 `color-scheme: dark`** browser support — Chrome 81+, Firefox 96+, Safari 13+. Edge legacy not supported but irrelevant. iOS Safari renders dropdown in OS-native dark when system dark mode active; user's OS setting required for full dark appearance.
- **Item 5 CSP regression risk** — future admin-panel changes that add new external resources (e.g., a new analytics SDK, a new font CDN) require CSP whitelist updates. Document the directive list in repo README or a `docs/ops/` markdown so it's discoverable.
- **Item 6 invitation token leakage in URL hash** — current backend issues raw token via email; the hash-based URL keeps it client-side (browser doesn't send fragment to servers). Acceptable. If at any point the link format changes to a query-param (`?token=`), browser could leak via `Referer` header to embedded resources — current hash-based design avoids that.
- **No backend changes in Phase 6.13** — backend test suite stays at 203/203. If any FE change reveals an unrelated backend bug during smoke, file as a fresh item, do NOT bundle silently into 6.13.

## Self-review notes

- **Placeholders:** none. All sections complete.
- **Internal consistency check:** D2 (Connecting state guard, 1 line) consistent with Item 1 architecture sketch. D3 (role-based gate) consistent with Item 2 sketch using `useRole()`. D4 (Hide + banner) consistent with Item 3 sketch — banner is conditional on `scope !== 'normal'`. D5 (`color-scheme: dark` on `:root`) consistent with Item 4 sketch. D6 (strict CSP) consistent with Item 5 directive list. D7 (hash detection in `page.tsx`) consistent with Item 6 sketch.
- **Scope check:** Single-implementation-plan scope. No further decomposition needed. Six items each with bounded surface; nothing crosses module boundaries unexpectedly.
- **Ambiguity check:** Item 5 form-action whitelist — current design says `'self' https://api.auracore.pro`. Admin-panel forms POST to either `/api/auth/login` (cross-origin) or stay within `admin.auracore.pro` for any future internal forms. Both covered. No ambiguity.

## Continuity note for next session

This spec is committed but **NOT yet handed to writing-plans skill** — user pulled the brake to preserve context budget for a fresh session. Resume command:

```
Read C:\Users\Admin\Desktop\auracorepro\AuraCorePro\docs\superpowers\specs\2026-04-25-phase-6-13-ux-polish-design.md for full design.
Branch: not yet created. Off main HEAD `197275f` (Phase 6.12 merge).
Next step: invoke superpowers:writing-plans skill to convert this spec into the implementation plan.
6 items locked, all approaches decided, no further user-confirmation needed before plan generation.
Test budget projection: backend 203 unchanged, FE 59 → ~63-65.
```
