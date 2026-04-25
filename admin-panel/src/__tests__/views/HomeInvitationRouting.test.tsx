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
