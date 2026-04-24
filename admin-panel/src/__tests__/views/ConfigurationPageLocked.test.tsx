import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ConfigurationPage } from '@/views/ConfigurationPage';
import { RoleContext } from '@/lib/roleContext';

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ loading: false, has: () => false, hasPending: () => false, data: null, refresh: () => {} }),
}));

describe('ConfigurationPage permission gating', () => {
  it('renders LockedTabPlaceholder when admin lacks tab:configuration', () => {
    render(
      <RoleContext.Provider value="admin"><ConfigurationPage /></RoleContext.Provider>
    );
    expect(screen.getByText(/disabled by the superadmin by default/i)).toBeTruthy();
  });
});
