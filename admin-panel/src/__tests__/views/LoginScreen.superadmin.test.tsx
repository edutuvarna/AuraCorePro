import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LoginScreen } from '@/components/LoginScreen';

// Auto-resolve Turnstile in tests so the submit button enables.
// The real widget makes a network call to challenges.cloudflare.com which
// jsdom can't service; stubbing also unblocks `disabled={!turnstileToken}`.
vi.mock('@marsidev/react-turnstile', () => ({
  Turnstile: ({ onSuccess }: any) => {
    onSuccess?.('stub-token');
    return null;
  },
}));

vi.mock('@/lib/api', () => ({
  api: {
    login: vi.fn().mockResolvedValue({ ok: true, data: { accessToken: 'x', user: { role: 'admin' } } }),
    superadminLogin: vi.fn().mockResolvedValue({ ok: true, data: { accessToken: 'y', user: { role: 'superadmin' } } }),
  },
  setToken: () => {},
}));

describe('LoginScreen', () => {
  it('exposes both admin and superadmin sign-in buttons', () => {
    render(<LoginScreen onLogin={() => {}} />);
    expect(screen.getByRole('button', { name: /sign in as admin/i })).toBeTruthy();
    expect(screen.getByRole('button', { name: /sign in as superadmin/i })).toBeTruthy();
  });

  it('posts to /api/auth/superadmin/login when superadmin button is clicked', async () => {
    const { api } = await import('@/lib/api');
    const onLogin = vi.fn();
    render(<LoginScreen onLogin={onLogin} />);

    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: 'boss@x.com' } });
    fireEvent.change(screen.getByLabelText(/password/i), { target: { value: 'GoodPass12' } });
    fireEvent.click(screen.getByRole('button', { name: /sign in as superadmin/i }));

    await waitFor(() => expect(api.superadminLogin).toHaveBeenCalled());
    await waitFor(() => expect(onLogin).toHaveBeenCalledWith('superadmin'));
  });
});
