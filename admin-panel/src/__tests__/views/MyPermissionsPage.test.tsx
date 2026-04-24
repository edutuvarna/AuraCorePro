import { render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { MyPermissionsPage } from '@/views/MyPermissionsPage';

vi.mock('@/lib/api', () => ({
  api: {
    getMyPermissions: vi.fn().mockResolvedValue({
      totalRestricted: 10, activeGrantsCount: 2,
      grants: [{ permissionKey: 'tab:updates', grantedAt: '2026-04-01', expiresAt: null, grantedByEmail: 'super@x.com' }],
      pending: [{ id: 'p1', permissionKey: 'tab:configuration', reason: 'needed urgently', requestedAt: '2026-04-22' }],
      recentDenials: [{ permissionKey: 'action:users.delete', reviewNote: 'wrong queue', reviewedAt: '2026-04-20' }],
    }),
    cancelPermissionRequest: vi.fn().mockResolvedValue({ ok: true }),
  },
}));

describe('MyPermissionsPage', () => {
  it('renders all three tables + summary', async () => {
    render(<MyPermissionsPage />);
    await waitFor(() => screen.getByText(/2 of 10/));
    expect(screen.getByText(/tab:updates/)).toBeTruthy();
    expect(screen.getByText(/tab:configuration/)).toBeTruthy();
    expect(screen.getByText(/wrong queue/)).toBeTruthy();
  });
});
