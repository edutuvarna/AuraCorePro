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

    fetchMock.mockImplementation(async (url: string, init: any) => {
      if (typeof url === 'string' && url.includes('/api/auth/refresh')) {
        return refreshPromise;
      }
      // For non-refresh endpoints: when bearer is expired, return 401; otherwise 200.
      const auth = init?.headers?.Authorization;
      if (auth === 'Bearer expired.jwt') {
        return { ok: false, status: 401, json: async () => ({}) };
      }
      return { ok: true, status: 200, json: async () => ({}) };
    });

    const calls = Promise.all([api.getStats(), api.getStats(), api.getStats()]);

    // Resolve refresh synchronously after Promise.all has scheduled all three
    // calls. The three callers are suspended on fetch() at this point; the
    // resolution will be observed on the next microtask cycle when each
    // resumes and awaits ensureRefresh. Synchronous resolution avoids a
    // leaked timer that would otherwise trigger Jest's worker-exit warning.
    refreshResolve({
      ok: true, status: 200,
      json: async () => ({ accessToken: 'fresh.jwt', refreshToken: 'fresh.refresh' }),
    } as any);

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
