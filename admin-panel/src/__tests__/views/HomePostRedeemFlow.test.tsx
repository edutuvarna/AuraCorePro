import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach, beforeAll } from 'vitest';

// Returns truthy from getStats so the auth check resolves and AdminPanelInner
// is mounted — that's the path Phase 6.13.6-followup needs to exercise.
// Other api calls remain Proxy-stubbed no-ops.
vi.mock('@/lib/api', () => ({
  api: new Proxy({ getStats: () => Promise.resolve({ users: 0, payments: 0 }) }, {
    get: (target: any, prop: string) => prop in target ? target[prop] : () => Promise.resolve(null),
  }),
  setToken: () => {},
  getToken: () => 'fake-jwt',
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

import Home from '@/app/page';

// Minimal valid-shape JWT: header.payload.sig where payload claims admin role.
function fakeAdminJwt(): string {
  const header = btoa(JSON.stringify({ alg: 'HS256', typ: 'JWT' }));
  const payload = btoa(JSON.stringify({
    'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': 'admin',
    sub: 'test-admin',
  }));
  return `${header}.${payload}.sig`;
}

describe('Home page post-redeem forced-2FA scope', () => {
  beforeEach(() => {
    localStorage.clear();
    sessionStorage.clear();
  });

  afterEach(() => {
    sessionStorage.clear();
    window.location.hash = '';
  });

  it('applies 2fa-setup-only scope when post-redeem sessionStorage flag is present', async () => {
    localStorage.setItem('aura_token', fakeAdminJwt());
    sessionStorage.setItem('aura_post_redeem_force_2fa', '1');

    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText(/two-factor authentication setup/i)).toBeTruthy();
    });
    // Flag must be cleared after a single read so a refresh later doesn't re-trigger.
    expect(sessionStorage.getItem('aura_post_redeem_force_2fa')).toBeNull();
  });

  it('does not apply scope when sessionStorage flag is absent — normal dashboard load', async () => {
    localStorage.setItem('aura_token', fakeAdminJwt());
    // No sessionStorage flag.

    render(<Home />);

    await waitFor(() => {
      expect(screen.queryByText(/two-factor authentication setup/i)).toBeNull();
    });
  });
});
