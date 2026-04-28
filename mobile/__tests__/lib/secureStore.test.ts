import * as SecureStore from 'expo-secure-store';
import {
  setJwt, getJwt, clearAuth, setLastActiveAt, getLastActiveAt, isInactiveBeyondLimit,
  setRefreshToken, getRefreshToken,
  setCachedJwt, getCachedJwt, setCachedRefreshToken, getCachedRefreshToken, clearAuthCache,
} from '@/lib/secureStore';

describe('secureStore', () => {
  beforeEach(async () => {
    await clearAuth();
    jest.clearAllMocks();
  });

  it('roundtrips JWT through secure storage', async () => {
    await setJwt('header.payload.sig');
    expect(await getJwt()).toBe('header.payload.sig');
  });

  it('returns null after clearAuth', async () => {
    await setJwt('x.y.z');
    await clearAuth();
    expect(await getJwt()).toBeNull();
  });

  it('lastActiveAt roundtrips and reports beyond-limit when stale', async () => {
    await setLastActiveAt(new Date('2026-01-01T00:00:00Z').getTime());
    const stale = await isInactiveBeyondLimit(30);
    expect(stale).toBe(true);
  });

  it('lastActiveAt within limit returns false', async () => {
    await setLastActiveAt(Date.now() - 1000 * 60 * 60); // 1 hour ago
    expect(await isInactiveBeyondLimit(30)).toBe(false);
  });

  it('lastActiveAt missing returns true (treat as inactive)', async () => {
    expect(await isInactiveBeyondLimit(30)).toBe(true);
  });
});

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
