# Phase 6.13 — UX Polish + FE Defense — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Polish admin-panel UX and close FE defense-in-depth gaps carried forward from Phase 6.11/6.12 via 6 bounded fixes — no architectural rewrites, no feature expansion.

**Architecture:** Six targeted edits across `admin-panel/` (TypeScript / React / Tailwind / Vitest) plus one nginx CSP update on origin `165.227.170.3`. FE-only — backend test suite (203/203) is not touched. New vitest specs add ~3-6 cases; FE total moves from 59 → ~63-65.

**Tech Stack:** Next.js 14 (App Router, static export), React 18, TypeScript 5, Tailwind 3, Vitest 4 + `@testing-library/react` + jsdom, `@microsoft/signalr` 10, nginx (origin).

**Branch:** `phase-6-13-ux-polish` off origin/main (`197275f`, Phase 6.12 close). HEAD will diverge by ~10-12 commits before merge.

**Spec reference:** `docs/superpowers/specs/2026-04-25-phase-6-13-ux-polish-design.md` (committed at `26f897b`).

---

## File map

### Modified
- `admin-panel/src/lib/signalr.ts` — add `Connecting` state guard in `startConnection()`.
- `admin-panel/src/components/LockedTabPlaceholder.tsx` — add optional `staticMessage` mode (no `Request Permission` button, custom copy) so the component is reusable for hard-restricted (role-based) tabs.
- `admin-panel/src/views/RoleChangePage.tsx` — early-return via `<LockedTabPlaceholder staticMessage=...>` when `useRole() !== 'superadmin'`.
- `admin-panel/src/app/AdminPanel.tsx` — accept new `scope?: 'normal' | '2fa-setup-only' | 'change-password'` prop; compute scope-limited `groups`; render `<ScopeLimitedBanner>` when scope ≠ `'normal'`.
- `admin-panel/src/app/page.tsx` — track `postLoginScope` state in parallel with existing `postLoginView`; forward to `AdminPanelInner`. Also: invitation deep-link hash detection mounting `<RedeemInvitationPage>` before LoginScreen.
- `admin-panel/src/app/globals.css` — add `:root { color-scheme: dark; }` at top of base layer.
- `/etc/nginx/sites-enabled/auracore-admin` (origin `165.227.170.3`) — add tailored Content-Security-Policy header.

### Created
- `admin-panel/src/components/ScopeLimitedBanner.tsx` — sticky-top amber banner with explanation copy + sign-out button.
- `admin-panel/src/__tests__/lib/signalr.test.ts` — Connecting-guard idempotency unit test.
- `admin-panel/src/__tests__/views/RoleChangePage.test.tsx` — role-based gate render assertions.
- `admin-panel/src/__tests__/components/ScopeLimitedBanner.test.tsx` — banner copy + sign-out callback unit test.
- `admin-panel/src/__tests__/views/AdminPanelScope.test.tsx` — scope-aware sidebar / banner render assertions.
- `admin-panel/src/__tests__/views/HomeInvitationRouting.test.tsx` — `Home` page hash-routing assertion.
- `docs/ops/admin-panel-csp.md` — operator runbook for the new CSP header (rationale, rollback, future-update protocol).

### Spec / plan divergence note (resolved before task list)

The spec's Item 2 sketch shows `<LockedTabPlaceholder title="Role Change" message="..." />` but the actual `admin-panel/src/components/LockedTabPlaceholder.tsx` exposes `tabName`, `permissionKey`, `onRequestStart`, `hasPending`, `pendingAt`, `lastDenial` — it is hard-wired for the permission-grant request flow. We resolve this by **extending** the component with an optional `staticMessage` prop. When `staticMessage` is supplied:
- The "Request Permission" button is **not** rendered.
- The custom message replaces the default "disabled by the superadmin by default..." copy.
- All existing behaviour (pending banner, denial banner) is preserved when `staticMessage` is omitted.

This keeps the spec's "Reuse, don't re-implement" intent while honouring the existing API. The 4 existing `LockedTabPlaceholder` tests continue to pass unchanged.

---

## Task 0: Branch setup and baseline verification

**Files:**
- (no edits)

- [ ] **Step 0.1: Confirm clean baseline**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git fetch origin
git status
git log -1 --oneline origin/main
```

Expected: `origin/main` HEAD is `26f897b` (spec commit) or `197275f` (Phase 6.12 close — whichever is the latest commit that is NOT a 6.13 plan-doc commit). Working tree may show landing-page dirt + build artefacts; those are unrelated to 6.13 and are not committed by this plan.

- [ ] **Step 0.2: Create feature branch**

```bash
git checkout -b phase-6-13-ux-polish origin/main
git status
```

Expected: `On branch phase-6-13-ux-polish`, clean tree (or pre-existing unrelated dirt).

- [ ] **Step 0.3: Verify FE test baseline**

```bash
cd admin-panel
npm test 2>&1 | tail -20
```

Expected: `Tests` line shows `59 passed` (or current baseline near 59). Capture exact number for end-of-phase delta.

- [ ] **Step 0.4: Verify build passes from clean state**

```bash
cd admin-panel
npm run build 2>&1 | tail -10
```

Expected: `Compiled successfully` line + static export ` ✓ Generating static pages` finishes without error. If it errors, stop — the baseline is broken and must be fixed before any 6.13 edits land on top.

---

## Task 1: Item 6.13.1 — SignalR `Connecting` state guard

**Files:**
- Modify: `admin-panel/src/lib/signalr.ts`
- Create: `admin-panel/src/__tests__/lib/signalr.test.ts`

- [ ] **Step 1.1: Write the failing test**

Create `admin-panel/src/__tests__/lib/signalr.test.ts`:

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock @microsoft/signalr BEFORE importing the module under test.
vi.mock('@microsoft/signalr', () => {
  const states = { Disconnected: 0, Connecting: 1, Connected: 2 };
  const mockBuild = vi.fn();
  class FakeBuilder {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      const conn = {
        state: states.Disconnected,
        start: vi.fn(() => Promise.resolve()),
        stop: vi.fn(() => Promise.resolve()),
        on: vi.fn(),
        off: vi.fn(),
      };
      mockBuild(conn);
      return conn;
    }
  }
  return {
    HubConnectionBuilder: FakeBuilder,
    HubConnectionState: states,
    LogLevel: { Warning: 3 },
    __mockBuild: mockBuild,
  };
});

vi.mock('@/lib/api', () => ({
  getToken: () => 'fake-jwt',
}));

import { startConnection, getConnection, stopConnection } from '@/lib/signalr';
import * as signalRMock from '@microsoft/signalr';

describe('signalr.startConnection — Connecting-state guard', () => {
  beforeEach(() => {
    stopConnection();
    (signalRMock as unknown as { __mockBuild: { mockClear: () => void } }).__mockBuild.mockClear();
  });

  it('does not rebuild the connection when state is Connecting (second concurrent call)', () => {
    startConnection();
    const first = getConnection();
    expect(first).not.toBeNull();
    // Simulate the in-flight Connecting state (start() Promise still pending).
    (first as unknown as { state: number }).state = signalRMock.HubConnectionState.Connecting;

    startConnection(); // second concurrent provider mount
    const second = getConnection();

    expect(second).toBe(first); // same reference, NOT a fresh builder.build()
    const build = (signalRMock as unknown as { __mockBuild: { mock: { calls: unknown[] } } }).__mockBuild;
    expect(build.mock.calls.length).toBe(1);
  });

  it('does not rebuild the connection when state is Connected', () => {
    startConnection();
    const first = getConnection();
    (first as unknown as { state: number }).state = signalRMock.HubConnectionState.Connected;

    startConnection();
    const second = getConnection();

    expect(second).toBe(first);
  });
});
```

- [ ] **Step 1.2: Run test to verify it fails**

```bash
cd admin-panel
npx vitest run src/__tests__/lib/signalr.test.ts 2>&1 | tail -20
```

Expected: Two tests, the **first** ("does not rebuild ... Connecting") FAILS because the current implementation only guards on `Connected`. The second test (Connected) should already PASS.

- [ ] **Step 1.3: Implement the guard**

Edit `admin-panel/src/lib/signalr.ts`. Replace the existing `startConnection` with:

```typescript
export function startConnection(){
if(!SIGNALR_ENABLED)return;
if(conn?.state===signalR.HubConnectionState.Connected
  ||conn?.state===signalR.HubConnectionState.Connecting)return;
if(!getToken())return;
conn=new signalR.HubConnectionBuilder()
.withUrl(API+"/hubs/admin",{accessTokenFactory:()=>getToken()||""})
.withAutomaticReconnect([0,2000,5000,10000,30000])
.configureLogging(signalR.LogLevel.Warning).build();
Object.keys(L).forEach(k=>L[k].forEach(f=>conn!.on(k,f as any)));
conn.start().catch(e=>console.warn("SignalR:",e));}
```

(The only change is the added `||conn?.state===signalR.HubConnectionState.Connecting` clause on line 22.)

- [ ] **Step 1.4: Run test to verify it passes**

```bash
cd admin-panel
npx vitest run src/__tests__/lib/signalr.test.ts 2>&1 | tail -10
```

Expected: Both tests PASS.

- [ ] **Step 1.5: Run full FE test suite — no regression**

```bash
cd admin-panel
npm test 2>&1 | tail -10
```

Expected: previous baseline + 2 new tests. e.g. `61 passed`.

- [ ] **Step 1.6: Commit**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add admin-panel/src/lib/signalr.ts admin-panel/src/__tests__/lib/signalr.test.ts
git commit -m "fix(admin-panel/signalr): guard startConnection on Connecting state to prevent dup-toast (Phase 6.13.1)"
```

---

## Task 2: Item 6.13.2 — RoleChangePage role-based gate (with LockedTabPlaceholder static-mode extension)

**Files:**
- Modify: `admin-panel/src/components/LockedTabPlaceholder.tsx`
- Modify: `admin-panel/src/views/RoleChangePage.tsx`
- Create: `admin-panel/src/__tests__/views/RoleChangePage.test.tsx`

### 2A — Extend LockedTabPlaceholder with static-message mode

- [ ] **Step 2.1: Update existing LockedTabPlaceholder test for forward-compat**

Open `admin-panel/src/__tests__/components/LockedTabPlaceholder.test.tsx` and APPEND a new `it()` block (do NOT modify existing tests):

```typescript
  it('renders staticMessage and hides the request button when staticMessage is provided', () => {
    render(<LockedTabPlaceholder
      tabName="Role Change"
      permissionKey="tab:roleChange"
      staticMessage="This page is restricted to superadmin role."
    />);
    expect(screen.getByText(/restricted to superadmin role/i)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /request permission/i })).toBeNull();
  });
```

- [ ] **Step 2.2: Run new test to verify it fails**

```bash
cd admin-panel
npx vitest run src/__tests__/components/LockedTabPlaceholder.test.tsx 2>&1 | tail -20
```

Expected: 4 existing tests PASS, the 5th new test FAILS — `staticMessage` prop unknown / button still rendered.

- [ ] **Step 2.3: Extend LockedTabPlaceholder**

Replace the entire contents of `admin-panel/src/components/LockedTabPlaceholder.tsx` with:

```tsx
'use client';

import { Lock, Send, Clock } from 'lucide-react';

export interface LockedTabPlaceholderProps {
  tabName: string;
  permissionKey: string;
  onRequestStart?: (key: string) => void;
  hasPending?: boolean;
  pendingAt?: string;
  lastDenial?: { reviewNote?: string | null; reviewedAt: string };
  /**
   * When set, renders a hard-restriction view with this message instead of the
   * default permission-request copy + button. Used by tabs whose backend
   * authorization is hardcoded (e.g. role-based) so a permission-grant UI
   * would mislead the user.
   */
  staticMessage?: string;
}

export function LockedTabPlaceholder({
  tabName, permissionKey, onRequestStart, hasPending, pendingAt, lastDenial, staticMessage,
}: LockedTabPlaceholderProps) {
  return (
    <div className="flex items-center justify-center min-h-[50vh]">
      <div className="max-w-md text-center space-y-6">
        <div className="inline-flex items-center justify-center w-20 h-20 rounded-3xl bg-accent/10 border border-accent/20 mx-auto">
          <Lock className="w-10 h-10 text-accent" />
        </div>
        <div className="space-y-2">
          <h2 className="text-xl font-display font-bold">{tabName} is locked</h2>
          <p className="text-sm text-white/60 leading-relaxed">
            {staticMessage ?? (
              <>This page has been disabled by the superadmin by default. You need permission
              from the superadmin to be able to use the {tabName} tab.</>
            )}
          </p>
        </div>
        {!staticMessage && (
          hasPending ? (
            <div className="flex items-center gap-2 justify-center text-sm text-white/50 bg-white/5 border border-white/10 rounded-xl px-4 py-3">
              <Clock className="w-4 h-4" />
              Pending request from {pendingAt ? new Date(pendingAt).toLocaleString() : 'recently'}, awaiting review.
            </div>
          ) : (
            <button onClick={() => onRequestStart?.(permissionKey)}
              className="btn-primary inline-flex items-center gap-2">
              <Send className="w-4 h-4" />
              Request Permission
            </button>
          )
        )}
        {!staticMessage && lastDenial && (
          <div className="text-xs text-aura-red/80 bg-aura-red/10 border border-aura-red/20 rounded-xl px-4 py-2">
            Last request denied: {lastDenial.reviewNote || 'no reason given'}
          </div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 2.4: Run LockedTabPlaceholder tests to verify all 5 PASS**

```bash
cd admin-panel
npx vitest run src/__tests__/components/LockedTabPlaceholder.test.tsx 2>&1 | tail -10
```

Expected: 5/5 PASS.

### 2B — Apply gate in RoleChangePage

- [ ] **Step 2.5: Write the failing test for RoleChangePage gate**

Create `admin-panel/src/__tests__/views/RoleChangePage.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { RoleContext } from '@/lib/roleContext';
import { RoleChangePage } from '@/views/RoleChangePage';

vi.mock('@/lib/api', () => ({
  api: new Proxy({}, { get: () => () => Promise.resolve(null) }),
}));

describe('RoleChangePage role-based gate', () => {
  it('renders the locked placeholder for admin role with explicit copy', () => {
    render(
      <RoleContext.Provider value="admin">
        <RoleChangePage />
      </RoleContext.Provider>
    );
    expect(screen.getByText(/Role Change is locked/i)).toBeTruthy();
    expect(screen.getByText(/restricted to superadmin role/i)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /request permission/i })).toBeNull();
    expect(screen.queryByPlaceholderText(/User ID/i)).toBeNull();
  });

  it('renders the actual page UI for superadmin role', () => {
    render(
      <RoleContext.Provider value="superadmin">
        <RoleChangePage />
      </RoleContext.Provider>
    );
    expect(screen.getByPlaceholderText(/User ID/i)).toBeTruthy();
    expect(screen.queryByText(/Role Change is locked/i)).toBeNull();
  });
});
```

- [ ] **Step 2.6: Run test to verify both fail**

```bash
cd admin-panel
npx vitest run src/__tests__/views/RoleChangePage.test.tsx 2>&1 | tail -20
```

Expected: First test FAILS (the gate isn't there yet — the page renders inputs unconditionally). Second test passes (superadmin path shows inputs already).

- [ ] **Step 2.7: Apply the gate in RoleChangePage**

Edit `admin-panel/src/views/RoleChangePage.tsx`. Replace the file contents with:

```tsx
'use client';

import { useState } from 'react';
import { ArrowRightLeft } from 'lucide-react';
import { api } from '@/lib/api';
import { CustomTemplatePicker, CustomKey } from '@/components/CustomTemplatePicker';
import { useRole } from '@/lib/roleContext';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';

export function RoleChangePage() {
  const role = useRole();

  const [mode, setMode] = useState<'promote'|'demote'>('promote');
  const [userId, setUserId] = useState('');
  const [template, setTemplate] = useState<'Default'|'Trusted'|'ReadOnly'|'Custom'>('Default');
  const [forcePwd, setForcePwd] = useState<'on_first_login'|'within_7_days'|'within_30_days'|'never'>('on_first_login');
  const [require2fa, setRequire2fa] = useState(true);
  const [customKeys, setCustomKeys] = useState<CustomKey[]>([]);
  const [status, setStatus] = useState<string>('');

  if (role !== 'superadmin') {
    return <LockedTabPlaceholder
      tabName="Role Change"
      permissionKey="tab:roleChange"
      staticMessage="This page is restricted to superadmin role. The backend will reject any role-change action regardless of UI access — the gate exists to prevent misleading 403 responses."
    />;
  }

  const run = async () => {
    setStatus('');
    const ok = mode === 'promote'
      ? (await api.promoteUserToAdmin(userId, { template, forcePasswordChange: forcePwd, require2fa, customKeys: template === 'Custom' ? customKeys : undefined })).ok
      : (await api.demoteAdminToUser(userId)).ok;
    setStatus(ok ? 'Success.' : 'Failed — check user id + role.');
  };

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2"><ArrowRightLeft className="w-6 h-6" />Role Change (single-user)</h1>
      <div className="glass-card p-4 space-y-3">
        <div className="flex gap-2">
          <button onClick={() => setMode('promote')} className={mode==='promote'?'btn-primary':'btn-ghost'}>Promote user → admin</button>
          <button onClick={() => setMode('demote')}  className={mode==='demote' ?'btn-primary':'btn-ghost'}>Demote admin → user</button>
        </div>
        <input value={userId} onChange={e => setUserId(e.target.value)} placeholder="User ID (UUID)" className="input-dark w-full" />
        {mode === 'promote' && (
          <>
            <select value={template} onChange={e => setTemplate(e.target.value as any)} className="input-dark w-full">
              <option value="Default">Default</option>
              <option value="Trusted">Trusted</option>
              <option value="ReadOnly">Read-Only</option>
              <option value="Custom">Custom</option>
            </select>
            {template === 'Custom' && <CustomTemplatePicker onChange={setCustomKeys} />}
            <select value={forcePwd} onChange={e => setForcePwd(e.target.value as any)} className="input-dark w-full">
              <option value="on_first_login">Force change on first login</option>
              <option value="within_7_days">Force change within 7 days</option>
              <option value="within_30_days">Force change within 30 days</option>
              <option value="never">Never</option>
            </select>
            <label className="flex gap-2 text-sm"><input type="checkbox" checked={require2fa} onChange={e => setRequire2fa(e.target.checked)} />Require 2FA</label>
          </>
        )}
        <button onClick={run} className="btn-primary w-full" disabled={!userId}>Apply</button>
        {status && <div className="text-xs text-white/60">{status}</div>}
      </div>
      <p className="text-xs text-white/40">Bulk operations + audit preview deferred to Phase 6.12.</p>
    </div>
  );
}
```

- [ ] **Step 2.8: Run RoleChangePage test — both PASS**

```bash
cd admin-panel
npx vitest run src/__tests__/views/RoleChangePage.test.tsx 2>&1 | tail -10
```

Expected: 2/2 PASS.

- [ ] **Step 2.9: Run full FE suite — no regression**

```bash
cd admin-panel
npm test 2>&1 | tail -10
```

Expected: previous baseline + 3 new tests (1 LockedTabPlaceholder static-mode + 2 RoleChangePage). e.g. `64 passed`.

- [ ] **Step 2.10: Commit**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add admin-panel/src/components/LockedTabPlaceholder.tsx admin-panel/src/views/RoleChangePage.tsx admin-panel/src/__tests__/components/LockedTabPlaceholder.test.tsx admin-panel/src/__tests__/views/RoleChangePage.test.tsx
git commit -m "feat(admin-panel/RoleChangePage): role-based gate via LockedTabPlaceholder staticMessage mode (Phase 6.13.2)"
```

---

## Task 3: Item 6.13.3 — Scope-limited FE nav-lock (banner + scope routing)

This task has three sub-parts. Complete in order.

### 3A — ScopeLimitedBanner component

**Files:**
- Create: `admin-panel/src/components/ScopeLimitedBanner.tsx`
- Create: `admin-panel/src/__tests__/components/ScopeLimitedBanner.test.tsx`

- [ ] **Step 3.1: Write the failing test**

Create `admin-panel/src/__tests__/components/ScopeLimitedBanner.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ScopeLimitedBanner } from '@/components/ScopeLimitedBanner';

describe('ScopeLimitedBanner', () => {
  it('renders 2FA-setup copy when scope=2fa-setup-only', () => {
    render(<ScopeLimitedBanner scope="2fa-setup-only" onLogout={() => {}} />);
    expect(screen.getByText(/two-factor authentication setup/i)).toBeTruthy();
  });

  it('renders password-change copy when scope=change-password', () => {
    render(<ScopeLimitedBanner scope="change-password" onLogout={() => {}} />);
    expect(screen.getByText(/Change your password/i)).toBeTruthy();
  });

  it('invokes onLogout when sign-out button is clicked', () => {
    const onLogout = vi.fn();
    render(<ScopeLimitedBanner scope="2fa-setup-only" onLogout={onLogout} />);
    fireEvent.click(screen.getByRole('button', { name: /sign out/i }));
    expect(onLogout).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 3.2: Run test to verify it fails**

```bash
cd admin-panel
npx vitest run src/__tests__/components/ScopeLimitedBanner.test.tsx 2>&1 | tail -10
```

Expected: 3 tests FAIL — `Cannot find module '@/components/ScopeLimitedBanner'`.

- [ ] **Step 3.3: Create the banner component**

Create `admin-panel/src/components/ScopeLimitedBanner.tsx`:

```tsx
'use client';

import { ShieldAlert, LogOut } from 'lucide-react';

export interface ScopeLimitedBannerProps {
  scope: '2fa-setup-only' | 'change-password';
  onLogout: () => void;
}

export function ScopeLimitedBanner({ scope, onLogout }: ScopeLimitedBannerProps) {
  const message = scope === '2fa-setup-only'
    ? 'Complete two-factor authentication setup to access the rest of the panel.'
    : 'Change your password to access the rest of the panel.';
  return (
    <div className="sticky top-0 z-40 bg-amber-500/10 border-b border-amber-500/30 px-4 py-3 flex items-center justify-between">
      <div className="flex items-center gap-2 text-sm text-amber-200">
        <ShieldAlert className="w-4 h-4 shrink-0" />
        <span>{message}</span>
      </div>
      <button onClick={onLogout} className="btn-ghost flex items-center gap-1.5 text-xs">
        <LogOut className="w-3.5 h-3.5" />
        Sign out
      </button>
    </div>
  );
}
```

- [ ] **Step 3.4: Run test to verify all 3 PASS**

```bash
cd admin-panel
npx vitest run src/__tests__/components/ScopeLimitedBanner.test.tsx 2>&1 | tail -10
```

Expected: 3/3 PASS.

### 3B — AdminPanel `scope` prop + scope-aware groups + banner mount

**Files:**
- Modify: `admin-panel/src/app/AdminPanel.tsx`
- Create: `admin-panel/src/__tests__/views/AdminPanelScope.test.tsx`

- [ ] **Step 3.5: Write the failing tests**

Create `admin-panel/src/__tests__/views/AdminPanelScope.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeAll } from 'vitest';
import { AdminPanelInner } from '@/app/AdminPanel';

vi.mock('@/lib/api', () => ({
  api: new Proxy({}, { get: () => () => Promise.resolve(null) }),
  setToken: () => {},
  getToken: () => 'x',
}));
vi.mock('@/lib/signalr', () => ({
  startConnection: () => {}, stopConnection: () => {}, on: () => {}, off: () => {},
  getConnection: () => null,
}));

beforeAll(() => {
  (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
    observe() {} unobserve() {} disconnect() {}
  };
});

describe('AdminPanelInner scope routing', () => {
  it('scope=2fa-setup-only renders only Enable 2FA tab and the banner', () => {
    render(<AdminPanelInner role="admin" onLogout={() => {}} initialPage="enable2fa" scope="2fa-setup-only" />);
    expect(screen.getAllByText('Enable 2FA').length).toBeGreaterThan(0);
    expect(screen.queryByText('Users')).toBeNull();
    expect(screen.queryByText('Dashboard')).toBeNull();
    expect(screen.getByText(/two-factor authentication setup/i)).toBeTruthy();
  });

  it('scope=change-password renders only Change Password tab and the banner', () => {
    render(<AdminPanelInner role="admin" onLogout={() => {}} initialPage="changePw" scope="change-password" />);
    expect(screen.getAllByText('Change Password').length).toBeGreaterThan(0);
    expect(screen.queryByText('Users')).toBeNull();
    expect(screen.getByText(/Change your password/i)).toBeTruthy();
  });

  it('scope=normal (or omitted) renders the full sidebar and no banner', () => {
    render(<AdminPanelInner role="admin" onLogout={() => {}} />);
    expect(screen.getAllByText('Users').length).toBeGreaterThan(0);
    expect(screen.queryByText(/two-factor authentication setup/i)).toBeNull();
    expect(screen.queryByText(/Change your password/i)).toBeNull();
  });
});
```

- [ ] **Step 3.6: Run test to verify it fails**

```bash
cd admin-panel
npx vitest run src/__tests__/views/AdminPanelScope.test.tsx 2>&1 | tail -20
```

Expected: First two tests FAIL (`scope` prop unknown / full sidebar still rendered / banner not present). Third test passes.

- [ ] **Step 3.7: Update AdminPanel.tsx with scope routing**

Edit `admin-panel/src/app/AdminPanel.tsx`. Apply the following set of edits:

(a) Add the new import at the top of the import block:

```tsx
import { ScopeLimitedBanner } from '@/components/ScopeLimitedBanner';
```

(b) Add the two scope-restricted nav group constants right after `SUPERADMIN_EXTRA_GROUPS`:

```tsx
export const SETUP_2FA_GROUPS: NavGroup[] = [
  { title: 'Setup', items: [{ id: 'enable2fa', icon: ShieldCheck, label: 'Enable 2FA' }] },
];

export const CHANGE_PW_GROUPS: NavGroup[] = [
  { title: 'Setup', items: [{ id: 'changePw', icon: Key, label: 'Change Password' }] },
];
```

(c) Extend the `AdminPanelProps` interface:

```tsx
interface AdminPanelProps {
  onLogout: () => void;
  role: UserRole;
  initialPage?: Page;
  currentUserEmail?: string;
  scope?: 'normal' | '2fa-setup-only' | 'change-password';
}
```

(d) Replace the `AdminPanelInner` function body with the scope-aware version:

```tsx
export function AdminPanelInner({ onLogout, role, initialPage, currentUserEmail, scope = 'normal' }: AdminPanelProps) {
  const [page, setPage] = useState<Page>(initialPage ?? 'dashboard');
  const groups = scope === '2fa-setup-only' ? SETUP_2FA_GROUPS
    : scope === 'change-password' ? CHANGE_PW_GROUPS
    : role === 'superadmin' ? [...ADMIN_NAV_GROUPS, ...SUPERADMIN_EXTRA_GROUPS]
    : ADMIN_NAV_GROUPS;
  const ActivePage = PAGES[page] ?? DashboardPage;
  const email = currentUserEmail ?? decodeEmailFromJwt();
  return (
    <RoleContext.Provider value={role}>
      <ActivityFeedProvider>
        <PermissionNotificationsProvider>
          <Toaster
            position="top-right"
            toastOptions={{
              className: 'glass-card',
              style: {
                background: 'rgba(20,20,24,0.9)',
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.08)',
              },
            }}
          />
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

- [ ] **Step 3.8: Run scope tests — all 3 PASS**

```bash
cd admin-panel
npx vitest run src/__tests__/views/AdminPanelScope.test.tsx 2>&1 | tail -10
```

Expected: 3/3 PASS.

- [ ] **Step 3.9: Run pre-existing NavGroupsByRole test — still PASS (no regression)**

```bash
cd admin-panel
npx vitest run src/__tests__/views/NavGroupsByRole.test.tsx 2>&1 | tail -10
```

Expected: 2/2 PASS — the default `scope='normal'` keeps the existing behaviour intact.

### 3C — page.tsx postLoginScope state plumbing

**Files:**
- Modify: `admin-panel/src/app/page.tsx`

- [ ] **Step 3.10: Wire postLoginScope state in page.tsx**

Edit `admin-panel/src/app/page.tsx`. Apply these edits:

(a) Add state declaration near the other `useState` calls (after `postLoginView`):

```tsx
const [postLoginScope, setPostLoginScope] = useState<'normal' | '2fa-setup-only' | 'change-password'>('normal');
```

(b) Update the `LoginScreen onLogin` callback to capture scope:

```tsx
if (!authenticated) return <LoginScreen onLogin={(r, scope) => {
  setRole(r); setAuthenticated(true); startConnection();
  if (scope === '2fa-setup-only') { setPostLoginView('enable2fa'); setPostLoginScope('2fa-setup-only'); }
  else if (scope === 'change-password') { setPostLoginView('changePw'); setPostLoginScope('change-password'); }
  else setPostLoginScope('normal');
}} />;
```

(c) Forward `scope` to `AdminPanelInner`:

```tsx
return <AdminPanelInner role={role} onLogout={handleLogout} initialPage={postLoginView ?? 'dashboard'} scope={postLoginScope} />;
```

- [ ] **Step 3.11: Re-run scope tests + full FE suite**

```bash
cd admin-panel
npm test 2>&1 | tail -15
```

Expected: previous baseline + 6 new tests (3 ScopeLimitedBanner + 3 AdminPanelScope). e.g. `70 passed`.

- [ ] **Step 3.12: Commit**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add admin-panel/src/components/ScopeLimitedBanner.tsx admin-panel/src/app/AdminPanel.tsx admin-panel/src/app/page.tsx admin-panel/src/__tests__/components/ScopeLimitedBanner.test.tsx admin-panel/src/__tests__/views/AdminPanelScope.test.tsx
git commit -m "feat(admin-panel): scope-limited FE nav-lock with sticky banner (Phase 6.13.3)"
```

---

## Task 4: Item 6.13.4 — `color-scheme: dark` CSS

**Files:**
- Modify: `admin-panel/src/app/globals.css`

This is a CSS-only change. No automated test fits — `color-scheme` is a UA-painting hint that does not surface in jsdom. We rely on the build smoke at end-of-phase + the manual browser smoke list.

- [ ] **Step 4.1: Add the `:root` rule**

Edit `admin-panel/src/app/globals.css`. Insert the following rule **before** the existing `@layer base { body { ... } }` block (between line 5 — the Google Fonts import — and line 7):

```css
:root {
  color-scheme: dark;
}
```

After the edit, the top of the file should read:

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

@import url('https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600&display=swap');

:root {
  color-scheme: dark;
}

@layer base {
  body {
    @apply bg-surface-900 text-white font-body antialiased;
    ...
```

- [ ] **Step 4.2: Verify `npm run build` still compiles**

```bash
cd admin-panel
npm run build 2>&1 | tail -10
```

Expected: `Compiled successfully` + static export passes. No new warnings about `:root`.

- [ ] **Step 4.3: Run full FE test suite — no regression**

```bash
cd admin-panel
npm test 2>&1 | tail -10
```

Expected: same count as Task 3.11 close — no new failures.

- [ ] **Step 4.4: Commit**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add admin-panel/src/app/globals.css
git commit -m "feat(admin-panel/styles): color-scheme dark on :root for native form-control dark rendering (Phase 6.13.4)"
```

---

## Task 5: Item 6.13.6 — Invitation deep-link routing in `page.tsx`

**Files:**
- Modify: `admin-panel/src/app/page.tsx`
- Create: `admin-panel/src/__tests__/views/HomeInvitationRouting.test.tsx`

(Item 6 is sequenced before Item 5 — CSP — because Item 5 is the deploy step that goes after all FE edits land.)

- [ ] **Step 5.1: Write the failing test**

Create `admin-panel/src/__tests__/views/HomeInvitationRouting.test.tsx`:

```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: new Proxy({}, { get: () => () => Promise.resolve(null) }),
  setToken: () => {},
  getToken: () => null,
}));
vi.mock('@/lib/signalr', () => ({
  startConnection: () => {}, stopConnection: () => {},
}));

import Home from '@/app/page';

describe('Home page invitation deep-link routing', () => {
  beforeEach(() => {
    localStorage.clear();
    // jsdom default — no token, unauth.
  });

  it('mounts RedeemInvitationPage when location.hash starts with #/invite', async () => {
    window.location.hash = '#/invite?token=abc&email=foo%40bar.com';
    render(<Home />);
    await waitFor(() => {
      expect(screen.getByText(/Welcome! Set your password/i)).toBeTruthy();
    });
    window.location.hash = '';
  });

  it('mounts LoginScreen when no hash and no token', async () => {
    window.location.hash = '';
    render(<Home />);
    await waitFor(() => {
      expect(screen.getByText(/Administration Console/i)).toBeTruthy();
    });
  });
});
```

- [ ] **Step 5.2: Run test to verify it fails**

```bash
cd admin-panel
npx vitest run src/__tests__/views/HomeInvitationRouting.test.tsx 2>&1 | tail -20
```

Expected: First test FAILS (no hash detection — falls through to LoginScreen). Second passes.

- [ ] **Step 5.3: Add hash detection to `page.tsx`**

Edit `admin-panel/src/app/page.tsx`. Apply these edits:

(a) Add the import for `RedeemInvitationPage` near the existing imports:

```tsx
import { RedeemInvitationPage } from '@/views/RedeemInvitationPage';
```

(b) Add a new state hook + useEffect right after the existing `useState` declarations and BEFORE the existing `useEffect`:

```tsx
const [redeemInvite, setRedeemInvite] = useState(false);

useEffect(() => {
  if (typeof window !== 'undefined' && window.location.hash.startsWith('#/invite')) {
    setRedeemInvite(true);
  }
}, []);
```

(c) Insert a new branch BETWEEN the `if (checking) return ...` block and the `if (!authenticated) return <LoginScreen ...` block:

```tsx
// Phase 6.13.6 — invitation deep-link. Mount RedeemInvitationPage before
// LoginScreen so an unauthenticated visitor with the invite hash lands on
// the password-set form. RedeemInvitationPage parses its own hash params
// and assigns location='/' on success, which clears the hash and triggers
// the normal authenticated render path.
if (redeemInvite && !authenticated) return <RedeemInvitationPage />;
```

- [ ] **Step 5.4: Run test to verify both PASS**

```bash
cd admin-panel
npx vitest run src/__tests__/views/HomeInvitationRouting.test.tsx 2>&1 | tail -10
```

Expected: 2/2 PASS.

- [ ] **Step 5.5: Run full FE suite — no regression**

```bash
cd admin-panel
npm test 2>&1 | tail -10
```

Expected: previous baseline + 2 new tests. Cumulative from Task 0 baseline: +8 (2 signalr + 1 LockedTabPlaceholder + 2 RoleChange + 3 ScopeBanner + 3 AdminPanelScope + 2 HomeInvitationRouting = 13) — but some tests share files / total sweeps may differ. Confirm no regressions versus the baseline captured in Task 0.3.

- [ ] **Step 5.6: Confirm npm build still passes**

```bash
cd admin-panel
npm run build 2>&1 | tail -10
```

Expected: `Compiled successfully`.

- [ ] **Step 5.7: Commit**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add admin-panel/src/app/page.tsx admin-panel/src/__tests__/views/HomeInvitationRouting.test.tsx
git commit -m "feat(admin-panel/Home): mount RedeemInvitationPage on #/invite hash before LoginScreen (Phase 6.13.6)"
```

---

## Task 6: Build the admin-panel and stage for deploy

**Files:**
- (no source edits)

- [ ] **Step 6.1: Production build**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro/admin-panel
npm run build 2>&1 | tail -20
```

Expected: `Compiled successfully` + ` ✓ Generating static pages (N/N)`. Output lands in `admin-panel/out/`.

- [ ] **Step 6.2: Sanity-check the build artefact**

```bash
ls /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro/admin-panel/out/ | head -20
```

Expected: `index.html`, `_next/`, and any other static pages. The `out/` directory must be non-empty.

(No commit — the build artefacts are not tracked.)

---

## Task 7: Deploy admin-panel to origin (`165.227.170.3`)

**Files:**
- Origin: `/var/www/admin-panel/` (SCP target).

This task is **operational** — it touches the production origin. The user should be alerted before each `scp` or `ssh` action.

- [ ] **Step 7.1: Confirm SSH connectivity**

```bash
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "echo connected; hostname; date"
```

Expected: `connected\n<hostname>\n<UTC date>`.

If this prompts for a password or returns "Permission denied", **stop** and ask the user to verify the SSH key path and `~/.ssh/authorized_keys` on the origin.

- [ ] **Step 7.2: Capture timestamp + back up live admin-panel directory**

```bash
STAMP=$(date +%Y%m%d%H%M%S)
echo "Backup stamp: $STAMP"
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "cp -a /var/www/admin-panel /var/www/admin-panel.bak-$STAMP && ls -d /var/www/admin-panel.bak-$STAMP"
```

Expected: backup directory listed. Save `$STAMP` for the rollback path in the runbook.

- [ ] **Step 7.3: SCP the new build**

```bash
scp -i /c/Users/Admin/.ssh/id_ed25519 -r /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro/admin-panel/out/* root@165.227.170.3:/var/www/admin-panel/
```

Expected: bytes transferred, no errors.

- [ ] **Step 7.4: Restore ownership**

```bash
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "chown -R deploy:deploy /var/www/admin-panel && ls -la /var/www/admin-panel/index.html"
```

Expected: `deploy deploy` ownership on `index.html`.

- [ ] **Step 7.5: HTTP smoke**

```bash
curl -sS -o /dev/null -w "%{http_code}\n" https://admin.auracore.pro/
```

Expected: `200`.

---

## Task 8: Item 6.13.5 — Add tailored CSP header to admin-panel nginx server block

**Files:**
- Origin: `/etc/nginx/sites-enabled/auracore-admin`
- Create (in repo): `docs/ops/admin-panel-csp.md` — operator runbook.

- [ ] **Step 8.1: Inspect current nginx config**

```bash
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "cat /etc/nginx/sites-enabled/auracore-admin"
```

Expected: the `server { ... }` block for `admin.auracore.pro` with no existing `Content-Security-Policy` `add_header` directive (or, if one exists, its current value — captured for the runbook).

- [ ] **Step 8.2: Backup the config**

```bash
STAMP=$(date +%Y%m%d%H%M%S)
echo "CSP backup stamp: $STAMP"
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "cp /etc/nginx/sites-enabled/auracore-admin /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-$STAMP && ls -la /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-$STAMP"
```

Expected: backup file listed.

- [ ] **Step 8.3: Add the CSP header inside the `server` block**

Use `ssh ... "sed -i ..."` or an in-place edit. The header to add:

```
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://challenges.cloudflare.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: https:; connect-src 'self' https://api.auracore.pro wss://api.auracore.pro; frame-src https://challenges.cloudflare.com; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://api.auracore.pro" always;
```

Recommended approach — fetch the file locally, edit, and push back:

```bash
scp -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3:/etc/nginx/sites-enabled/auracore-admin /tmp/auracore-admin.conf
# Edit /tmp/auracore-admin.conf — insert the add_header line above inside the
# `server { ... }` block, near the other security headers if they exist
# (HSTS / X-Content-Type-Options / Referrer-Policy / etc.).
# Then push back:
scp -i /c/Users/Admin/.ssh/id_ed25519 /tmp/auracore-admin.conf root@165.227.170.3:/etc/nginx/sites-enabled/auracore-admin
```

The `add_header` directive must include the trailing `always;` so the header is sent on non-200 responses too (e.g. 404 from the static export's missing-page handling).

- [ ] **Step 8.4: nginx -t**

```bash
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "nginx -t"
```

Expected: `nginx: configuration file /etc/nginx/nginx.conf test is successful`.

If `nginx -t` errors, **stop** and roll back via the backup from Step 8.2 before continuing:

```bash
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "cp /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-<STAMP> /etc/nginx/sites-enabled/auracore-admin && nginx -t"
```

- [ ] **Step 8.5: Reload nginx**

```bash
ssh -i /c/Users/Admin/.ssh/id_ed25519 root@165.227.170.3 "systemctl reload nginx && systemctl status nginx --no-pager | head -10"
```

Expected: `Active: active (running)` and recent `reloaded` message.

- [ ] **Step 8.6: Verify the header is live**

```bash
curl -sS -D - https://admin.auracore.pro/ -o /dev/null | grep -i content-security-policy
```

Expected: a single `content-security-policy:` response line containing all the directives from Step 8.3.

- [ ] **Step 8.7: Verify Turnstile + login still work (live browser smoke)**

Open `https://admin.auracore.pro/` in a fresh browser session:

- DevTools Console: no CSP `Refused to load`/`Refused to execute` warnings.
- Turnstile widget renders (CF iframe loaded under `frame-src`).
- Sign in as admin → main panel loads → Sidebar renders → no console CSP errors.
- DevTools Network: API calls to `api.auracore.pro` succeed; SignalR `wss://api.auracore.pro/hubs/admin` connects.

If any blocked resource appears in the console, capture it before rolling back, decide whether the directive needs widening, and re-apply.

- [ ] **Step 8.8: Document the CSP in the repo**

Create `docs/ops/admin-panel-csp.md`:

```markdown
# admin-panel CSP — operator runbook (Phase 6.13.5)

Live as of 2026-04-25. Server block: `/etc/nginx/sites-enabled/auracore-admin`.

## Current header

```
add_header Content-Security-Policy "default-src 'self'; script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://challenges.cloudflare.com; style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; font-src 'self' https://fonts.gstatic.com; img-src 'self' data: https:; connect-src 'self' https://api.auracore.pro wss://api.auracore.pro; frame-src https://challenges.cloudflare.com; object-src 'none'; frame-ancestors 'none'; base-uri 'self'; form-action 'self' https://api.auracore.pro" always;
```

## Directive rationale

| Directive | Why |
|---|---|
| `default-src 'self'` | Default-deny any origin not explicitly listed below. |
| `script-src 'self' 'unsafe-inline' 'wasm-unsafe-eval' https://challenges.cloudflare.com` | Next.js hydration injects inline scripts (`'unsafe-inline'`); Turnstile uses WebAssembly (`'wasm-unsafe-eval'`); the Turnstile JS bundle is served from `challenges.cloudflare.com`. |
| `style-src 'self' 'unsafe-inline' https://fonts.googleapis.com` | Tailwind utilities resolve to inline `<style>` injections; Google Fonts CSS is fetched at runtime. |
| `font-src 'self' https://fonts.gstatic.com` | Outfit + JetBrains Mono font files. |
| `img-src 'self' data: https:` | Avatar / icon `data:` URIs + remote thumbnails (e.g. update screenshots). |
| `connect-src 'self' https://api.auracore.pro wss://api.auracore.pro` | XHR + SignalR hub. Turnstile siteverify happens server-side, so CF is NOT in this list. |
| `frame-src https://challenges.cloudflare.com` | Turnstile widget iframe. |
| `object-src 'none'` | Block all plugin embeds. |
| `frame-ancestors 'none'` | Clickjacking defense. |
| `base-uri 'self'` | Block injected `<base>` tag attacks. |
| `form-action 'self' https://api.auracore.pro` | admin-panel forms only POST to its own API. |

## Updating the header

When the admin-panel adds a new external dependency (analytics SDK, font CDN, video host, etc.), the operator MUST update this file and the nginx directive in lockstep:

1. Identify the directive that needs widening.
2. `cp /etc/nginx/sites-enabled/auracore-admin /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-$(date +%Y%m%d%H%M%S)`
3. Edit the directive, save.
4. `nginx -t` → on success, `systemctl reload nginx`.
5. Verify live: `curl -sS -D - https://admin.auracore.pro/ -o /dev/null | grep -i content-security-policy`.
6. Browser smoke (DevTools Console must be CSP-warning-free).
7. Update this `admin-panel-csp.md` file and commit.

## Rollback path

```bash
ssh -i ~/.ssh/id_ed25519 root@165.227.170.3 \
  "cp /etc/nginx/sites-enabled/auracore-admin.bak-pre-csp-<STAMP> /etc/nginx/sites-enabled/auracore-admin \
   && nginx -t && systemctl reload nginx"
```

The pre-Phase-6.13 backup stamp is captured during the deploy session and noted in the merge commit message.
```

- [ ] **Step 8.9: Commit the runbook**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git add docs/ops/admin-panel-csp.md
git commit -m "docs(ops): admin-panel CSP runbook (Phase 6.13.5)"
```

---

## Task 9: End-to-end manual smoke (live origin)

**Files:**
- (no edits)

This task captures the user-visible verification from the spec's `## Testing` section. Each item is a checkbox; the executor should record outcomes in the merge commit message.

- [ ] **Step 9.1: Item 6.13.1 dup-toast smoke**

1. Open `https://admin.auracore.pro/` in Chrome with an empty `localStorage`.
2. Sign in (admin) → wait for SignalR connection (DevTools Network → `/hubs/admin?...` shows status 101 Switching Protocols).
3. From a superadmin session in another browser/profile, **approve a permission request** for the test admin so a `PermissionApproved` event fires.
4. Confirm the admin sees **exactly one** toast ("Permission granted" or similar). No duplicate.

Expected: 1 toast. If 2 toasts appear, the guard isn't applied — re-check `signalr.ts` change and rebuild.

- [ ] **Step 9.2: Item 6.13.2 RoleChangePage gate smoke**

1. Sign in as **admin** (not superadmin).
2. Navigate to `/?#` and inspect the sidebar. The `Role Change` entry **must NOT appear** (it's already a SUPERADMIN_EXTRA_GROUPS entry, so this is a sanity check).
3. **Manually** force-render the RoleChangePage by typing into DevTools console: `window.history.pushState({},'','#'); document.querySelector('button[data-page="roleChange"]')?.click();` — or simpler: temporarily change `role` in dev tools localStorage payload to admin and try clicking the entry. If no entry exists, this item's frontend gate is moot for the admin role and only the unit test verifies it. Mark as covered by Task 2 unit tests.
4. Sign in as **superadmin** → click `Role Change` → confirm the original page (with User-ID input) renders.

Expected: gate fires for admin, page renders for superadmin.

- [ ] **Step 9.3: Item 6.13.3 scope-limited nav-lock smoke**

1. Set up a test admin account in DB with `requiresPasswordChange=true` (or use an existing one).
2. Sign in → after the password-step submit, the post-login redirect should land on the `Change Password` page with **only `Change Password`** in the sidebar and the **amber banner** at the top reading "Change your password to access the rest of the panel."
3. The banner's "Sign out" button should log out and return to LoginScreen.
4. Repeat for an account with `requiresTwoFactorSetup=true` → should land on `Enable 2FA` with banner "Complete two-factor authentication setup..."

Expected: scope-limited sidebar (1 entry), banner present, sign-out works.

- [ ] **Step 9.4: Item 6.13.4 dark `<select>` smoke**

1. Sign in as superadmin → navigate to `Role Change`.
2. Click the `Default` dropdown. The native `<select>` popup should render with **dark background + light text** (OS dark scheme).
3. Repeat in Firefox + Edge (Chromium).

Expected: dropdown is dark in all 3 browsers (assuming Chrome 81+/Firefox 96+/Edge Chromium-based — all current as of 2026-04-25).

- [ ] **Step 9.5: Item 6.13.5 CSP smoke (re-confirm)**

```bash
curl -sS -D - https://admin.auracore.pro/ -o /dev/null | grep -i content-security-policy
```

Expected: header line as in Task 8.6.

Plus DevTools Console with admin signed in: 0 CSP-Refused warnings. SignalR Network tab shows `wss://api.auracore.pro/hubs/admin` connected. Turnstile renders.

- [ ] **Step 9.6: Item 6.13.6 invitation deep-link smoke**

1. As superadmin, open `Invitations` → create a new admin invitation (test email).
2. Open the test inbox; copy the invitation link.
3. Paste the link into a fresh browser session (no `aura_token` in localStorage). The link should look like `https://admin.auracore.pro/#/invite?token=...&email=...`.
4. The browser should land on **the password-set form** (`Welcome! Set your password.`) — NOT the LoginScreen.
5. Set a password (≥10 chars) → submit → token saved → redirect to `/`. After redirect, the panel renders normally.
6. **Negative case:** Open `https://admin.auracore.pro/` (no hash) in an unauth session. The browser should land on LoginScreen (no regression).

Expected: hash → RedeemInvitationPage; no hash → LoginScreen.

---

## Task 10: Final verification, push, and merge prep

**Files:**
- (no edits)

- [ ] **Step 10.1: Final test run with full output**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro/admin-panel
npm test 2>&1 | tail -30
```

Expected: All passing. Capture the final test count for the merge-commit body.

Compare to the Task 0.3 baseline. Delta should be `+8` to `+13` new passing tests (depending on how many shared-file tests rolled into the same file vs. new files).

Spec target: `59 → 63-65`. Plan target: `≥63`.

- [ ] **Step 10.2: Final build**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro/admin-panel
npm run build 2>&1 | tail -10
```

Expected: `Compiled successfully`.

- [ ] **Step 10.3: Confirm no uncommitted source changes**

```bash
cd /c/Users/Admin/Desktop/AuraCorePro/AuraCorePro
git status
```

Expected: clean tree (or only the unrelated landing-page / build-artefact dirt that pre-existed Task 0). All Phase 6.13 source edits are committed.

- [ ] **Step 10.4: Push the feature branch to origin**

```bash
git push -u origin phase-6-13-ux-polish
```

Expected: branch published to `origin/phase-6-13-ux-polish`.

- [ ] **Step 10.5: User-confirmation checkpoint — merge to main**

**STOP. Do NOT merge to main without user approval.**

Print the merge plan for the user:

```
Phase 6.13 ready for merge:
  Branch: phase-6-13-ux-polish
  Commits: <count> ahead of main
  FE tests: <baseline> → <final> (+<delta>)
  Backend tests: 203 unchanged (no backend touch)
  Live origin state: admin-panel deployed at <STAMP>, CSP header live
Pattern: --no-ff merge to main, ceremonial commit
```

Wait for user approval. On approval, execute:

```bash
git checkout main
git pull origin main
git merge --no-ff phase-6-13-ux-polish -m "Phase 6.13 — UX Polish + FE Defense (merge)

6 items shipped:
  6.13.1 SignalR Connecting-state guard (dup-toast fix)
  6.13.2 RoleChangePage role-based gate (LockedTabPlaceholder staticMessage mode)
  6.13.3 Scope-limited FE nav-lock + amber banner
  6.13.4 color-scheme: dark for native form-control dark rendering
  6.13.5 admin-panel nginx CSP (live; runbook in docs/ops/)
  6.13.6 invitation deep-link routing (#/invite hash detection in Home)

FE tests: <baseline> → <final> (+<delta>)
Backend: 203/203 unchanged."
git push origin main
```

Then ceremonial commit (empty marker for the phase close):

```bash
git commit --allow-empty -m "Phase 6.13 closed — UX polish + FE defense complete"
git push origin main
```

- [ ] **Step 10.6: Update memory index**

Update `C:\Users\Admin\.claude\projects\C--\memory\MEMORY.md` to mark Phase 6.13 complete + add the close memory file pointer. (See `superpowers:finishing-a-development-branch` skill if applicable.)

---

## Self-review log

**Spec coverage:**
- Item 6.13.1 → Task 1 ✅
- Item 6.13.2 → Task 2 ✅ (with documented LockedTabPlaceholder extension)
- Item 6.13.3 → Task 3 (3A banner, 3B AdminPanel scope routing, 3C page.tsx state) ✅
- Item 6.13.4 → Task 4 ✅
- Item 6.13.5 → Task 8 (deferred until after deploy in Task 7) ✅
- Item 6.13.6 → Task 5 ✅

**Placeholder scan:** None. Every step has exact code or exact command.

**Type consistency:**
- `scope` literal type identical in `LoginScreen.tsx` (`'normal' | '2fa-setup-only' | 'change-password'`), new `AdminPanelProps.scope`, new `ScopeLimitedBannerProps.scope`, new `postLoginScope` state in `page.tsx`.
- `staticMessage?: string` in `LockedTabPlaceholder.tsx` consistent with usage in `RoleChangePage.tsx`.
- `Page` enum type from `AdminPanel.tsx` already includes `'enable2fa'` and `'changePw'` — no extension needed.
- `useRole()` hook signature matches existing usage in `lib/roleContext.ts`.

**Sequencing rationale:** Items are ordered FE-edit-only (Tasks 1-5) → build (Task 6) → deploy (Task 7) → CSP (Task 8) → smoke (Task 9) → merge (Task 10). The CSP add lands AFTER the new build is on the origin so any new external resource the build pulls in will be visible during CSP verification.

**Risk acknowledgements (per spec § Known risks):**
- Item 1 dup-toast does not auto-repro in unit tests; manual smoke (Step 9.1) is the gate.
- Item 5 CSP regression risk on future external-resource additions — mitigated by `docs/ops/admin-panel-csp.md` runbook.
- Item 4 `color-scheme: dark` browser support is universal among supported browsers (Chrome 81+/Firefox 96+/Safari 13+).
- No backend changes — backend test count must remain `203/203`. If any FE smoke surfaces a backend bug, file a fresh item.
