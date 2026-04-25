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
