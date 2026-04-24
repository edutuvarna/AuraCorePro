import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PermissionRequestsPage } from '@/views/PermissionRequestsPage';

const { pending } = vi.hoisted(() => ({
  pending: [
    { id: 'r1', permissionKey: 'tab:updates', reason: 'x'.repeat(60), status: 'pending', requestedAt: '', adminEmail: 'a@x.com' },
    { id: 'r2', permissionKey: 'action:users.delete', reason: 'x'.repeat(60), status: 'pending', requestedAt: '', adminEmail: 'b@x.com' },
  ],
}));

vi.mock('@/lib/api', () => ({
  api: {
    listPermissionRequests: vi.fn().mockResolvedValue({ items: pending }),
    approvePermissionRequest: vi.fn().mockResolvedValue({ ok: true }),
    denyPermissionRequest: vi.fn().mockResolvedValue({ ok: true }),
    bulkApprovePermissionRequests: vi.fn().mockResolvedValue({ ok: true }),
  },
}));

vi.mock('@/lib/signalr', () => ({ on: () => {}, off: () => {} }));

describe('PermissionRequestsPage', () => {
  it('renders pending requests', async () => {
    render(<PermissionRequestsPage />);
    await waitFor(() => expect(screen.getByText(/a@x.com/)).toBeTruthy());
    expect(screen.getByText(/b@x.com/)).toBeTruthy();
  });

  it('approves a single request', async () => {
    const { api } = await import('@/lib/api');
    render(<PermissionRequestsPage />);
    await waitFor(() => screen.getByText(/a@x.com/));
    fireEvent.click(screen.getAllByRole('button', { name: /approve/i })[0]);
    await waitFor(() => expect(api.approvePermissionRequest).toHaveBeenCalled());
  });
});
