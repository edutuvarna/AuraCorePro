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

    const rowCheckboxes = screen.getAllByRole('checkbox').filter((c) => c.getAttribute('data-row-checkbox'));
    expect(rowCheckboxes.length).toBe(2);

    fireEvent.click(rowCheckboxes[0]);
    await waitFor(() => screen.getByText(/1 selected/i));

    fireEvent.click(rowCheckboxes[1]);
    await waitFor(() => screen.getByText(/2 selected/i));
  });

  it('clicking Demote N opens BulkRoleChangeModal showing both rows', async () => {
    render(<AdminManagementPage />);
    await waitFor(() => screen.getByText('a@x.com'));
    const rowCheckboxes = screen.getAllByRole('checkbox').filter((c) => c.getAttribute('data-row-checkbox'));
    fireEvent.click(rowCheckboxes[0]);
    fireEvent.click(rowCheckboxes[1]);
    fireEvent.click(screen.getByText(/Demote 2 selected/i));

    await waitFor(() => screen.getByText(/Bulk Demote/i));
    // Modal lists each selected admin
    const modal = screen.getByText(/Bulk Demote/i).closest('div')!.parentElement!;
    expect(modal.textContent).toContain('a@x.com');
    expect(modal.textContent).toContain('b@x.com');
  });

  it('Cancel button clears selection', async () => {
    render(<AdminManagementPage />);
    await waitFor(() => screen.getByText('a@x.com'));
    const rowCheckboxes = screen.getAllByRole('checkbox').filter((c) => c.getAttribute('data-row-checkbox'));
    fireEvent.click(rowCheckboxes[0]);
    await waitFor(() => screen.getByText(/1 selected/i));

    // Cancel button in the sticky toolbar
    fireEvent.click(screen.getByText(/Cancel/i));
    await waitFor(() => {
      expect(screen.queryByText(/selected/i)).toBeNull();
    });
  });
});
