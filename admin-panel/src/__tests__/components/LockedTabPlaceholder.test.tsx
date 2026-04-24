import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LockedTabPlaceholder } from '@/components/LockedTabPlaceholder';

describe('LockedTabPlaceholder', () => {
  it('renders the spec message verbatim', () => {
    render(<LockedTabPlaceholder tabName="Configuration" permissionKey="tab:configuration" />);
    expect(screen.getByText(/disabled by the superadmin by default/i)).toBeTruthy();
    expect(screen.getByText(/Configuration tab/i)).toBeTruthy();
  });

  it('opens the request dialog on button click', () => {
    const onRequestStart = vi.fn();
    render(<LockedTabPlaceholder tabName="Configuration" permissionKey="tab:configuration" onRequestStart={onRequestStart} />);
    fireEvent.click(screen.getByRole('button', { name: /request permission/i }));
    expect(onRequestStart).toHaveBeenCalledWith('tab:configuration');
  });

  it('shows pending banner when hasPending=true', () => {
    render(<LockedTabPlaceholder tabName="Updates" permissionKey="tab:updates" hasPending pendingAt="2026-04-23T12:00:00Z" />);
    expect(screen.getByText(/Pending request/i)).toBeTruthy();
  });

  it('shows denial banner when lastDenial provided', () => {
    render(<LockedTabPlaceholder tabName="Updates" permissionKey="tab:updates" lastDenial={{ reviewNote: 'wrong team', reviewedAt: '2026-04-23T10:00:00Z' }} />);
    expect(screen.getByText(/Last request denied/i)).toBeTruthy();
    expect(screen.getByText(/wrong team/)).toBeTruthy();
  });
});
