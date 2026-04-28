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
