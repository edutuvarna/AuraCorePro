# Phase 6.15 — Mobile Polish + Web Cleanup Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close two carry-forward streams in one phase — mobile Tier-0 UX correctness (single biometric prompt + JWT refresh-token rotation) and web Tier-3 deferred features (bulk role change, RateLimiter hot-reload, audit-log retention).

**Architecture:** Five items across mobile (RN/Expo), backend (ASP.NET Core), and admin-panel (Next.js). Items 1+2 are mobile-only and chain (item 2 depends on item 1's cache). Items 3-5 are independent backend + admin-panel work. Each item ships TDD-first with its own test suite, then a single end-of-phase deploy.

**Tech Stack:**
- **Mobile:** React Native 0.81, Expo SDK 54, expo-secure-store 15, expo-local-authentication 17, Jest 29 + jest-expo + @testing-library/react-native
- **Backend:** ASP.NET Core, EF Core (Npgsql), xUnit 2.9 + in-memory provider for tests
- **Admin-panel:** Next.js 14, React 18, Vitest 4 + @testing-library/react + jsdom

**Branch off:** `main` at HEAD `a33b60e` (Phase 6.14 close + 6.15 spec landed). Create `phase-6-15-mobile-polish-web-cleanup`.

**Scope correction from spec exploration:**
- Backend currently has **no built-in `AddRateLimiter()`/`UseRateLimiter()` wired** in Program.cs. We are building rate limiting greenfield, not replacing. No regression risk vs. the built-in middleware.
- Existing service is `IRateLimitConfigService` (spec said `IRateLimitPoliciesService`). Use real name throughout.
- `audit_log.CreatedAt` has no single-column index (only composite `(ActorId, CreatedAt)` and `(Action, CreatedAt)`). Retention `DELETE` will need an index migration.
- `PermissionKeys.ActionUsersPromote/Demote` are NOT declared. Bulk endpoints will rely on the controller-level superadmin authorization (matches existing per-row promote/demote pattern in AdminManagementController).

---

## Task 0: Branch setup

**Files:**
- None (git only)

- [ ] **Step 1: Create and switch to phase branch**

Run from repo root `C:\Users\Admin\Desktop\AuraCorePro\AuraCorePro`:

```bash
git switch main
git pull --ff-only
git switch -c phase-6-15-mobile-polish-web-cleanup
```

Expected: `Switched to a new branch 'phase-6-15-mobile-polish-web-cleanup'`. HEAD remains at `a33b60e`.

- [ ] **Step 2: Verify clean working tree**

Run: `git status`
Expected: `nothing to commit, working tree clean`

---

## Task 1: Mobile JWT cache — secureStore module changes (6.15.1.A)

**Files:**
- Modify: `mobile/src/lib/secureStore.ts`
- Test: `mobile/__tests__/lib/secureStore.test.ts` (extend existing)

**Goal:** Drop `requireAuthentication: true` from JWT + refresh-token writes. Add module-level cache with explicit setters/getters/clear. `clearAuth()` also clears cache.

- [ ] **Step 1: Write failing tests for cache + non-auth-protected writes**

Append to `mobile/__tests__/lib/secureStore.test.ts`:

```typescript
import * as SecureStore from 'expo-secure-store';
import {
  setJwt, getJwt, clearAuth, setLastActiveAt, getLastActiveAt, isInactiveBeyondLimit,
  setRefreshToken, getRefreshToken,
  setCachedJwt, getCachedJwt, setCachedRefreshToken, getCachedRefreshToken, clearAuthCache,
} from '@/lib/secureStore';

describe('secureStore — Phase 6.15 cache + single-gate', () => {
  beforeEach(async () => {
    await clearAuth();
    clearAuthCache();
    jest.clearAllMocks();
  });

  it('setJwt does NOT pass requireAuthentication: true (single-gate biometric)', async () => {
    await setJwt('header.payload.sig');
    const setItemMock = SecureStore.setItemAsync as jest.Mock;
    const lastCall = setItemMock.mock.calls.find((c) => c[0] === 'aura_jwt');
    expect(lastCall).toBeDefined();
    // Either no options arg (undefined) or options without requireAuthentication: true
    const opts = lastCall![2];
    expect(opts?.requireAuthentication).not.toBe(true);
  });

  it('setRefreshToken does NOT pass requireAuthentication: true', async () => {
    await setRefreshToken('refresh-token-value');
    const setItemMock = SecureStore.setItemAsync as jest.Mock;
    const lastCall = setItemMock.mock.calls.find((c) => c[0] === 'aura_refresh');
    expect(lastCall).toBeDefined();
    const opts = lastCall![2];
    expect(opts?.requireAuthentication).not.toBe(true);
  });

  it('cache roundtrips JWT in memory', () => {
    setCachedJwt('cached.jwt.value');
    expect(getCachedJwt()).toBe('cached.jwt.value');
  });

  it('cache roundtrips refresh token in memory', () => {
    setCachedRefreshToken('cached.refresh.value');
    expect(getCachedRefreshToken()).toBe('cached.refresh.value');
  });

  it('clearAuthCache resets both cache slots', () => {
    setCachedJwt('a'); setCachedRefreshToken('b');
    clearAuthCache();
    expect(getCachedJwt()).toBeNull();
    expect(getCachedRefreshToken()).toBeNull();
  });

  it('clearAuth also clears in-memory cache', async () => {
    setCachedJwt('a'); setCachedRefreshToken('b');
    await clearAuth();
    expect(getCachedJwt()).toBeNull();
    expect(getCachedRefreshToken()).toBeNull();
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run from `mobile/`:
```bash
npm test -- secureStore
```

Expected: TypeScript errors `setCachedJwt is not exported` and/or test failures.

- [ ] **Step 3: Replace `mobile/src/lib/secureStore.ts` with cache + single-gate version**

```typescript
import * as SecureStore from 'expo-secure-store';

const KEY_JWT = 'aura_jwt';
const KEY_REFRESH = 'aura_refresh';
const KEY_LAST_ACTIVE = 'aura_last_active';

// Phase 6.15.1: in-memory cache populated by AuthProvider after the single
// biometric unlock. All request paths read from the cache, so SecureStore
// reads (and any biometric prompt) happen exactly once per app session.
let cachedJwt: string | null = null;
let cachedRefresh: string | null = null;

export function setCachedJwt(token: string | null): void {
  cachedJwt = token;
}

export function getCachedJwt(): string | null {
  return cachedJwt;
}

export function setCachedRefreshToken(token: string | null): void {
  cachedRefresh = token;
}

export function getCachedRefreshToken(): string | null {
  return cachedRefresh;
}

export function clearAuthCache(): void {
  cachedJwt = null;
  cachedRefresh = null;
}

// Phase 6.15.1: requireAuthentication dropped on writes. Single-gate biometric
// happens via expo-local-authentication in AuthProvider mount; the OS Keystore
// master key still encrypts at rest. Threat model: physical-device-theft +
// screen-unlock-bypass exposes both the SecureStore item and the in-memory
// cache equivalently — no marginal security loss vs. double-gate.
export async function setJwt(token: string) {
  await SecureStore.setItemAsync(KEY_JWT, token);
}

export async function getJwt(): Promise<string | null> {
  return SecureStore.getItemAsync(KEY_JWT);
}

export async function setRefreshToken(token: string) {
  await SecureStore.setItemAsync(KEY_REFRESH, token);
}

export async function getRefreshToken(): Promise<string | null> {
  return SecureStore.getItemAsync(KEY_REFRESH);
}

export async function setLastActiveAt(epochMs: number) {
  await SecureStore.setItemAsync(KEY_LAST_ACTIVE, String(epochMs));
}

export async function getLastActiveAt(): Promise<number | null> {
  const v = await SecureStore.getItemAsync(KEY_LAST_ACTIVE);
  return v ? Number(v) : null;
}

export async function isInactiveBeyondLimit(days: number): Promise<boolean> {
  const last = await getLastActiveAt();
  if (last == null) return true;
  const ageMs = Date.now() - last;
  return ageMs > days * 24 * 60 * 60 * 1000;
}

export async function clearAuth() {
  clearAuthCache();
  await SecureStore.deleteItemAsync(KEY_JWT);
  await SecureStore.deleteItemAsync(KEY_REFRESH);
  await SecureStore.deleteItemAsync(KEY_LAST_ACTIVE);
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `npm test -- secureStore`
Expected: all secureStore tests green (existing 5 + new 6 = 11 passing).

- [ ] **Step 5: Commit**

```bash
git add mobile/src/lib/secureStore.ts mobile/__tests__/lib/secureStore.test.ts
git commit -m "phase-6.15.1: mobile secureStore cache + drop requireAuthentication on writes"
```

---

## Task 2: Mobile single-gate biometric — auth.ts + authContext.tsx wiring (6.15.1.B)

**Files:**
- Modify: `mobile/src/lib/auth.ts`
- Modify: `mobile/src/lib/authContext.tsx`
- Test: `mobile/__tests__/lib/auth.test.ts` (new)

**Goal:** `loadAuthFromStore` becomes side-effect-free SecureStore read (no biometric). AuthProvider mount calls `tryBiometricUnlock` ONCE and on success populates the cache. `logout` clears cache + secureStore.

- [ ] **Step 1: Write failing tests for auth.ts cache integration**

Create `mobile/__tests__/lib/auth.test.ts`:

```typescript
import * as LocalAuthentication from 'expo-local-authentication';
import * as SecureStore from 'expo-secure-store';
import {
  persistLoginSuccess, logout, loadAuthFromStore, hydrateCacheFromStore,
} from '@/lib/auth';
import {
  getCachedJwt, getCachedRefreshToken, clearAuthCache, setCachedJwt, setCachedRefreshToken,
  clearAuth,
} from '@/lib/secureStore';

describe('auth.ts — Phase 6.15 cache integration', () => {
  beforeEach(async () => {
    await clearAuth();
    clearAuthCache();
    jest.clearAllMocks();
    (LocalAuthentication.hasHardwareAsync as jest.Mock).mockResolvedValue(true);
    (LocalAuthentication.isEnrolledAsync as jest.Mock).mockResolvedValue(true);
    (LocalAuthentication.authenticateAsync as jest.Mock).mockResolvedValue({ success: true });
  });

  it('persistLoginSuccess writes both SecureStore AND populates cache', async () => {
    await persistLoginSuccess('access.token.abc', 'refresh.token.xyz');
    expect(getCachedJwt()).toBe('access.token.abc');
    expect(getCachedRefreshToken()).toBe('refresh.token.xyz');
  });

  it('logout clears cache AND SecureStore', async () => {
    setCachedJwt('a'); setCachedRefreshToken('b');
    await SecureStore.setItemAsync('aura_jwt', 'a');
    await SecureStore.setItemAsync('aura_refresh', 'b');
    await logout();
    expect(getCachedJwt()).toBeNull();
    expect(getCachedRefreshToken()).toBeNull();
    expect(await SecureStore.getItemAsync('aura_jwt')).toBeNull();
    expect(await SecureStore.getItemAsync('aura_refresh')).toBeNull();
  });

  it('loadAuthFromStore does NOT call biometric (side-effect-free read)', async () => {
    await SecureStore.setItemAsync('aura_jwt', 'header.eyJyb2xlIjoiYWRtaW4ifQ.sig');
    await SecureStore.setItemAsync('aura_last_active', String(Date.now()));
    const result = await loadAuthFromStore();
    expect(result).not.toBeNull();
    expect(LocalAuthentication.authenticateAsync).not.toHaveBeenCalled();
  });

  it('hydrateCacheFromStore reads SecureStore into cache (post-biometric)', async () => {
    await SecureStore.setItemAsync('aura_jwt', 'jwt.value');
    await SecureStore.setItemAsync('aura_refresh', 'refresh.value');
    await hydrateCacheFromStore();
    expect(getCachedJwt()).toBe('jwt.value');
    expect(getCachedRefreshToken()).toBe('refresh.value');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run from `mobile/`: `npm test -- auth.test`
Expected: failures — `hydrateCacheFromStore is not a function`, missing cache wiring.

- [ ] **Step 3: Replace `mobile/src/lib/auth.ts`**

```typescript
import * as LocalAuthentication from 'expo-local-authentication';
import {
  setJwt, setRefreshToken, setLastActiveAt, clearAuth, getJwt, getRefreshToken,
  isInactiveBeyondLimit,
  setCachedJwt, setCachedRefreshToken, clearAuthCache,
} from './secureStore';
import { unregisterPush } from './notifications';

export type Role = 'admin' | 'superadmin';

export interface AuthState {
  authenticated: boolean;
  role: Role;
  jwt: string | null;
}

export const INACTIVITY_LIMIT_DAYS = 30;

export function decodeRoleFromJwt(token: string | null): Role {
  try {
    if (!token) return 'admin';
    const parts = token.split('.');
    if (parts.length < 2) return 'admin';
    const payload = JSON.parse(atob(parts[1]));
    const claim = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    const roles: string[] = Array.isArray(claim) ? claim : [claim].filter(Boolean);
    if (roles.includes('superadmin')) return 'superadmin';
    if (roles.includes('admin')) return 'admin';
  } catch {}
  return 'admin';
}

export async function tryBiometricUnlock(): Promise<boolean> {
  const hardware = await LocalAuthentication.hasHardwareAsync();
  const enrolled = await LocalAuthentication.isEnrolledAsync();
  if (!hardware || !enrolled) return false;
  const res = await LocalAuthentication.authenticateAsync({
    promptMessage: 'Unlock AuraCore Admin',
    fallbackLabel: 'Use password',
    disableDeviceFallback: false,
  });
  return res.success;
}

/**
 * Side-effect-free read: returns AuthState if a JWT exists and the user
 * hasn't been inactive past the limit. Does NOT touch biometric — that's
 * AuthProvider's job (single-gate). After this returns truthy, AuthProvider
 * runs the biometric prompt, then calls hydrateCacheFromStore.
 */
export async function loadAuthFromStore(): Promise<AuthState | null> {
  if (await isInactiveBeyondLimit(INACTIVITY_LIMIT_DAYS)) {
    await clearAuth();
    return null;
  }
  const jwt = await getJwt();
  if (!jwt) return null;
  return { authenticated: true, role: decodeRoleFromJwt(jwt), jwt };
}

/**
 * Phase 6.15.1: called by AuthProvider after a successful biometric unlock.
 * Pulls JWT + refresh from SecureStore into the in-memory cache so the
 * `request()` path never touches SecureStore again this session.
 */
export async function hydrateCacheFromStore(): Promise<void> {
  const [jwt, refresh] = await Promise.all([getJwt(), getRefreshToken()]);
  setCachedJwt(jwt);
  setCachedRefreshToken(refresh);
}

export async function persistLoginSuccess(accessToken: string, refreshToken: string) {
  await setJwt(accessToken);
  await setRefreshToken(refreshToken);
  await setLastActiveAt(Date.now());
  // Phase 6.15.1: populate cache immediately so the next request() reads the
  // new token without round-tripping SecureStore.
  setCachedJwt(accessToken);
  setCachedRefreshToken(refreshToken);
}

export async function logout() {
  try { await unregisterPush(); } catch {}
  clearAuthCache();
  await clearAuth();
}
```

- [ ] **Step 4: Replace `mobile/src/lib/authContext.tsx` mount logic**

```typescript
import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import {
  loadAuthFromStore, tryBiometricUnlock, hydrateCacheFromStore, AuthState,
} from './auth';
import { clearAuthCache } from './secureStore';
import { clearAuth } from './secureStore';

interface AuthCtx {
  auth: AuthState | null;
  checking: boolean;
  setAuth: (a: AuthState | null) => void;
}

const AuthContext = createContext<AuthCtx>({
  auth: null,
  checking: true,
  setAuth: () => {},
});

/**
 * Phase 6.15.1: Single-gate biometric.
 *
 * Flow on cold start:
 *   1. loadAuthFromStore — side-effect-free SecureStore read, no biometric
 *   2. If no JWT or stale → checking=false, Index renders <Redirect href="/(auth)/login" />
 *   3. If JWT present → tryBiometricUnlock (THE single biometric prompt)
 *   4. On success → hydrateCacheFromStore + setAuth → Index renders /(app)
 *   5. On 3-fail/cancel → clearAuth + clearAuthCache + checking=false → /(auth)/login
 *
 * Subsequent api.request() calls use getCachedJwt() — no SecureStore touch,
 * no biometric prompt. Tab switching, refreshes, pull-to-refresh = 0 prompts.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthState | null>(null);
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const cached = await loadAuthFromStore();
        if (cancelled) return;
        if (!cached) {
          setChecking(false);
          return;
        }
        const ok = await tryBiometricUnlock();
        if (cancelled) return;
        if (!ok) {
          await clearAuth();
          clearAuthCache();
          setChecking(false);
          return;
        }
        await hydrateCacheFromStore();
        if (cancelled) return;
        setAuth(cached);
        setChecking(false);
      } catch (e) {
        console.warn('Auth load failed:', e);
        if (!cancelled) setChecking(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  return <AuthContext.Provider value={{ auth, checking, setAuth }}>{children}</AuthContext.Provider>;
}

export const useAuth = () => useContext(AuthContext);
```

- [ ] **Step 5: Run tests to verify they pass**

Run from `mobile/`:
```bash
npm test -- auth.test secureStore
```
Expected: all auth.ts tests pass + secureStore unchanged green.

- [ ] **Step 6: Commit**

```bash
git add mobile/src/lib/auth.ts mobile/src/lib/authContext.tsx mobile/__tests__/lib/auth.test.ts
git commit -m "phase-6.15.1: single-gate biometric + cache hydration in AuthProvider"
```

---

## Task 3: Mobile api.ts — read JWT from cache (6.15.1.C)

**Files:**
- Modify: `mobile/src/lib/api.ts`
- Test: `mobile/__tests__/lib/api.test.ts` (new)

**Goal:** `request()` reads token via `getCachedJwt()` (synchronous, no SecureStore round-trip, no biometric prompt). Login/superadminLogin paths unchanged.

- [ ] **Step 1: Write failing test**

Create `mobile/__tests__/lib/api.test.ts`:

```typescript
import { setCachedJwt, clearAuthCache } from '@/lib/secureStore';
import { api } from '@/lib/api';

const fetchMock = jest.fn();
global.fetch = fetchMock as any;

describe('api.request — Phase 6.15 cache-driven token read', () => {
  beforeEach(() => {
    clearAuthCache();
    fetchMock.mockReset();
    fetchMock.mockResolvedValue({ ok: true, status: 200, json: async () => ({}) } as any);
  });

  it('reads JWT from in-memory cache, not SecureStore', async () => {
    setCachedJwt('cached.bearer.token');
    await api.getStats();
    expect(fetchMock).toHaveBeenCalledTimes(1);
    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers.Authorization).toBe('Bearer cached.bearer.token');
  });

  it('omits Authorization header when cache is empty', async () => {
    await api.getStats();
    const [, init] = fetchMock.mock.calls[0];
    expect(init.headers.Authorization).toBeUndefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run from `mobile/`: `npm test -- api.test`
Expected: failures — current `request()` calls `await getJwt()`, returns null without explicit cache write, Authorization absent OR test fails because token came from SecureStore mock not cache.

- [ ] **Step 3: Patch `request()` in `mobile/src/lib/api.ts`**

Replace the existing `request()` function only (keep all method definitions unchanged):

```typescript
import Constants from 'expo-constants';
import { getCachedJwt } from './secureStore';

const API = (Constants.expoConfig?.extra as any)?.apiUrl
  ?? process.env.EXPO_PUBLIC_API_URL
  ?? 'https://api.auracore.pro';

const MOBILE_CLIENT_SECRET = (Constants.expoConfig?.extra as any)?.mobileClientSecret
  ?? process.env.EXPO_PUBLIC_MOBILE_CLIENT_SECRET
  ?? '';

// Phase 6.15.1: synchronous cache read replaces the previous `await getJwt()`.
// AuthProvider populates the cache after the single biometric unlock; login/
// refresh paths populate it via persistLoginSuccess and tryRefreshToken.
async function request(path: string, init: RequestInit = {}) {
  const token = getCachedJwt();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init.headers as Record<string, string>) ?? {}),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (MOBILE_CLIENT_SECRET) headers['X-Auracore-Mobile-Client'] = MOBILE_CLIENT_SECRET;
  const res = await fetch(`${API}${path}`, { ...init, headers });
  return res;
}

async function safeJson(res: Response) {
  try { return await res.json(); } catch { return null; }
}
```

The `import { getJwt }` in the file must be replaced with `import { getCachedJwt }` and uses removed.

- [ ] **Step 4: Run mobile tests**

```bash
npm test
```
Expected: all mobile tests green. The login.test.tsx and dashboard.test.tsx must continue to pass (they go through `persistLoginSuccess` which now also writes the cache).

- [ ] **Step 5: Commit**

```bash
git add mobile/src/lib/api.ts mobile/__tests__/lib/api.test.ts
git commit -m "phase-6.15.1: mobile api.request reads JWT from in-memory cache"
```

---

## Task 4: Mobile JWT refresh on 401 (6.15.2)

**Files:**
- Modify: `mobile/src/lib/api.ts` (add 401 interceptor + single-flight)
- Modify: `mobile/src/lib/auth.ts` (add `tryRefreshToken` + auth-failure callback hook)
- Modify: `mobile/src/lib/authContext.tsx` (register auth-failure callback that triggers logout)
- Add: `mobile/__tests__/lib/api-refresh.test.ts`

**Goal:** Any non-`/auth/*` request that gets 401 → call `/api/auth/refresh` ONCE (single-flight) → on success retry the original request with the new token; on failure trigger logout via a callback that AuthContext registered at mount.

- [ ] **Step 1: Write failing tests**

Create `mobile/__tests__/lib/api-refresh.test.ts`:

```typescript
import { setCachedJwt, setCachedRefreshToken, clearAuthCache, getCachedJwt } from '@/lib/secureStore';
import { api } from '@/lib/api';
import { setOnAuthFailure } from '@/lib/auth';

const fetchMock = jest.fn();
global.fetch = fetchMock as any;

describe('api refresh-on-401 — Phase 6.15.2', () => {
  beforeEach(() => {
    clearAuthCache();
    fetchMock.mockReset();
    setOnAuthFailure(() => {});
  });

  it('401 → refresh OK → retry with new token → success', async () => {
    setCachedJwt('expired.jwt');
    setCachedRefreshToken('refresh.value');

    fetchMock
      // first call: stats with expired JWT → 401
      .mockResolvedValueOnce({ ok: false, status: 401, json: async () => ({}) } as any)
      // refresh call: returns new tokens
      .mockResolvedValueOnce({
        ok: true, status: 200,
        json: async () => ({ accessToken: 'new.jwt', refreshToken: 'new.refresh' }),
      } as any)
      // retried stats call: now ok
      .mockResolvedValueOnce({ ok: true, status: 200, json: async () => ({ totalUsers: 5 }) } as any);

    const result = await api.getStats();
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(getCachedJwt()).toBe('new.jwt');
    expect(result).toEqual({ totalUsers: 5 });

    // Retry request used the new token
    const [, retryInit] = fetchMock.mock.calls[2];
    expect(retryInit.headers.Authorization).toBe('Bearer new.jwt');
  });

  it('401 → refresh 401 → no infinite loop, fires onAuthFailure', async () => {
    setCachedJwt('expired.jwt');
    setCachedRefreshToken('also.expired');

    const onFail = jest.fn();
    setOnAuthFailure(onFail);

    fetchMock
      .mockResolvedValueOnce({ ok: false, status: 401, json: async () => ({}) } as any)
      .mockResolvedValueOnce({ ok: false, status: 401, json: async () => ({}) } as any);

    const res = await api.getStats();
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(onFail).toHaveBeenCalledTimes(1);
    // getStats helper returns null on res.ok === false
    expect(res).toBeNull();
  });

  it('concurrent 401s share a single refresh', async () => {
    setCachedJwt('expired.jwt');
    setCachedRefreshToken('refresh.value');

    let refreshResolve!: (v: any) => void;
    const refreshPromise = new Promise((resolve) => { refreshResolve = resolve; });

    fetchMock.mockImplementation(async (url: string) => {
      if (typeof url === 'string' && url.includes('/api/auth/refresh')) {
        return refreshPromise;
      }
      // For non-refresh endpoints: first hit returns 401, second hit (after refresh) returns 200
      const token = (fetchMock.mock.calls.find((c) => c[1]?.headers?.Authorization)?.[1].headers.Authorization);
      if (token === 'Bearer expired.jwt') {
        return { ok: false, status: 401, json: async () => ({}) };
      }
      return { ok: true, status: 200, json: async () => ({}) };
    });

    const calls = Promise.all([api.getStats(), api.getStats(), api.getStats()]);

    // Resolve refresh
    setTimeout(() => refreshResolve({
      ok: true, status: 200,
      json: async () => ({ accessToken: 'fresh.jwt', refreshToken: 'fresh.refresh' }),
    } as any), 10);

    await calls;

    const refreshCalls = fetchMock.mock.calls.filter(([u]) => typeof u === 'string' && u.includes('/api/auth/refresh'));
    expect(refreshCalls.length).toBe(1);
    expect(getCachedJwt()).toBe('fresh.jwt');
  });

  it('does not refresh on 401 from /api/auth/* endpoints', async () => {
    setCachedJwt('jwt');
    setCachedRefreshToken('refresh');
    fetchMock.mockResolvedValueOnce({ ok: false, status: 401, json: async () => ({ error: 'Invalid creds' }) } as any);

    const res = await api.login('x@y.test', 'wrongpw');
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(res.ok).toBe(false);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `npm test -- api-refresh`
Expected: `setOnAuthFailure is not a function` and absence of refresh logic causes failures.

- [ ] **Step 3: Add `tryRefreshToken` + onAuthFailure to `mobile/src/lib/auth.ts`**

Append to the file (do NOT remove existing exports):

```typescript
import Constants from 'expo-constants';

const API_FOR_REFRESH = (Constants.expoConfig?.extra as any)?.apiUrl
  ?? process.env.EXPO_PUBLIC_API_URL
  ?? 'https://api.auracore.pro';

const MOBILE_SECRET = (Constants.expoConfig?.extra as any)?.mobileClientSecret
  ?? process.env.EXPO_PUBLIC_MOBILE_CLIENT_SECRET
  ?? '';

// Phase 6.15.2: AuthProvider registers a callback so api.ts can trigger
// logout when refresh fails (refresh-token expired or rotated by another
// session). Default no-op so unit tests don't crash on missing handler.
let onAuthFailure: () => void = () => {};

export function setOnAuthFailure(cb: () => void): void {
  onAuthFailure = cb;
}

export function fireAuthFailure(): void {
  try { onAuthFailure(); } catch (e) { console.warn('onAuthFailure handler threw:', e); }
}

import { getCachedRefreshToken } from './secureStore';

export async function tryRefreshToken(): Promise<boolean> {
  const refresh = getCachedRefreshToken();
  if (!refresh) return false;
  try {
    const res = await fetch(`${API_FOR_REFRESH}/api/auth/refresh`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(MOBILE_SECRET ? { 'X-Auracore-Mobile-Client': MOBILE_SECRET } : {}),
      },
      body: JSON.stringify({ refreshToken: refresh }),
    });
    if (!res.ok) return false;
    const data = await res.json().catch(() => null);
    if (!data?.accessToken || !data?.refreshToken) return false;
    await persistLoginSuccess(data.accessToken, data.refreshToken);
    return true;
  } catch {
    return false;
  }
}
```

- [ ] **Step 4: Patch `mobile/src/lib/api.ts` with single-flight refresh + 401 interceptor**

Replace the `request()` function with this version (keep method bodies unchanged):

```typescript
import { getCachedJwt } from './secureStore';
import { tryRefreshToken, fireAuthFailure } from './auth';

// Phase 6.15.2: single-flight refresh. Concurrent 401s during the refresh
// window share the same in-flight promise so the refresh-token isn't burned
// multiple times.
let inFlightRefresh: Promise<boolean> | null = null;

async function ensureRefresh(): Promise<boolean> {
  if (inFlightRefresh) return inFlightRefresh;
  inFlightRefresh = (async () => {
    try { return await tryRefreshToken(); }
    finally { inFlightRefresh = null; }
  })();
  return inFlightRefresh;
}

async function request(path: string, init: RequestInit = {}, isRetry = false): Promise<Response> {
  const token = getCachedJwt();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init.headers as Record<string, string>) ?? {}),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (MOBILE_CLIENT_SECRET) headers['X-Auracore-Mobile-Client'] = MOBILE_CLIENT_SECRET;
  const res = await fetch(`${API}${path}`, { ...init, headers });

  // Phase 6.15.2: lazy refresh on 401. Skip /api/auth/* paths (login itself
  // returning 401 means bad creds, not stale token). Skip retries to avoid
  // infinite loops when refresh succeeds but the retried request also 401s.
  if (res.status === 401 && !isRetry && !path.startsWith('/api/auth/')) {
    const refreshed = await ensureRefresh();
    if (refreshed) {
      return request(path, init, true);
    }
    // Refresh failed → trigger logout via AuthProvider's registered callback.
    fireAuthFailure();
  }
  return res;
}
```

- [ ] **Step 5: Wire `setOnAuthFailure` in AuthProvider**

In `mobile/src/lib/authContext.tsx`, add inside the `useEffect`'s async block, after the imports:

```typescript
import { setOnAuthFailure } from './auth';
// inside AuthProvider, before the IIFE:
useEffect(() => {
  setOnAuthFailure(() => {
    // Refresh failed: logout + force navigation back to login on next render.
    // We can't call router.replace here (would race with AuthContext); instead,
    // null out auth and let Index dispatcher's <Redirect /> handle the route.
    setAuth(null);
  });
}, []);
```

Place this `useEffect` BEFORE the existing mount effect (so the callback is registered before any request can fire).

- [ ] **Step 6: Run all mobile tests**

```bash
npm test
```
Expected: all green.

- [ ] **Step 7: Commit**

```bash
git add mobile/src/lib/api.ts mobile/src/lib/auth.ts mobile/src/lib/authContext.tsx mobile/__tests__/lib/api-refresh.test.ts
git commit -m "phase-6.15.2: mobile JWT refresh on 401 with single-flight + auth-failure callback"
```

---

## Task 5: Backend custom rate limiter — token-bucket service (6.15.3.A)

**Files:**
- Create: `src/Backend/AuraCore.API.Infrastructure/RateLimiting/TokenBucketLimiter.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/RateLimiting/IAuraCoreRateLimiter.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/RateLimiting/AuraCoreRateLimiter.cs`
- Test: `tests/AuraCore.Tests.API/Phase615/CustomRateLimiterTests.cs`

**Goal:** Singleton service that resolves a policy by name (via cached `IRateLimitConfigService`), maintains a `ConcurrentDictionary<(policy,key), bucket>` of token buckets, and exposes `TryAcquire(policy, bucketKey, out retryAfter)`.

- [ ] **Step 1: Write the failing tests**

Create `tests/AuraCore.Tests.API/Phase615/CustomRateLimiterTests.cs`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Application.Services.RateLimiting;
using AuraCore.API.Infrastructure.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class CustomRateLimiterTests
{
    private static (IAuraCoreRateLimiter limiter, FakePolicies policies) Build()
    {
        var policies = new FakePolicies();
        var limiter = new AuraCoreRateLimiter(new ServiceCollection()
            .AddSingleton<IRateLimitConfigService>(policies)
            .BuildServiceProvider());
        return (limiter, policies);
    }

    [Fact]
    public async Task TryAcquire_AllowsUpToBudget_ThenBlocks()
    {
        var (limiter, policies) = Build();
        policies.Set("auth.login", 5, 1800);

        for (int i = 0; i < 5; i++)
        {
            var (ok, _) = await limiter.TryAcquireAsync("auth.login", "1.2.3.4", CancellationToken.None);
            Assert.True(ok, $"req {i} should be allowed");
        }
        var (sixth, retry) = await limiter.TryAcquireAsync("auth.login", "1.2.3.4", CancellationToken.None);
        Assert.False(sixth);
        Assert.True(retry > System.TimeSpan.Zero);
    }

    [Fact]
    public async Task TryAcquire_DifferentBucketKeys_AreIndependent()
    {
        var (limiter, policies) = Build();
        policies.Set("auth.login", 2, 1800);

        Assert.True((await limiter.TryAcquireAsync("auth.login", "1.1.1.1", default)).Allowed);
        Assert.True((await limiter.TryAcquireAsync("auth.login", "1.1.1.1", default)).Allowed);
        Assert.False((await limiter.TryAcquireAsync("auth.login", "1.1.1.1", default)).Allowed);
        // Different IP — own bucket — fresh budget
        Assert.True((await limiter.TryAcquireAsync("auth.login", "2.2.2.2", default)).Allowed);
    }

    [Fact]
    public async Task TryAcquire_PolicyChange_TakesEffectImmediately()
    {
        var (limiter, policies) = Build();
        policies.Set("auth.login", 1, 1800);
        Assert.True((await limiter.TryAcquireAsync("auth.login", "ip1", default)).Allowed);
        Assert.False((await limiter.TryAcquireAsync("auth.login", "ip1", default)).Allowed);

        // Operator increases budget via UI → service invalidates cache
        policies.Set("auth.login", 10, 1800);

        // Next call must see the new policy (refilled by elapsed time + new max)
        // We expect at least one more allowance immediately because 10 tokens
        // > 1 (current bucket cap) → bucket clamps up to new max.
        Assert.True((await limiter.TryAcquireAsync("auth.login", "ip1", default)).Allowed);
    }

    [Fact]
    public async Task TryAcquire_UnknownPolicy_AllowsAndDoesNotThrow()
    {
        var (limiter, _) = Build();
        var (ok, _) = await limiter.TryAcquireAsync("unknown.policy", "ip", default);
        Assert.True(ok);
    }

    [Fact]
    public async Task TryAcquire_ConcurrentSameBucket_TotalDoesNotExceedBudget()
    {
        var (limiter, policies) = Build();
        policies.Set("admin.all", 100, 60);

        var allowed = 0;
        await Parallel.ForAsync(0, 200, async (_, ct) =>
        {
            var (ok, _) = await limiter.TryAcquireAsync("admin.all", "ip-x", ct);
            if (ok) System.Threading.Interlocked.Increment(ref allowed);
        });
        Assert.True(allowed <= 100, $"expected ≤ 100 allowed, got {allowed}");
        Assert.True(allowed >= 95, $"expected most allowed near budget, got {allowed}");
    }

    private sealed class FakePolicies : IRateLimitConfigService
    {
        private readonly System.Collections.Generic.Dictionary<string, RateLimitPolicy> _policies = new();
        public void Set(string name, int requests, int windowSeconds) =>
            _policies[name] = new RateLimitPolicy(requests, windowSeconds);
        public Task<System.Collections.Generic.IReadOnlyDictionary<string, RateLimitPolicy>> GetAllAsync(CancellationToken ct = default) =>
            Task.FromResult<System.Collections.Generic.IReadOnlyDictionary<string, RateLimitPolicy>>(_policies);
        public Task UpdateAsync(string name, RateLimitPolicy policy, System.Guid? actorId, CancellationToken ct = default)
        { Set(name, policy.Requests, policy.WindowSeconds); return Task.CompletedTask; }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run from repo root:
```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~CustomRateLimiterTests
```
Expected: build failure — `IAuraCoreRateLimiter` and `AuraCoreRateLimiter` do not exist.

- [ ] **Step 3: Create `src/Backend/AuraCore.API.Infrastructure/RateLimiting/IAuraCoreRateLimiter.cs`**

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AuraCore.API.Infrastructure.RateLimiting;

/// <summary>
/// Phase 6.15.3 — token-bucket rate limiter that pulls policy parameters from
/// the cached IRateLimitConfigService. Operator UI edits invalidate the cache;
/// the next TryAcquireAsync call sees the new policy immediately.
/// </summary>
public interface IAuraCoreRateLimiter
{
    Task<RateLimitResult> TryAcquireAsync(string policyName, string bucketKey, CancellationToken ct);
}

public readonly record struct RateLimitResult(bool Allowed, TimeSpan RetryAfter);
```

- [ ] **Step 4: Create `src/Backend/AuraCore.API.Infrastructure/RateLimiting/TokenBucketLimiter.cs`**

```csharp
using System;
using System.Threading;

namespace AuraCore.API.Infrastructure.RateLimiting;

/// <summary>
/// Single token bucket. Atomically refills based on elapsed monotonic time and
/// the current policy parameters. Lock-free reads, lightweight lock for refill.
/// </summary>
internal sealed class TokenBucketState
{
    private readonly object _gate = new();
    private double _tokens;
    private long _lastRefillTicks;

    public TokenBucketState(double initialTokens, long nowTicks)
    {
        _tokens = initialTokens;
        _lastRefillTicks = nowTicks;
    }

    /// <summary>
    /// Try to consume 1 token. Returns (allowed, retryAfter). retryAfter is
    /// computed from the deficit + refill rate when the request is blocked.
    /// </summary>
    public RateLimitResult TryConsume(int budget, int windowSeconds, long nowTicks)
    {
        if (budget <= 0 || windowSeconds <= 0) return new RateLimitResult(true, TimeSpan.Zero);

        lock (_gate)
        {
            // Refill based on elapsed time. Tokens accumulate at budget/windowSeconds per second.
            var elapsedSeconds = (double)(nowTicks - _lastRefillTicks) / TimeSpan.TicksPerSecond;
            if (elapsedSeconds > 0)
            {
                var refillRatePerSecond = (double)budget / windowSeconds;
                _tokens = Math.Min(budget, _tokens + elapsedSeconds * refillRatePerSecond);
                _lastRefillTicks = nowTicks;
            }
            else if (_tokens > budget)
            {
                // Policy budget shrank below current bucket — clamp down.
                _tokens = budget;
            }

            if (_tokens >= 1)
            {
                _tokens -= 1;
                return new RateLimitResult(true, TimeSpan.Zero);
            }

            // Compute retry-after: how long until 1 token regenerates.
            var deficit = 1 - _tokens;
            var refillRate = (double)budget / windowSeconds;
            var retrySeconds = deficit / refillRate;
            return new RateLimitResult(false, TimeSpan.FromSeconds(retrySeconds));
        }
    }
}
```

- [ ] **Step 5: Create `src/Backend/AuraCore.API.Infrastructure/RateLimiting/AuraCoreRateLimiter.cs`**

```csharp
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Application.Services.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace AuraCore.API.Infrastructure.RateLimiting;

public sealed class AuraCoreRateLimiter : IAuraCoreRateLimiter
{
    private readonly IServiceProvider _root;
    private readonly ConcurrentDictionary<(string policy, string key), TokenBucketState> _buckets = new();

    public AuraCoreRateLimiter(IServiceProvider root)
    {
        _root = root;
    }

    public async Task<RateLimitResult> TryAcquireAsync(string policyName, string bucketKey, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(policyName)) return new RateLimitResult(true, TimeSpan.Zero);

        // Resolve current policy from cached service. UI edits invalidate the
        // cache → next call here sees the new policy. 5-min TTL is the
        // defensive safety net only.
        using var scope = _root.CreateScope();
        var cfg = scope.ServiceProvider.GetRequiredService<IRateLimitConfigService>();
        var all = await cfg.GetAllAsync(ct);
        if (!all.TryGetValue(policyName, out var policy))
        {
            // Unknown policy → fail-open (behaves as if no limit configured).
            // Operator should declare via UI; we don't 429 because of a typo.
            return new RateLimitResult(true, TimeSpan.Zero);
        }

        var bucket = _buckets.GetOrAdd(
            (policyName, bucketKey),
            _ => new TokenBucketState(policy.Requests, Environment.TickCount64 * (TimeSpan.TicksPerMillisecond)));
        return bucket.TryConsume(policy.Requests, policy.WindowSeconds, Environment.TickCount64 * TimeSpan.TicksPerMillisecond);
    }
}
```

- [ ] **Step 6: Run tests to verify they pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~CustomRateLimiterTests
```
Expected: 5 tests passing.

- [ ] **Step 7: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/RateLimiting/ tests/AuraCore.Tests.API/Phase615/CustomRateLimiterTests.cs
git commit -m "phase-6.15.3: AuraCoreRateLimiter token-bucket service + tests"
```

---

## Task 6: Backend rate-limiter middleware + Program.cs wiring + endpoint attributes (6.15.3.B)

**Files:**
- Create: `src/Backend/AuraCore.API.Infrastructure/RateLimiting/RateLimitedAttribute.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/RateLimiting/RateLimiterMiddleware.cs`
- Modify: `src/Backend/AuraCore.API/Program.cs`
- Modify: `src/Backend/AuraCore.API/Controllers/AuthController.cs` (apply `[RateLimited("auth.login")]` to login)
- Modify: `src/Backend/AuraCore.API/Controllers/Superadmin/SuperadminAuthController.cs` (or wherever superadmin login lives) — apply same attribute
- Test: `tests/AuraCore.Tests.API/Phase615/RateLimiterMiddlewareTests.cs`

**Goal:** Middleware that reads the `[RateLimited(policyName)]` attribute from the matched endpoint, computes bucket key from `HttpContext.Connection.RemoteIpAddress` (after `UseForwardedHeaders`), calls `IAuraCoreRateLimiter.TryAcquireAsync`, and returns 429 with `Retry-After` header if blocked.

- [ ] **Step 1: Write failing middleware test**

Create `tests/AuraCore.Tests.API/Phase615/RateLimiterMiddlewareTests.cs`:

```csharp
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Infrastructure.RateLimiting;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class RateLimiterMiddlewareTests
{
    [Fact]
    public async Task NoEndpoint_PassesThrough()
    {
        var ctx = new DefaultHttpContext();
        var fakeLimiter = new FakeLimiter(allow: false); // would block if asked
        var mw = new RateLimiterMiddleware((_) => Task.CompletedTask, fakeLimiter);

        await mw.InvokeAsync(ctx);

        Assert.Equal(0, fakeLimiter.Calls);
        Assert.Equal(200, ctx.Response.StatusCode);
    }

    [Fact]
    public async Task EndpointWithoutAttribute_PassesThrough()
    {
        var ctx = new DefaultHttpContext();
        ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(_ => Task.CompletedTask, new Microsoft.AspNetCore.Http.EndpointMetadataCollection(), "test"));
        var fakeLimiter = new FakeLimiter(allow: false);
        var mw = new RateLimiterMiddleware((_) => Task.CompletedTask, fakeLimiter);

        await mw.InvokeAsync(ctx);

        Assert.Equal(0, fakeLimiter.Calls);
    }

    [Fact]
    public async Task EndpointWithAttribute_Allowed_CallsNext()
    {
        var nextCalled = false;
        var ctx = new DefaultHttpContext();
        ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(
            _ => Task.CompletedTask,
            new Microsoft.AspNetCore.Http.EndpointMetadataCollection(new RateLimitedAttribute("auth.login")),
            "test"));
        var fakeLimiter = new FakeLimiter(allow: true);
        var mw = new RateLimiterMiddleware((_) => { nextCalled = true; return Task.CompletedTask; }, fakeLimiter);

        await mw.InvokeAsync(ctx);

        Assert.Equal(1, fakeLimiter.Calls);
        Assert.True(nextCalled);
    }

    [Fact]
    public async Task EndpointWithAttribute_Blocked_Returns429WithRetryAfter()
    {
        var nextCalled = false;
        var ctx = new DefaultHttpContext();
        ctx.SetEndpoint(new Microsoft.AspNetCore.Http.Endpoint(
            _ => Task.CompletedTask,
            new Microsoft.AspNetCore.Http.EndpointMetadataCollection(new RateLimitedAttribute("auth.login")),
            "test"));
        var fakeLimiter = new FakeLimiter(allow: false, retryAfter: System.TimeSpan.FromSeconds(42));
        var mw = new RateLimiterMiddleware((_) => { nextCalled = true; return Task.CompletedTask; }, fakeLimiter);

        await mw.InvokeAsync(ctx);

        Assert.False(nextCalled);
        Assert.Equal((int)HttpStatusCode.TooManyRequests, ctx.Response.StatusCode);
        Assert.Equal("42", ctx.Response.Headers["Retry-After"].ToString());
    }

    private sealed class FakeLimiter : IAuraCoreRateLimiter
    {
        private readonly bool _allow;
        private readonly System.TimeSpan _retry;
        public int Calls;
        public FakeLimiter(bool allow, System.TimeSpan? retryAfter = null)
        {
            _allow = allow;
            _retry = retryAfter ?? System.TimeSpan.Zero;
        }
        public Task<RateLimitResult> TryAcquireAsync(string policyName, string bucketKey, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult(new RateLimitResult(_allow, _retry));
        }
    }
}
```

- [ ] **Step 2: Run test (expect compile errors first)**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~RateLimiterMiddlewareTests
```
Expected: build failure — `RateLimitedAttribute` and `RateLimiterMiddleware` not defined.

- [ ] **Step 3: Create `src/Backend/AuraCore.API.Infrastructure/RateLimiting/RateLimitedAttribute.cs`**

```csharp
using System;

namespace AuraCore.API.Infrastructure.RateLimiting;

/// <summary>
/// Phase 6.15.3 — endpoint metadata declaring which rate-limit policy applies.
/// Read by RateLimiterMiddleware via Endpoint.Metadata.GetMetadata.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class RateLimitedAttribute : Attribute
{
    public string PolicyName { get; }
    public RateLimitedAttribute(string policyName) { PolicyName = policyName; }
}
```

- [ ] **Step 4: Create `src/Backend/AuraCore.API.Infrastructure/RateLimiting/RateLimiterMiddleware.cs`**

```csharp
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace AuraCore.API.Infrastructure.RateLimiting;

public sealed class RateLimiterMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IAuraCoreRateLimiter _limiter;

    public RateLimiterMiddleware(RequestDelegate next, IAuraCoreRateLimiter limiter)
    {
        _next = next;
        _limiter = limiter;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var endpoint = ctx.GetEndpoint();
        var attr = endpoint?.Metadata.GetMetadata<RateLimitedAttribute>();
        if (attr is null)
        {
            await _next(ctx);
            return;
        }

        var bucketKey = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await _limiter.TryAcquireAsync(attr.PolicyName, bucketKey, ctx.RequestAborted);
        if (!result.Allowed)
        {
            ctx.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            var retrySeconds = (int)Math.Ceiling(result.RetryAfter.TotalSeconds);
            ctx.Response.Headers["Retry-After"] = retrySeconds.ToString(CultureInfo.InvariantCulture);
            await ctx.Response.WriteAsync($"Rate limit exceeded. Retry after {retrySeconds} seconds.");
            return;
        }

        await _next(ctx);
    }
}

public static class RateLimiterMiddlewareExtensions
{
    public static IApplicationBuilder UseAuraCoreRateLimiter(this IApplicationBuilder app)
        => app.UseMiddleware<RateLimiterMiddleware>();
}
```

- [ ] **Step 5: Run middleware tests to verify they pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~RateLimiterMiddlewareTests
```
Expected: 4 tests passing.

- [ ] **Step 6: Wire into `Program.cs`**

In `src/Backend/AuraCore.API/Program.cs`:

After the existing `AddScoped<IRateLimitConfigService, RateLimitConfigService>()` line (~80), add:

```csharp
builder.Services.AddSingleton<AuraCore.API.Infrastructure.RateLimiting.IAuraCoreRateLimiter,
                              AuraCore.API.Infrastructure.RateLimiting.AuraCoreRateLimiter>();
```

In the request-pipeline section (after `app.UseRouting()` and before `app.UseAuthorization()`), add:

```csharp
app.UseAuraCoreRateLimiter();
```

Open the file and find these two locations; the actual line numbers depend on existing content but follow the convention of "register service in builder section, register middleware in app section after routing".

- [ ] **Step 7: Apply `[RateLimited]` to auth endpoints**

In `src/Backend/AuraCore.API/Controllers/AuthController.cs` (or wherever the existing `[HttpPost("login")]` action lives), add:

```csharp
using AuraCore.API.Infrastructure.RateLimiting;

[HttpPost("login")]
[RateLimited("auth.login")]
public async Task<IActionResult> Login(...)
```

Repeat for any superadmin login endpoint with policy name `"auth.superadmin.login"` (add a row in the `system_settings` rate_limit_policies seed if missing — see Step 9). For register and signalr.connect, add the matching `[RateLimited(...)]` if those routes are still in scope.

Use Grep to find all current `[HttpPost("login")]` / `[HttpPost("register")]` / `[HttpPost("superadmin/login")]` actions; apply the attribute to each.

- [ ] **Step 8: Run all backend tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj
```
Expected: all tests pass (215 baseline + 9 new = 224, ±a few). No regressions.

- [ ] **Step 9: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/RateLimiting/RateLimitedAttribute.cs src/Backend/AuraCore.API.Infrastructure/RateLimiting/RateLimiterMiddleware.cs src/Backend/AuraCore.API/Program.cs src/Backend/AuraCore.API/Controllers/ tests/AuraCore.Tests.API/Phase615/RateLimiterMiddlewareTests.cs
git commit -m "phase-6.15.3: rate-limiter middleware + endpoint attribute + Program.cs wiring"
```

---

## Task 7: Backend bulk role change endpoints (6.15.4.A)

**Files:**
- Modify: `src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs` (add 2 actions + 2 DTOs)
- Test: `tests/AuraCore.Tests.API/Phase615/BulkRoleChangeTests.cs`

**Goal:** `POST /api/superadmin/admins/bulk-promote` and `bulk-demote`. Single `IDbContextTransaction`. Audit-log row per user via `[AuditAction]` (or manual `IAuditLogService` call inside the transaction since attribute-based audit only logs once for the bulk action). Return `{ succeeded, failed, errors[] }`.

- [ ] **Step 1: Write failing tests**

Create `tests/AuraCore.Tests.API/Phase615/BulkRoleChangeTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Controllers.Superadmin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Helpers;
using AuraCore.API.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class BulkRoleChangeTests
{
    private static AuraCoreDbContext NewDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"bulk-role-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    [Fact]
    public async Task BulkPromote_PromotesAllUsersAndAppliesTemplate()
    {
        await using var db = NewDb();
        var u1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", Role = "user", IsActive = true };
        var u2 = new User { Id = Guid.NewGuid(), Email = "b@x.com", Role = "user", IsActive = true };
        db.Users.AddRange(u1, u2);
        await db.SaveChangesAsync();

        var ctrl = new AdminManagementController(db);
        var dto = new BulkPromoteDto(
            new[] { u1.Id, u2.Id },
            PermissionTemplates.Trusted,
            "on_first_login",
            true);

        var result = await ctrl.BulkPromote(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var promoted = await db.Users.Where(u => u.Role == "admin").CountAsync();
        Assert.Equal(2, promoted);
        // Trusted = all Tier 2 grants
        var grants = await db.PermissionGrants.CountAsync();
        Assert.True(grants >= 2);
    }

    [Fact]
    public async Task BulkPromote_RollsBackOnFailure()
    {
        await using var db = NewDb();
        var u1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", Role = "user", IsActive = true };
        db.Users.Add(u1);
        await db.SaveChangesAsync();

        var ctrl = new AdminManagementController(db);
        // Include a non-existent ID to force failure inside the transaction.
        var dto = new BulkPromoteDto(
            new[] { u1.Id, Guid.NewGuid() },
            PermissionTemplates.Trusted,
            "never",
            false);

        var result = await ctrl.BulkPromote(dto, CancellationToken.None);

        // Either 4xx or partial-failure response — we just assert NO promotion happened
        // since the failed user should roll the whole tx back per all-or-nothing contract.
        var promoted = await db.Users.Where(u => u.Role == "admin").CountAsync();
        Assert.Equal(0, promoted);
    }

    [Fact]
    public async Task BulkDemote_DemotesAllAndRevokesGrants()
    {
        await using var db = NewDb();
        var a1 = new User { Id = Guid.NewGuid(), Email = "a@x.com", Role = "admin", IsActive = true };
        var a2 = new User { Id = Guid.NewGuid(), Email = "b@x.com", Role = "admin", IsActive = true };
        db.Users.AddRange(a1, a2);
        db.PermissionGrants.AddRange(
            new PermissionGrant { Id = Guid.NewGuid(), UserId = a1.Id, PermissionKey = PermissionKeys.ActionUsersDelete, GrantedAt = DateTimeOffset.UtcNow },
            new PermissionGrant { Id = Guid.NewGuid(), UserId = a2.Id, PermissionKey = PermissionKeys.ActionUsersBan, GrantedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var ctrl = new AdminManagementController(db);
        var dto = new BulkDemoteDto(new[] { a1.Id, a2.Id });

        var result = await ctrl.BulkDemote(dto, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stillAdmin = await db.Users.Where(u => u.Role == "admin").CountAsync();
        Assert.Equal(0, stillAdmin);
        var activeGrants = await db.PermissionGrants.Where(g => g.RevokedAt == null).CountAsync();
        Assert.Equal(0, activeGrants);
    }
}
```

> **Note:** This test references `PermissionGrant.RevokedAt`. If the actual entity uses a different field name (e.g. `RevokedAtUtc`), the implementing subagent must adjust the test field reference to match while keeping the assertion intent.

- [ ] **Step 2: Run test (expect failure)**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~BulkRoleChangeTests
```
Expected: build failure — `BulkPromoteDto`, `BulkDemoteDto`, `ctrl.BulkPromote`, `ctrl.BulkDemote` undefined.

- [ ] **Step 3: Add bulk endpoints to `AdminManagementController.cs`**

After the existing single `Demote` action (currently around line 313), add:

```csharp
[HttpPost("admins/bulk-promote")]
[AuditAction("BulkPromoteUsers", "User")]
public async Task<IActionResult> BulkPromote([FromBody] BulkPromoteDto dto, CancellationToken ct)
{
    if (dto?.UserIds == null || dto.UserIds.Length == 0)
        return BadRequest(new { error = "No users selected" });
    if (!PermissionTemplates.IsValidTemplate(dto.Template) || dto.Template == PermissionTemplates.Custom)
        return BadRequest(new { error = "Invalid template (Custom not supported in bulk; promote individually)" });

    var users = await _db.Users.Where(u => dto.UserIds.Contains(u.Id)).ToListAsync(ct);
    if (users.Count != dto.UserIds.Length)
        return BadRequest(new { error = "One or more user IDs not found", missing = dto.UserIds.Except(users.Select(u => u.Id)) });
    if (users.Any(u => u.Role != "user"))
        return BadRequest(new { error = "Some selected users are not in 'user' role" });

    var supportsTx = _db.Database.ProviderName?.Contains("InMemory") != true;
    var tx = supportsTx ? await _db.Database.BeginTransactionAsync(ct) : null;
    try
    {
        var keys = PermissionTemplates.GetPermissionsForTemplate(dto.Template);
        var deadline = ForceChangeDeadline(dto.ForcePasswordChange);
        foreach (var u in users)
        {
            u.Role = "admin";
            u.CreatedVia = "admin_bulk_promote";
            u.IsReadonly = PermissionTemplates.RequiresIsReadonlyFlag(dto.Template);
            u.Require2fa = dto.Require2fa;
            if (deadline.HasValue) u.ForcePasswordChangeAt = deadline.Value;

            foreach (var key in keys)
            {
                _db.PermissionGrants.Add(new PermissionGrant
                {
                    Id = Guid.NewGuid(),
                    UserId = u.Id,
                    PermissionKey = key,
                    GrantedAt = DateTimeOffset.UtcNow,
                });
            }
        }
        await _db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return Ok(new
        {
            succeeded = users.Count,
            failed = 0,
            promoted = users.Select(u => new { u.Id, u.Email, template = dto.Template }),
        });
    }
    catch (Exception ex)
    {
        if (tx != null) await tx.RollbackAsync(ct);
        return StatusCode(500, new { error = "Bulk promote failed", detail = ex.Message });
    }
}

[HttpPost("admins/bulk-demote")]
[AuditAction("BulkDemoteAdmins", "User")]
public async Task<IActionResult> BulkDemote([FromBody] BulkDemoteDto dto, CancellationToken ct)
{
    if (dto?.AdminIds == null || dto.AdminIds.Length == 0)
        return BadRequest(new { error = "No admins selected" });

    var admins = await _db.Users.Where(u => dto.AdminIds.Contains(u.Id) && u.Role == "admin").ToListAsync(ct);
    if (admins.Count != dto.AdminIds.Length)
        return BadRequest(new { error = "Some IDs are not active admins" });

    var supportsTx = _db.Database.ProviderName?.Contains("InMemory") != true;
    var tx = supportsTx ? await _db.Database.BeginTransactionAsync(ct) : null;
    try
    {
        var ids = admins.Select(a => a.Id).ToHashSet();
        foreach (var u in admins) u.Role = "user";
        var grants = await _db.PermissionGrants
            .Where(g => ids.Contains(g.UserId) && g.RevokedAt == null)
            .ToListAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var g in grants)
        {
            g.RevokedAt = now;
            g.RevokeReason = "bulk_demoted";
        }
        await _db.SaveChangesAsync(ct);
        if (tx != null) await tx.CommitAsync(ct);

        return Ok(new { succeeded = admins.Count, failed = 0 });
    }
    catch (Exception ex)
    {
        if (tx != null) await tx.RollbackAsync(ct);
        return StatusCode(500, new { error = "Bulk demote failed", detail = ex.Message });
    }
}
```

Then in the DTO section at the bottom of the file (around line 385), add:

```csharp
public record BulkPromoteDto(Guid[] UserIds, string Template, string ForcePasswordChange, bool Require2fa);
public record BulkDemoteDto(Guid[] AdminIds);
```

> **If `PermissionGrant.RevokedAt` / `RevokeReason` field names differ in the actual entity, adjust both this controller code and the test file in Step 1 to match.

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~BulkRoleChangeTests
```
Expected: 3 tests passing.

- [ ] **Step 5: Run full backend test suite**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj
```
Expected: full suite green, ~227+ tests.

- [ ] **Step 6: Commit**

```bash
git add src/Backend/AuraCore.API/Controllers/Superadmin/AdminManagementController.cs tests/AuraCore.Tests.API/Phase615/BulkRoleChangeTests.cs
git commit -m "phase-6.15.4: bulk-promote + bulk-demote endpoints in AdminManagementController"
```

---

## Task 8: Admin-panel bulk role change UI (6.15.4.B)

**Files:**
- Modify: `admin-panel/src/views/AdminManagementPage.tsx` (multi-select + sticky toolbar)
- Create: `admin-panel/src/components/BulkRoleChangeModal.tsx`
- Modify: `admin-panel/src/lib/api.ts` (bulkPromoteUsersToAdmin, bulkDemoteAdminsToUser)
- Test: `admin-panel/src/__tests__/views/AdminManagementBulk.test.tsx`

- [ ] **Step 1: Write the failing tests**

Create `admin-panel/src/__tests__/views/AdminManagementBulk.test.tsx`:

```typescript
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AdminManagementPage } from '@/views/AdminManagementPage';

const { items } = vi.hoisted(() => ({
  items: [
    { id: 'u1', email: 'a@x.com', role: 'admin', isActive: true, isReadonly: false, totpEnabled: true, require2fa: true, createdAt: '2026-04-01' },
    { id: 'u2', email: 'b@x.com', role: 'admin', isActive: true, isReadonly: false, totpEnabled: false, require2fa: false, createdAt: '2026-04-02' },
  ],
}));

vi.mock('@/lib/api', () => ({
  api: {
    listAdminAccounts: vi.fn().mockResolvedValue({ items }),
    createAdminAccount: vi.fn(),
    suspendAdmin: vi.fn(),
    restoreAdmin: vi.fn(),
    deleteAdmin: vi.fn(),
    resetAdminPassword: vi.fn(),
    bulkDemoteAdminsToUser: vi.fn().mockResolvedValue({ ok: true, data: { succeeded: 2 } }),
    bulkPromoteUsersToAdmin: vi.fn().mockResolvedValue({ ok: true, data: { succeeded: 2 } }),
  },
}));

describe('AdminManagementPage — bulk select', () => {
  it('selecting rows shows the bulk action toolbar with count', async () => {
    render(<AdminManagementPage />);
    await waitFor(() => screen.getByText('a@x.com'));

    const rowCheckboxes = screen.getAllByRole('checkbox').filter(c => c.getAttribute('data-row-checkbox'));
    expect(rowCheckboxes.length).toBe(2);

    fireEvent.click(rowCheckboxes[0]);
    await waitFor(() => screen.getByText(/1 selected/i));

    fireEvent.click(rowCheckboxes[1]);
    await waitFor(() => screen.getByText(/2 selected/i));
  });

  it('clicking Demote N opens BulkRoleChangeModal', async () => {
    render(<AdminManagementPage />);
    await waitFor(() => screen.getByText('a@x.com'));
    const rowCheckboxes = screen.getAllByRole('checkbox').filter(c => c.getAttribute('data-row-checkbox'));
    fireEvent.click(rowCheckboxes[0]);
    fireEvent.click(rowCheckboxes[1]);
    fireEvent.click(screen.getByText(/Demote 2 selected/i));

    await waitFor(() => screen.getByText(/Bulk Demote/i));
    expect(screen.getByText(/a@x.com/)).toBeInTheDocument();
    expect(screen.getByText(/b@x.com/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test**

```bash
cd admin-panel && npm test -- AdminManagementBulk
```
Expected: failure — checkbox role + bulk toolbar not yet present.

- [ ] **Step 3: Add API methods to `admin-panel/src/lib/api.ts`**

After the existing `applyAdminTemplate` method (line ~534-542), add:

```typescript
async bulkPromoteUsersToAdmin(userIds: string[], template: string, forcePasswordChange: string, require2fa: boolean) {
  const res = await request('/api/superadmin/admins/bulk-promote', {
    method: 'POST',
    body: JSON.stringify({ userIds, template, forcePasswordChange, require2fa }),
  });
  return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
},

async bulkDemoteAdminsToUser(adminIds: string[]) {
  const res = await request('/api/superadmin/admins/bulk-demote', {
    method: 'POST',
    body: JSON.stringify({ adminIds }),
  });
  return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
},
```

- [ ] **Step 4: Create `admin-panel/src/components/BulkRoleChangeModal.tsx`**

```tsx
import { useState } from 'react';
import { X } from 'lucide-react';
import { api } from '@/lib/api';
import { Combobox } from './Combobox';

interface AdminAccount {
  id: string;
  email: string;
  role: string;
}

type Mode = 'promote' | 'demote';

interface Props {
  mode: Mode;
  selected: AdminAccount[];
  onClose: () => void;
  onSuccess: () => void;
}

const TEMPLATE_OPTIONS = [
  { value: 'Default', label: 'Default (no Tier 2 permissions)' },
  { value: 'Trusted', label: 'Trusted (all Tier 2 permissions)' },
  { value: 'ReadOnly', label: 'ReadOnly (read-only flag, no permissions)' },
];

const FORCE_PW_OPTIONS = [
  { value: 'on_first_login', label: 'On first login' },
  { value: 'within_7_days', label: 'Within 7 days' },
  { value: 'within_30_days', label: 'Within 30 days' },
  { value: 'never', label: 'Never' },
];

export function BulkRoleChangeModal({ mode, selected, onClose, onSuccess }: Props) {
  const [template, setTemplate] = useState('Default');
  const [forcePw, setForcePw] = useState('on_first_login');
  const [require2fa, setRequire2fa] = useState(true);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState('');

  const apply = async () => {
    setSubmitting(true);
    setError('');
    try {
      const ids = selected.map((s) => s.id);
      const res = mode === 'promote'
        ? await api.bulkPromoteUsersToAdmin(ids, template, forcePw, require2fa)
        : await api.bulkDemoteAdminsToUser(ids);
      if (!res.ok) {
        setError(res.data?.error ?? 'Bulk operation failed');
        return;
      }
      onSuccess();
      onClose();
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 backdrop-blur-sm flex items-center justify-center z-50 p-4">
      <div className="glass-card w-full max-w-lg p-6 space-y-4">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-display font-bold">
            Bulk {mode === 'promote' ? 'Promote' : 'Demote'} — {selected.length} selected
          </h3>
          <button onClick={onClose}><X className="w-5 h-5" /></button>
        </div>

        {mode === 'promote' && (
          <>
            <label className="block text-xs text-white/50">Template
              <Combobox
                value={template}
                onChange={setTemplate}
                options={TEMPLATE_OPTIONS}
              />
            </label>
            <label className="block text-xs text-white/50">Force password change
              <Combobox
                value={forcePw}
                onChange={setForcePw}
                options={FORCE_PW_OPTIONS}
              />
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input type="checkbox" checked={require2fa} onChange={(e) => setRequire2fa(e.target.checked)} />
              Require 2FA
            </label>
          </>
        )}

        <div className="bg-white/5 rounded p-3 space-y-1 max-h-48 overflow-y-auto">
          <div className="text-xs text-white/50 mb-2">Audit preview ({selected.length} accounts):</div>
          {selected.map((s) => (
            <div key={s.id} className="text-xs font-mono">
              {s.email}: {s.role} → {mode === 'promote' ? `admin (${template}${require2fa ? ', 2FA required' : ''})` : 'user (grants revoked)'}
            </div>
          ))}
        </div>

        {error && <div className="text-red-400 text-sm">{error}</div>}

        <div className="flex justify-end gap-2">
          <button onClick={onClose} className="btn-ghost" disabled={submitting}>Cancel</button>
          <button onClick={apply} className="btn-primary" disabled={submitting}>
            {submitting ? 'Applying…' : `Apply to ${selected.length}`}
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Modify `admin-panel/src/views/AdminManagementPage.tsx`**

Add state at component top:

```typescript
const [selected, setSelected] = useState<Set<string>>(new Set());
const [bulkMode, setBulkMode] = useState<'promote' | 'demote' | null>(null);
```

Add helper:

```typescript
const toggleRow = (id: string) => {
  const next = new Set(selected);
  if (next.has(id)) next.delete(id); else next.add(id);
  setSelected(next);
};
const clearSelection = () => setSelected(new Set());
```

Insert sticky toolbar above the table (right after the page header h1 area):

```tsx
{selected.size > 0 && (
  <div className="glass-card p-3 flex items-center gap-3 sticky top-0 z-10">
    <span className="text-sm">{selected.size} selected</span>
    <button className="btn-primary-sm" onClick={() => setBulkMode('demote')}>
      Demote {selected.size} selected
    </button>
    <button className="btn-ghost btn-sm" onClick={clearSelection}>Cancel</button>
  </div>
)}
```

> Bulk-promote on AdminManagementPage doesn't make sense (it lists existing admins, not users). The toolbar exposes only Demote. Bulk Promote happens via the Roles & Permissions tab where users live; that integration is out-of-scope for this task — only the Demote path is wired.

Add checkbox column in `<thead>`:

```tsx
<th className="p-3 text-left w-8">
  <input
    type="checkbox"
    checked={selected.size === items.length && items.length > 0}
    onChange={(e) => setSelected(e.target.checked ? new Set(items.map(i => i.id)) : new Set())}
  />
</th>
```

Add per-row checkbox as the first `<td>`:

```tsx
<td className="p-3 w-8">
  <input
    type="checkbox"
    data-row-checkbox="true"
    checked={selected.has(account.id)}
    onChange={() => toggleRow(account.id)}
  />
</td>
```

At the bottom of the JSX (before closing fragment), render the modal conditionally:

```tsx
{bulkMode && (
  <BulkRoleChangeModal
    mode={bulkMode}
    selected={items.filter(i => selected.has(i.id))}
    onClose={() => setBulkMode(null)}
    onSuccess={() => { clearSelection(); refresh(); }}
  />
)}
```

Add the import at top of the file:

```typescript
import { BulkRoleChangeModal } from '@/components/BulkRoleChangeModal';
```

- [ ] **Step 6: Run tests**

```bash
cd admin-panel && npm test -- AdminManagementBulk AdminManagementPage
```
Expected: bulk tests + existing AdminManagementPage tests green.

- [ ] **Step 7: Run full admin-panel test suite**

```bash
cd admin-panel && npm test
```
Expected: 83 baseline + ~3 new = 86 passing.

- [ ] **Step 8: Commit**

```bash
git add admin-panel/src/views/AdminManagementPage.tsx admin-panel/src/components/BulkRoleChangeModal.tsx admin-panel/src/lib/api.ts admin-panel/src/__tests__/views/AdminManagementBulk.test.tsx
git commit -m "phase-6.15.4: admin-panel bulk demote multi-select + BulkRoleChangeModal"
```

---

## Task 9: Backend audit retention service + endpoints + index migration (6.15.5.A)

**Files:**
- Create: `src/Backend/AuraCore.API.Infrastructure/Services/Background/AuditLogCleanupService.cs`
- Create: `src/Backend/AuraCore.API/Controllers/Superadmin/AuditRetentionController.cs`
- Create: `src/Backend/AuraCore.API.Infrastructure/Migrations/<timestamp>_AddAuditLogCreatedAtIndex.cs` (via `dotnet ef migrations add`)
- Modify: `src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs` (add single-column index declaration + 3 new system_settings rows)
- Modify: `src/Backend/AuraCore.API/Program.cs` (register hosted service)
- Test: `tests/AuraCore.Tests.API/Phase615/AuditRetentionTests.cs`

- [ ] **Step 1: Add the single-column `CreatedAt` index to `AuraCoreDbContext`**

In the `audit_log` `e =>` block (around line 195-211), append after the existing three indexes:

```csharp
e.HasIndex(a => a.CreatedAt).HasDatabaseName("idx_audit_created");
```

In the `system_settings` `HasData(...)` (around line 277-279), append three new rows:

```csharp
new SystemSetting { Key = "audit_retention.retentionDays", Value = "365", UpdatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero) },
new SystemSetting { Key = "audit_retention.lastRunAt", Value = "", UpdatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero) },
new SystemSetting { Key = "audit_retention.lastRunDeletedRows", Value = "0", UpdatedAt = new DateTimeOffset(2026, 4, 28, 0, 0, 0, TimeSpan.Zero) },
```

- [ ] **Step 2: Create the EF migration**

Run from `src/Backend/AuraCore.API.Infrastructure/`:

```bash
dotnet ef migrations add AddAuditRetentionSettingsAndIndex --startup-project ../AuraCore.API
```

Expected: a new migration file under `Migrations/` that adds the index and seed rows. Inspect the generated `Up()` method — it should contain `CreateIndex(name: "idx_audit_created", ...)` and three `InsertData(...)` for system_settings.

- [ ] **Step 3: Write the failing tests**

Create `tests/AuraCore.Tests.API/Phase615/AuditRetentionTests.cs`:

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Controllers.Superadmin;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services.Background;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AuraCore.Tests.API.Phase615;

public sealed class AuditRetentionTests
{
    private static AuraCoreDbContext NewDb()
    {
        var opt = new DbContextOptionsBuilder<AuraCoreDbContext>()
            .UseInMemoryDatabase($"audit-retention-{Guid.NewGuid()}").Options;
        return new AuraCoreDbContext(opt);
    }

    [Fact]
    public async Task RunCleanup_DeletesOnlyOldRows()
    {
        await using var db = NewDb();
        db.SystemSettings.Add(new SystemSetting { Key = "audit_retention.retentionDays", Value = "30" });
        var oldDate = DateTimeOffset.UtcNow.AddDays(-60);
        var newDate = DateTimeOffset.UtcNow.AddDays(-5);
        db.AuditLog.AddRange(
            new AuditLogEntry { ActorEmail = "a", Action = "X", TargetType = "T", CreatedAt = oldDate },
            new AuditLogEntry { ActorEmail = "a", Action = "X", TargetType = "T", CreatedAt = oldDate },
            new AuditLogEntry { ActorEmail = "a", Action = "X", TargetType = "T", CreatedAt = newDate });
        await db.SaveChangesAsync();

        var deleted = await AuditLogCleanupService.RunCleanupAsync(db, NullLogger.Instance, CancellationToken.None);

        Assert.Equal(2, deleted);
        var remaining = await db.AuditLog.CountAsync();
        Assert.Equal(2, remaining); // 1 original new + 1 retention_run audit_log row
        Assert.Contains(await db.AuditLog.ToListAsync(), e => e.Action == "AuditRetentionRun");
    }

    [Fact]
    public async Task RunCleanup_WritesRetentionRunAuditRow()
    {
        await using var db = NewDb();
        db.SystemSettings.Add(new SystemSetting { Key = "audit_retention.retentionDays", Value = "30" });
        await db.SaveChangesAsync();

        await AuditLogCleanupService.RunCleanupAsync(db, NullLogger.Instance, CancellationToken.None);

        var run = await db.AuditLog.FirstOrDefaultAsync(e => e.Action == "AuditRetentionRun");
        Assert.NotNull(run);
        Assert.Equal("System", run!.TargetType);
        Assert.Contains("\"deleted\":0", run.AfterData);
        Assert.Contains("\"retentionDays\":30", run.AfterData);
    }

    [Fact]
    public async Task PolicyEndpoint_ReturnsCurrentSettings()
    {
        await using var db = NewDb();
        db.SystemSettings.AddRange(
            new SystemSetting { Key = "audit_retention.retentionDays", Value = "180" },
            new SystemSetting { Key = "audit_retention.lastRunAt", Value = "2026-04-28T03:00:00+00:00" },
            new SystemSetting { Key = "audit_retention.lastRunDeletedRows", Value = "1234" });
        db.AuditLog.Add(new AuditLogEntry {
            ActorEmail = "a", Action = "X", TargetType = "T",
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-100) });
        await db.SaveChangesAsync();

        var ctrl = new AuditRetentionController(db);
        var result = await ctrl.GetPolicy(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        dynamic body = ok.Value!;
        Assert.Equal(180, (int)body.retentionDays);
        Assert.Equal(1234, (int)body.lastRunDeletedRows);
    }

    [Fact]
    public async Task PolicyEndpoint_PostUpdatesRetentionDays()
    {
        await using var db = NewDb();
        db.SystemSettings.Add(new SystemSetting { Key = "audit_retention.retentionDays", Value = "365" });
        await db.SaveChangesAsync();

        var ctrl = new AuditRetentionController(db);
        var result = await ctrl.SetPolicy(new SetRetentionDto(90), CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var stored = await db.SystemSettings.FirstAsync(s => s.Key == "audit_retention.retentionDays");
        Assert.Equal("90", stored.Value);
    }

    [Fact]
    public async Task SetPolicy_RejectsOutOfRange()
    {
        await using var db = NewDb();
        var ctrl = new AuditRetentionController(db);
        var resultLow = await ctrl.SetPolicy(new SetRetentionDto(29), CancellationToken.None);
        var resultHigh = await ctrl.SetPolicy(new SetRetentionDto(3651), CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(resultLow);
        Assert.IsType<BadRequestObjectResult>(resultHigh);
    }
}
```

- [ ] **Step 4: Run test (expect failure)**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~AuditRetentionTests
```
Expected: build failure — service + controller + DTO not defined.

- [ ] **Step 5: Create `AuditLogCleanupService.cs`**

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Domain.Entities;
using AuraCore.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AuraCore.API.Infrastructure.Services.Background;

/// <summary>
/// Phase 6.15.5 — periodic cleanup of audit_log rows older than the configured
/// retention window. Runs once per day; manual triggers go through
/// AuditRetentionController.RunNow which calls the same RunCleanupAsync helper.
/// </summary>
public sealed class AuditLogCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private static readonly TimeSpan StartupDelay = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuditLogCleanupService> _logger;

    public AuditLogCleanupService(IServiceScopeFactory scopeFactory, ILogger<AuditLogCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(StartupDelay, stoppingToken); }
        catch (TaskCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AuraCoreDbContext>();
                await RunCleanupAsync(db, _logger, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit log cleanup tick failed");
            }
            try { await Task.Delay(Interval, stoppingToken); }
            catch (TaskCanceledException) { return; }
        }
    }

    public static async Task<int> RunCleanupAsync(AuraCoreDbContext db, Microsoft.Extensions.Logging.ILogger logger, CancellationToken ct)
    {
        var setting = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "audit_retention.retentionDays", ct);
        var days = setting != null && int.TryParse(setting.Value, out var d) ? d : 365;
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        int deleted;
        if (db.Database.ProviderName?.Contains("InMemory") == true)
        {
            var rows = await db.AuditLog.Where(a => a.CreatedAt < cutoff).ToListAsync(ct);
            db.AuditLog.RemoveRange(rows);
            deleted = rows.Count;
            await db.SaveChangesAsync(ct);
        }
        else
        {
            deleted = await db.AuditLog.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
        }

        // Write a self-audit row so the cleanup itself is traceable.
        db.AuditLog.Add(new AuditLogEntry
        {
            ActorEmail = "system",
            Action = "AuditRetentionRun",
            TargetType = "System",
            AfterData = $"{{\"deleted\":{deleted},\"retentionDays\":{days}}}",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        // Update lastRunAt + lastRunDeletedRows in system_settings
        await UpsertSettingAsync(db, "audit_retention.lastRunAt", DateTimeOffset.UtcNow.ToString("O"), ct);
        await UpsertSettingAsync(db, "audit_retention.lastRunDeletedRows", deleted.ToString(), ct);

        await db.SaveChangesAsync(ct);

        if (deleted > 10000)
            logger.LogWarning("Audit retention deleted {Deleted} rows in one run — investigate growth", deleted);

        return deleted;
    }

    private static async Task UpsertSettingAsync(AuraCoreDbContext db, string key, string value, CancellationToken ct)
    {
        var existing = await db.SystemSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (existing == null)
            db.SystemSettings.Add(new SystemSetting { Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow });
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }
}
```

- [ ] **Step 6: Create `AuditRetentionController.cs`**

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuraCore.API.Filters;
using AuraCore.API.Infrastructure.Data;
using AuraCore.API.Infrastructure.Services.Background;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AuraCore.API.Controllers.Superadmin;

[ApiController]
[Route("api/superadmin/audit-retention")]
[Authorize(Roles = "superadmin")]
public sealed class AuditRetentionController : ControllerBase
{
    private readonly AuraCoreDbContext _db;

    public AuditRetentionController(AuraCoreDbContext db) { _db = db; }

    [HttpGet("policy")]
    public async Task<IActionResult> GetPolicy(CancellationToken ct)
    {
        var settings = await _db.SystemSettings
            .Where(s => s.Key.StartsWith("audit_retention."))
            .ToDictionaryAsync(s => s.Key, s => s.Value, ct);

        var totalRows = await _db.AuditLog.CountAsync(ct);
        var oldestAt = totalRows > 0
            ? await _db.AuditLog.MinAsync(a => (DateTimeOffset?)a.CreatedAt, ct)
            : (DateTimeOffset?)null;

        return Ok(new
        {
            retentionDays = settings.TryGetValue("audit_retention.retentionDays", out var d) && int.TryParse(d, out var dn) ? dn : 365,
            lastRunAt = settings.TryGetValue("audit_retention.lastRunAt", out var l) && DateTimeOffset.TryParse(l, out var lr) ? (DateTimeOffset?)lr : null,
            lastRunDeletedRows = settings.TryGetValue("audit_retention.lastRunDeletedRows", out var n) && int.TryParse(n, out var nn) ? nn : 0,
            totalRows,
            oldestAt,
        });
    }

    [HttpPost("policy")]
    [AuditAction("AuditRetentionPolicySet", "System")]
    public async Task<IActionResult> SetPolicy([FromBody] SetRetentionDto dto, CancellationToken ct)
    {
        if (dto.RetentionDays < 30 || dto.RetentionDays > 3650)
            return BadRequest(new { error = "Retention must be 30-3650 days" });

        var existing = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == "audit_retention.retentionDays", ct);
        if (existing == null)
            _db.SystemSettings.Add(new Domain.Entities.SystemSetting { Key = "audit_retention.retentionDays", Value = dto.RetentionDays.ToString(), UpdatedAt = DateTimeOffset.UtcNow });
        else
        {
            existing.Value = dto.RetentionDays.ToString();
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { retentionDays = dto.RetentionDays });
    }

    [HttpPost("run-now")]
    [AuditAction("AuditRetentionRunNow", "System")]
    public async Task<IActionResult> RunNow(CancellationToken ct)
    {
        var deleted = await AuditLogCleanupService.RunCleanupAsync(_db, NullLogger.Instance, ct);
        return Ok(new { deleted });
    }
}

public record SetRetentionDto(int RetentionDays);
```

- [ ] **Step 7: Register hosted service in `Program.cs`**

Find the existing `AddHostedService<AuditLogPurgeService>()` line (~225) and add immediately after:

```csharp
builder.Services.AddHostedService<AuraCore.API.Infrastructure.Services.Background.AuditLogCleanupService>();
```

- [ ] **Step 8: Run tests**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj --filter FullyQualifiedName~AuditRetentionTests
```
Expected: 5 tests passing.

- [ ] **Step 9: Run full backend suite**

```bash
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj
```
Expected: ~233 passing.

- [ ] **Step 10: Commit**

```bash
git add src/Backend/AuraCore.API.Infrastructure/Services/Background/AuditLogCleanupService.cs src/Backend/AuraCore.API/Controllers/Superadmin/AuditRetentionController.cs src/Backend/AuraCore.API.Infrastructure/Data/AuraCoreDbContext.cs src/Backend/AuraCore.API.Infrastructure/Migrations/ src/Backend/AuraCore.API/Program.cs tests/AuraCore.Tests.API/Phase615/AuditRetentionTests.cs
git commit -m "phase-6.15.5: audit-log retention service + controller + index migration"
```

---

## Task 10: Admin-panel AuditRetentionPage + routing (6.15.5.B)

**Files:**
- Create: `admin-panel/src/views/AuditRetentionPage.tsx`
- Modify: `admin-panel/src/lib/api.ts` (audit retention methods)
- Modify: `admin-panel/src/app/AdminPanel.tsx` (Page type + nav group + PAGES mapping)
- Test: `admin-panel/src/__tests__/views/AuditRetentionPage.test.tsx`

- [ ] **Step 1: Add API methods to `admin-panel/src/lib/api.ts`**

After the bulk role change methods added in Task 8, add:

```typescript
async getAuditRetentionPolicy() {
  const res = await request('/api/superadmin/audit-retention/policy');
  return res.ok ? await res.json() : null;
},

async setAuditRetentionPolicy(retentionDays: number) {
  const res = await request('/api/superadmin/audit-retention/policy', {
    method: 'POST',
    body: JSON.stringify({ retentionDays }),
  });
  return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
},

async runAuditRetentionNow() {
  const res = await request('/api/superadmin/audit-retention/run-now', { method: 'POST' });
  return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
},
```

- [ ] **Step 2: Write the failing test**

Create `admin-panel/src/__tests__/views/AuditRetentionPage.test.tsx`:

```typescript
import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AuditRetentionPage } from '@/views/AuditRetentionPage';

vi.mock('@/lib/api', () => ({
  api: {
    getAuditRetentionPolicy: vi.fn().mockResolvedValue({
      retentionDays: 180,
      lastRunAt: '2026-04-28T03:00:00.000+00:00',
      lastRunDeletedRows: 1234,
      totalRows: 50000,
      oldestAt: '2025-01-01T00:00:00.000+00:00',
    }),
    setAuditRetentionPolicy: vi.fn().mockResolvedValue({ ok: true, data: { retentionDays: 90 } }),
    runAuditRetentionNow: vi.fn().mockResolvedValue({ ok: true, data: { deleted: 12 } }),
  },
}));

describe('AuditRetentionPage', () => {
  it('renders current policy + KPIs', async () => {
    render(<AuditRetentionPage />);
    await waitFor(() => screen.getByDisplayValue('180'));
    expect(screen.getByText(/50,?000/)).toBeInTheDocument();
    expect(screen.getByText(/1,?234/)).toBeInTheDocument();
  });

  it('saving a new retention value calls api.setAuditRetentionPolicy', async () => {
    const { api } = await import('@/lib/api');
    render(<AuditRetentionPage />);
    await waitFor(() => screen.getByDisplayValue('180'));
    const input = screen.getByDisplayValue('180') as HTMLInputElement;
    fireEvent.change(input, { target: { value: '90' } });
    fireEvent.click(screen.getByText(/save/i));
    await waitFor(() => expect(api.setAuditRetentionPolicy).toHaveBeenCalledWith(90));
  });

  it('Run Now button calls api.runAuditRetentionNow', async () => {
    const { api } = await import('@/lib/api');
    render(<AuditRetentionPage />);
    await waitFor(() => screen.getByDisplayValue('180'));
    fireEvent.click(screen.getByText(/run cleanup now/i));
    await waitFor(() => expect(api.runAuditRetentionNow).toHaveBeenCalled());
  });
});
```

- [ ] **Step 3: Run test (expect failure)**

```bash
cd admin-panel && npm test -- AuditRetentionPage
```
Expected: failure — page does not exist.

- [ ] **Step 4: Create `admin-panel/src/views/AuditRetentionPage.tsx`**

```tsx
import { useEffect, useState } from 'react';
import { Clock, Play } from 'lucide-react';
import { api } from '@/lib/api';

interface RetentionPolicy {
  retentionDays: number;
  lastRunAt: string | null;
  lastRunDeletedRows: number;
  totalRows: number;
  oldestAt: string | null;
}

export function AuditRetentionPage() {
  const [policy, setPolicy] = useState<RetentionPolicy | null>(null);
  const [pendingDays, setPendingDays] = useState(0);
  const [saving, setSaving] = useState(false);
  const [running, setRunning] = useState(false);
  const [message, setMessage] = useState('');

  const refresh = async () => {
    const p = await api.getAuditRetentionPolicy();
    if (p) {
      setPolicy(p);
      setPendingDays(p.retentionDays);
    }
  };
  useEffect(() => { refresh(); }, []);

  const save = async () => {
    setSaving(true); setMessage('');
    try {
      const res = await api.setAuditRetentionPolicy(pendingDays);
      setMessage(res.ok ? `Saved: ${pendingDays} days` : (res.data?.error ?? 'Save failed'));
      if (res.ok) refresh();
    } finally {
      setSaving(false);
    }
  };

  const runNow = async () => {
    setRunning(true); setMessage('');
    try {
      const res = await api.runAuditRetentionNow();
      setMessage(res.ok ? `Cleanup complete: ${res.data.deleted} rows deleted` : (res.data?.error ?? 'Run failed'));
      if (res.ok) refresh();
    } finally {
      setRunning(false);
    }
  };

  if (!policy) return <div className="p-8 text-center text-white/50">Loading…</div>;

  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-display font-bold flex items-center gap-2">
        <Clock className="w-6 h-6" />Audit Log Retention
      </h1>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
        <div className="glass-card p-4">
          <div className="text-xs text-white/50">Total rows</div>
          <div className="text-2xl font-bold">{policy.totalRows.toLocaleString()}</div>
        </div>
        <div className="glass-card p-4">
          <div className="text-xs text-white/50">Oldest entry</div>
          <div className="text-sm">{policy.oldestAt ? new Date(policy.oldestAt).toLocaleString() : '—'}</div>
        </div>
        <div className="glass-card p-4">
          <div className="text-xs text-white/50">Last cleanup</div>
          <div className="text-sm">{policy.lastRunAt ? new Date(policy.lastRunAt).toLocaleString() : 'Never'}</div>
          <div className="text-xs text-white/50 mt-1">{policy.lastRunDeletedRows.toLocaleString()} rows deleted</div>
        </div>
      </div>

      <div className="glass-card p-6 space-y-4">
        <div>
          <h3 className="text-lg font-bold mb-2">Retention policy</h3>
          <label className="block text-xs text-white/50">Retain audit logs for (days)
            <input
              type="number"
              min={30}
              max={3650}
              value={pendingDays}
              onChange={(e) => setPendingDays(Number(e.target.value))}
              className="input-dark w-full mt-1"
            />
          </label>
          <div className="text-xs text-white/40 mt-1">Range: 30–3650 days. Daily cleanup runs at the configured retention window.</div>
        </div>

        <div className="flex gap-2">
          <button onClick={save} className="btn-primary" disabled={saving || pendingDays === policy.retentionDays}>
            {saving ? 'Saving…' : 'Save'}
          </button>
          <button onClick={runNow} className="btn-ghost flex items-center gap-1" disabled={running}>
            <Play className="w-4 h-4" />{running ? 'Running…' : 'Run cleanup now'}
          </button>
        </div>

        {message && <div className="text-sm text-cyan-300">{message}</div>}
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Wire into `admin-panel/src/app/AdminPanel.tsx`**

In the imports (~line 40):

```typescript
import { AuditRetentionPage } from '@/views/AuditRetentionPage';
import { Clock as ClockIcon } from 'lucide-react';
```

In the `Page` type union (line 53):

```typescript
type Page = 'dashboard' | ... | 'auditRetention';
```
(append `| 'auditRetention'` to the existing union — keep all current entries)

In the SUPERADMIN_EXTRA_GROUPS (lines 77-87), append a new item to the items array:

```typescript
{ id: 'auditRetention', icon: ClockIcon, label: 'Audit Retention' },
```

In the PAGES mapping (lines 97-105):

```typescript
auditRetention: AuditRetentionPage,
```

- [ ] **Step 6: Run test**

```bash
cd admin-panel && npm test -- AuditRetentionPage
```
Expected: 3 tests passing.

- [ ] **Step 7: Run full admin-panel test suite**

```bash
cd admin-panel && npm test
```
Expected: ~89 tests passing (83 baseline + 3 bulk + 3 retention).

- [ ] **Step 8: Commit**

```bash
git add admin-panel/src/views/AuditRetentionPage.tsx admin-panel/src/app/AdminPanel.tsx admin-panel/src/lib/api.ts admin-panel/src/__tests__/views/AuditRetentionPage.test.tsx
git commit -m "phase-6.15.5: admin-panel AuditRetentionPage + routing wiring"
```

---

## Task 11: Final integration check (build + test all surfaces)

**Files:** None (verification only)

- [ ] **Step 1: Backend build + test**

```bash
dotnet build src/Backend/AuraCore.API/AuraCore.API.csproj -c Release
dotnet test tests/AuraCore.Tests.API/AuraCore.Tests.API.csproj
```

Expected:
- Build: clean (no warnings about new code)
- Tests: ~233 passing (215 baseline + 5 rate limiter + 4 middleware + 3 bulk role + 5 retention)

- [ ] **Step 2: Admin-panel build + test**

```bash
cd admin-panel && npm run build && npm test
```

Expected:
- Build: clean Next.js production build
- Tests: ~89 passing (83 baseline + 3 bulk + 3 retention)

- [ ] **Step 3: Mobile build + test**

```bash
cd mobile && npm test
```

Expected:
- Tests: ~22 passing (16 baseline + 6 cache/refresh)

The mobile EAS build itself is deferred to operational deploy — `npm test` is sufficient for code-correctness gating.

- [ ] **Step 4: Verify no untracked or modified files**

```bash
git status
```

Expected: `nothing to commit, working tree clean` (all 10 prior commits applied).

- [ ] **Step 5: Generate summary commit count**

```bash
git log --oneline main..HEAD
```

Expected: 10 commits on top of `a33b60e`. List them — they should match the task names above.

- [ ] **Step 6: Push branch (DEFERRED — confirm with user before push)**

The user must confirm push + deploy timing per supervisor mode. Plan execution stops here; actual deployment (backend scp + admin-panel rebuild + mobile EAS build) happens in a separate supervised step after the user reviews the diff.

---

## Operational deploy checklist (executed only after user confirms)

This section is reference-only for the deploy step; **do not execute during plan execution**.

1. **Merge `phase-6-15-mobile-polish-web-cleanup` to main** with `--no-ff` + ceremonial empty commit (matches Phase 6.13/6.14 pattern)
2. **Push to origin/main**
3. **Backend deploy:**
   - `dotnet publish src/Backend/AuraCore.API -c Release -o publish-api`
   - `tar` the publish-api dir
   - scp to `/var/www/auracore-api.bak-YYYYMMDDHHMMSS` then move
   - `chown -R www-data:www-data /var/www/auracore-api`
   - `systemctl restart auracore-api`
   - Apply EF migration: `dotnet ef database update` (or via `dotnet AuraCore.API.dll --migrate` if a migration runner exists)
4. **Admin-panel deploy:**
   - `npm run build` in admin-panel/
   - scp `out/` (or `.next/` per your Next.js config) to `/var/www/admin-panel.bak-YYYYMMDDHHMMSS`
   - chown + restart nginx if needed
5. **Mobile deploy:**
   - `eas build --profile preview --platform android` from mobile/
   - Sideload APK to admin team
6. **Smoke test:**
   - Mobile: cold start → single biometric prompt → tabs work without re-prompt
   - Mobile: leave app open >15min, hit Dashboard → 401 → silent refresh → success
   - Admin: AdminManagementPage → multi-select 2 admins → Demote 2 selected → confirm → both demoted
   - Admin: superadmin nav → Audit Retention → set 90 days → Save → Run cleanup now → confirm row count change
   - Operator UI: edit a rate-limit policy → curl auth/login 5x → 6th gets 429 with Retry-After

## Rollback

- Backend: restore prior `bak-YYYYMMDDHHMMSS` directory + `systemctl restart auracore-api`. Migration rollback: `dotnet ef database update <previous-migration-name>` reverts the new index + seed rows.
- Admin-panel: restore prior `bak-YYYYMMDDHHMMSS`.
- Mobile: previous APK if available, otherwise users keep current build (mobile clients are isolated; uninstall + reinstall with prior APK if needed).
- Branch: `git revert <merge-commit>` if everything else fails.

---

## Self-review

**Spec coverage:**
- 6.15.1 (mobile cache + single-gate) → Tasks 1–3 ✅
- 6.15.2 (mobile JWT refresh) → Task 4 ✅
- 6.15.3 (rate limiter hot-reload) → Tasks 5–6 ✅
- 6.15.4 (bulk role change) → Tasks 7–8 ✅
- 6.15.5 (audit retention) → Tasks 9–10 ✅

**Test budget:**
- Backend: 215 → ~233 (+18; spec said +12-15, but middleware coverage adds 4 extra)
- Mobile: 16 → ~22 (+6)
- FE admin-panel: 83 → ~89 (+6)

**Spec deviations documented in plan:**
- `IRateLimitPoliciesService` → actual `IRateLimitConfigService`
- "replace `AddRateLimiter`" → greenfield (built-in not wired)
- `PermissionKeys.ActionUsersPromote/Demote` not declared → bulk endpoints inherit superadmin authorization at controller scope (matches existing single-promote/demote pattern)
- Bulk Promote on AdminManagementPage scoped down to Demote-only (page lists admins not users); promote-bulk left for a future phase if user-list integration is added

**Type/method consistency:** `getCachedJwt`, `setCachedJwt`, `clearAuthCache`, `tryRefreshToken`, `setOnAuthFailure`, `fireAuthFailure`, `IAuraCoreRateLimiter`, `RateLimitedAttribute`, `RunCleanupAsync` — all referenced uniformly across tasks.

**Placeholders:** none. All steps contain executable code or commands.
