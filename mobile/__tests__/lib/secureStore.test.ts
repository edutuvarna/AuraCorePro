import * as SecureStore from 'expo-secure-store';
import {
  setJwt, getJwt, clearAuth, setLastActiveAt, getLastActiveAt, isInactiveBeyondLimit,
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
