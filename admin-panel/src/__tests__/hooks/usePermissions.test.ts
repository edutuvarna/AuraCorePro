import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { usePermissions } from '@/hooks/usePermissions';

vi.mock('@/lib/api', () => ({
  api: {
    getMyPermissions: vi.fn().mockResolvedValue({
      totalRestricted: 10,
      activeGrantsCount: 2,
      grants: [
        { permissionKey: 'action:users.delete', grantedAt: '', expiresAt: null },
        { permissionKey: 'tab:updates', grantedAt: '', expiresAt: null },
      ],
      pending: [],
      recentDenials: [],
    }),
  },
}));

describe('usePermissions', () => {
  it('returns a has() predicate that reports granted keys', async () => {
    const { result } = renderHook(() => usePermissions('admin'));
    await waitFor(() => expect(result.current.loading).toBe(false));
    expect(result.current.has('action:users.delete')).toBe(true);
    expect(result.current.has('action:users.ban')).toBe(false);
  });

  it('superadmin has all permissions without a fetch', () => {
    const { result } = renderHook(() => usePermissions('superadmin'));
    expect(result.current.has('action:users.delete')).toBe(true);
    expect(result.current.has('tab:configuration')).toBe(true);
  });
});
