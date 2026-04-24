import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PermissionRequestDialog } from '@/components/PermissionRequestDialog';

describe('PermissionRequestDialog', () => {
  it('blocks submit when reason < 50 chars', () => {
    const onSubmit = vi.fn();
    render(<PermissionRequestDialog permissionKey="tab:updates" isOpen onClose={() => {}} onSubmit={onSubmit} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'too short' } });
    fireEvent.click(screen.getByRole('button', { name: /submit/i }));
    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByText(/at least 50/i)).toBeTruthy();
  });

  it('submits valid reason', async () => {
    const onSubmit = vi.fn().mockResolvedValue(true);
    render(<PermissionRequestDialog permissionKey="tab:updates" isOpen onClose={() => {}} onSubmit={onSubmit} />);
    fireEvent.change(screen.getByRole('textbox'), {
      target: { value: 'I need to publish a new release for the Q2 rollout that customers are waiting on urgently.' },
    });
    fireEvent.click(screen.getByRole('button', { name: /submit/i }));
    expect(onSubmit).toHaveBeenCalledWith('tab:updates', expect.stringContaining('Q2 rollout'));
  });

  it('shows char counter', () => {
    render(<PermissionRequestDialog permissionKey="tab:updates" isOpen onClose={() => {}} onSubmit={async () => true} />);
    fireEvent.change(screen.getByRole('textbox'), { target: { value: 'x'.repeat(75) } });
    expect(screen.getByText(/75 \/ 500/)).toBeTruthy();
  });
});
