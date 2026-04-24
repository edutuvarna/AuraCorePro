import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AdminManagementPage } from '@/views/AdminManagementPage';

const { items } = vi.hoisted(() => ({
  items: [
    { id: 'u1', email: 'a@x.com', role: 'admin', isActive: true, isReadonly: false, totpEnabled: true, require2fa: true, createdAt: '2026-04-01' },
  ],
}));

vi.mock('@/lib/api', () => ({
  api: {
    listAdminAccounts: vi.fn().mockResolvedValue({ items }),
    createAdminAccount: vi.fn().mockResolvedValue({ ok: true, data: {} }),
    suspendAdmin: vi.fn(),
    restoreAdmin: vi.fn(),
    deleteAdmin: vi.fn(),
    resetAdminPassword: vi.fn(),
  },
}));

describe('AdminManagementPage', () => {
  it('lists admins and opens create modal', async () => {
    render(<AdminManagementPage />);
    await waitFor(() => screen.getByText('a@x.com'));
    fireEvent.click(screen.getByRole('button', { name: /\+ create admin/i }));
    expect(screen.getByText(/new admin account/i)).toBeTruthy();
  });
});
