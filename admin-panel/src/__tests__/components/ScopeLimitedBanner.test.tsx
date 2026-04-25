import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ScopeLimitedBanner } from '@/components/ScopeLimitedBanner';

describe('ScopeLimitedBanner', () => {
  it('renders 2FA-setup copy when scope=2fa-setup-only', () => {
    render(<ScopeLimitedBanner scope="2fa-setup-only" onLogout={() => {}} />);
    expect(screen.getByText(/two-factor authentication setup/i)).toBeTruthy();
  });

  it('renders password-change copy when scope=change-password', () => {
    render(<ScopeLimitedBanner scope="change-password" onLogout={() => {}} />);
    expect(screen.getByText(/Change your password/i)).toBeTruthy();
  });

  it('invokes onLogout when sign-out button is clicked', () => {
    const onLogout = vi.fn();
    render(<ScopeLimitedBanner scope="2fa-setup-only" onLogout={onLogout} />);
    fireEvent.click(screen.getByRole('button', { name: /sign out/i }));
    expect(onLogout).toHaveBeenCalledTimes(1);
  });
});
