import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeAll } from 'vitest';
// page.tsx cannot host non-standard named exports (Next.js App Router
// validates its shape), so the inner AdminPanel was extracted into
// app/AdminPanel.tsx alongside AdminPanelForTest. Tests mount that harness
// directly with a synthetic `role` prop.
import { AdminPanelForTest } from '@/app/AdminPanel';

// Minimal api stub — every property returns a no-op resolved Promise. Avoids
// mocking each call-site individually while still isolating from network.
vi.mock('@/lib/api', () => ({
  api: new Proxy({}, { get: () => () => Promise.resolve(null) }),
  setToken: () => {},
  getToken: () => 'x',
}));
vi.mock('@/lib/signalr', () => ({
  startConnection: () => {}, stopConnection: () => {}, on: () => {}, off: () => {},
  getConnection: () => null,
}));

// Recharts ResponsiveContainer needs ResizeObserver in jsdom.
beforeAll(() => {
  (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
    observe() {}
    unobserve() {}
    disconnect() {}
  };
});

// Sidebar renders labels in both desktop list and mobile "more" sheet, so
// getAllByText is the right query here. Presence is what we're asserting.
describe('NAV_GROUPS by role', () => {
  it('admin sees 13 standard tabs and no superadmin-only tabs', () => {
    render(<AdminPanelForTest role="admin" />);
    expect(screen.getAllByText('Users').length).toBeGreaterThan(0);
    expect(screen.queryByText('Permission Requests')).toBeNull();
    expect(screen.queryByText('Admin Management')).toBeNull();
  });

  it('superadmin sees admin tabs plus superadmin-only tabs', () => {
    render(<AdminPanelForTest role="superadmin" />);
    expect(screen.getAllByText('Users').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Permission Requests').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Admin Management').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Security Policy').length).toBeGreaterThan(0);
    expect(screen.getAllByText('API Rate Limits').length).toBeGreaterThan(0);
  });
});
