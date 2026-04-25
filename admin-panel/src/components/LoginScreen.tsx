'use client';

import { useRef, useState } from 'react';
import { Shield, AlertCircle, RefreshCw, Lock, Crown } from 'lucide-react';
import { Turnstile, type TurnstileInstance } from '@marsidev/react-turnstile';
import { api, setToken } from '@/lib/api';
import type { UserRole } from '@/lib/types';

export interface LoginScreenProps {
  onLogin: (role: UserRole, scope?: 'normal' | '2fa-setup-only' | 'change-password') => void;
}

export function LoginScreen({ onLogin }: LoginScreenProps) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [needs2fa, setNeeds2fa] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState<null | 'admin' | 'superadmin'>(null);
  const [turnstileToken, setTurnstileToken] = useState<string | null>(null);
  // Phase 6.12 polish: continuation token issued by the password-step response
  // when 2FA is required. Forwarded on the TOTP-step submit so the backend
  // skips Turnstile verification — single CAPTCHA per login session.
  const [continuationToken, setContinuationToken] = useState<string | null>(null);
  const turnstileRef = useRef<TurnstileInstance | null>(null);

  // Turnstile tokens are single-use + 300s TTL. After any submit attempt
  // (success OR failure including requires2fa partial), reset the widget so
  // the next click gets a fresh token instead of replaying the consumed one
  // (CF returns success=false → backend sees 400 captcha_invalid).
  const resetTurnstile = () => {
    setTurnstileToken(null);
    turnstileRef.current?.reset();
  };

  const submit = async (mode: 'admin' | 'superadmin') => {
    setLoading(mode); setError('');
    try {
      const { ok, data } = mode === 'admin'
        ? await api.login(email, password, totpCode || undefined, turnstileToken || undefined, continuationToken || undefined)
        : await api.superadminLogin(email, password, totpCode || undefined, turnstileToken || undefined, continuationToken || undefined);

      if (data?.requires2fa && !totpCode) {
        setNeeds2fa(true);
        // Phase 6.12 polish: store the continuation token so the next click
        // can skip Turnstile. DO NOT call resetTurnstile() — the second submit
        // doesn't use a fresh Turnstile token (it uses the continuation token
        // instead), and resetting would force the user through another
        // CAPTCHA challenge for nothing.
        setContinuationToken(data.twoFactorContinuationToken ?? null);
        return;
      }

      if (data?.requiresTwoFactorSetup && data.accessToken) {
        setToken(data.accessToken);
        if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
        setContinuationToken(null);
        onLogin(data.user?.role ?? mode, '2fa-setup-only');
        return;
      }

      if (data?.requiresPasswordChange && data.accessToken) {
        setToken(data.accessToken);
        if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
        setContinuationToken(null);
        onLogin(data.user?.role ?? mode, 'change-password');
        return;
      }

      if (ok && data.accessToken) {
        setToken(data.accessToken);
        if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
        const role: UserRole = data.user?.role ?? mode;
        if (role !== 'admin' && role !== 'superadmin') {
          setError('Access denied. Admin role required.');
          setToken(null);
          setContinuationToken(null);
          resetTurnstile();
          return;
        }
        setContinuationToken(null);
        onLogin(role);
        return;
      }
      setError(data?.error || 'Authentication failed');
      // Failure on the TOTP step (e.g. wrong code) — the continuation was
      // single-use and is now consumed server-side. Clear it so the next
      // attempt falls back to Turnstile.
      setContinuationToken(null);
      resetTurnstile();
    } finally { setLoading(null); }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 relative overflow-hidden">
      <div className="absolute inset-0">
        <div className="absolute top-0 left-1/3 w-[600px] h-[600px] bg-accent/[0.07] rounded-full blur-[120px] animate-pulse" />
        <div className="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-aura-purple/[0.05] rounded-full blur-[100px]" />
      </div>

      <div className="relative w-full max-w-md">
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-accent/10 border border-accent/20 mb-4 accent-glow">
            <Shield className="w-8 h-8 text-accent" />
          </div>
          <h1 className="text-2xl font-display font-bold">AuraCore Pro</h1>
          <p className="text-white/40 text-sm mt-1">Administration Console</p>
        </div>

        <form
          name="signin"
          method="post"
          onSubmit={e => { e.preventDefault(); submit('admin'); }}
          className="glass-card p-8 space-y-5"
        >
          <div>
            <label htmlFor="email" className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Email</label>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="username"
              inputMode="email"
              value={email}
              onChange={e => setEmail(e.target.value)}
              className="input-dark w-full"
              placeholder="admin@auracore.pro"
              required
            />
          </div>
          <div>
            <label htmlFor="password" className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Password</label>
            <input
              id="password"
              name="password"
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={e => setPassword(e.target.value)}
              className="input-dark w-full"
              placeholder="Enter password"
              required
            />
          </div>
          {needs2fa && (
            <div className="animate-fade-in">
              <label htmlFor="totp" className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">2FA Code</label>
              <input
                id="totp"
                name="totp"
                type="text"
                autoComplete="one-time-code"
                inputMode="numeric"
                pattern="[0-9]*"
                value={totpCode}
                onChange={e => setTotpCode(e.target.value)}
                className="input-dark w-full text-center tracking-[0.5em] text-lg"
                placeholder="000000"
                maxLength={6}
                autoFocus
              />
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 text-aura-red text-sm bg-aura-red/10 border border-aura-red/20 rounded-xl px-4 py-3">
              <AlertCircle className="w-4 h-4 shrink-0" />{error}
            </div>
          )}
          <div className="flex justify-center">
            <Turnstile
              ref={turnstileRef}
              siteKey={process.env.NEXT_PUBLIC_TURNSTILE_SITE_KEY!}
              onSuccess={(token) => setTurnstileToken(token)}
              onError={() => setTurnstileToken(null)}
              onExpire={() => setTurnstileToken(null)}
              options={{ theme: 'dark' }}
            />
          </div>
          <button type="submit" disabled={loading !== null || (!turnstileToken && !continuationToken)}
            className="btn-primary w-full flex items-center justify-center gap-2">
            {loading === 'admin' ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Lock className="w-4 h-4" />}
            {loading === 'admin' ? 'Authenticating...' : needs2fa ? 'Verify 2FA' : 'Sign In as Admin'}
          </button>
          <button type="button" disabled={loading !== null || (!turnstileToken && !continuationToken)}
            onClick={() => submit('superadmin')}
            className="w-full flex items-center justify-center gap-2 py-3 rounded-xl font-semibold transition
                       bg-gradient-to-r from-accent to-aura-purple text-black hover:opacity-90 disabled:opacity-50">
            {loading === 'superadmin' ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Crown className="w-4 h-4" />}
            {loading === 'superadmin' ? 'Authenticating...' : 'Sign In as Superadmin'}
          </button>
        </form>
      </div>
    </div>
  );
}
