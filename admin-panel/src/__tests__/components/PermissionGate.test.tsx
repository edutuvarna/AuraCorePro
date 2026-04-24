import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PermissionGate } from '@/components/PermissionGate';

describe('PermissionGate', () => {
  it('renders children when permission granted', () => {
    render(
      <PermissionGate permissionKey="action:users.delete" hasPermission onRequestStart={() => {}}>
        <button>Delete</button>
      </PermissionGate>
    );
    expect(screen.getByRole('button', { name: 'Delete' })).toBeTruthy();
  });

  it('renders disabled lock button when permission denied', () => {
    const onRequestStart = vi.fn();
    render(
      <PermissionGate permissionKey="action:users.delete" hasPermission={false} onRequestStart={onRequestStart}>
        <button>Delete</button>
      </PermissionGate>
    );
    // The real Delete button is NOT rendered; replaced with the locked stub.
    const lockBtn = screen.getByRole('button');
    fireEvent.click(lockBtn);
    expect(onRequestStart).toHaveBeenCalledWith('action:users.delete');
  });
});
