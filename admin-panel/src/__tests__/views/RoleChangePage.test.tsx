import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { RoleContext } from '@/lib/roleContext';
import { RoleChangePage } from '@/views/RoleChangePage';

vi.mock('@/lib/api', () => ({
  api: new Proxy({}, { get: () => () => Promise.resolve(null) }),
}));

describe('RoleChangePage role-based gate', () => {
  it('renders the locked placeholder for admin role with explicit copy', () => {
    render(
      <RoleContext.Provider value="admin">
        <RoleChangePage />
      </RoleContext.Provider>
    );
    expect(screen.getByText(/Role Change is locked/i)).toBeTruthy();
    expect(screen.getByText(/restricted to superadmin role/i)).toBeTruthy();
    expect(screen.queryByRole('button', { name: /request permission/i })).toBeNull();
    expect(screen.queryByPlaceholderText(/User ID/i)).toBeNull();
  });

  it('renders the actual page UI for superadmin role', () => {
    render(
      <RoleContext.Provider value="superadmin">
        <RoleChangePage />
      </RoleContext.Provider>
    );
    expect(screen.getByPlaceholderText(/User ID/i)).toBeTruthy();
    expect(screen.queryByText(/Role Change is locked/i)).toBeNull();
  });
});
