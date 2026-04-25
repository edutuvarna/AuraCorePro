'use client';

import { useState, useEffect } from 'react';
import { Layers } from 'lucide-react';
import { api, setToken } from '@/lib/api';
import { startConnection, stopConnection } from '@/lib/signalr';
import { LoginScreen } from '@/components/LoginScreen';
import { RedeemInvitationPage } from '@/views/RedeemInvitationPage';
import { AdminPanelInner, type Page } from './AdminPanel';
import type { UserRole } from '@/lib/types';

function decodeRoleFromJwt(token: string | null): UserRole {
  if (!token) return 'admin';
  try {
    const payload = JSON.parse(atob(token.split('.')[1]!));
    const roles: string[] = Array.isArray(payload[
      'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'
    ]) ? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
       : [payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']].filter(Boolean);
    if (roles.includes('superadmin')) return 'superadmin';
    if (roles.includes('admin')) return 'admin';
  } catch {}
  return 'admin';
}

export default function Home() {
  const [authenticated, setAuthenticated] = useState(false);
  const [role, setRole] = useState<UserRole>('admin');
  const [checking, setChecking] = useState(true);
  const [postLoginView, setPostLoginView] = useState<Page | null>(null);
  const [postLoginScope, setPostLoginScope] = useState<'normal' | '2fa-setup-only' | 'change-password'>('normal');
  const [redeemInvite, setRedeemInvite] = useState(false);

  useEffect(() => {
    if (typeof window !== 'undefined' && window.location.hash.startsWith('#/invite')) {
      setRedeemInvite(true);
    }
  }, []);

  useEffect(() => {
    // Phase 6.13.6 followup: if we just came from RedeemInvitationPage,
    // honor the post-redeem 2FA-setup scope before the auth check resolves.
    // This is read once and cleared so that subsequent loads of the same
    // session land on dashboard normally.
    if (typeof window !== 'undefined' && sessionStorage.getItem('aura_post_redeem_force_2fa') === '1') {
      sessionStorage.removeItem('aura_post_redeem_force_2fa');
      setPostLoginView('enable2fa');
      setPostLoginScope('2fa-setup-only');
    }
    const saved = typeof window !== 'undefined' ? localStorage.getItem('aura_token') : null;
    if (saved) {
      setToken(saved);
      api.getStats().then(data => {
        if (data) {
          setAuthenticated(true);
          setRole(decodeRoleFromJwt(saved));
          startConnection();
        } else { setToken(null); localStorage.removeItem('aura_token'); }
        setChecking(false);
      });
    } else setChecking(false);
  }, []);

  const handleLogout = () => {
    setToken(null); localStorage.removeItem('aura_token'); stopConnection(); setAuthenticated(false);
  };

  if (checking) return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="flex flex-col items-center gap-4">
        <div className="w-12 h-12 rounded-2xl bg-accent/10 border border-accent/20 flex items-center justify-center animate-pulse-glow">
          <Layers className="w-6 h-6 text-accent" />
        </div>
        <p className="text-sm text-white/30">Loading...</p>
      </div>
    </div>
  );
  // Phase 6.13.6 — invitation deep-link. Mount RedeemInvitationPage before
  // LoginScreen so an unauthenticated visitor with the invite hash lands on
  // the password-set form. RedeemInvitationPage parses its own hash params
  // and assigns location='/' on success, which clears the hash and triggers
  // the normal authenticated render path.
  if (redeemInvite && !authenticated) return <RedeemInvitationPage />;
  if (!authenticated) return <LoginScreen onLogin={(r, scope) => {
    setRole(r); setAuthenticated(true); startConnection();
    if (scope === '2fa-setup-only') { setPostLoginView('enable2fa'); setPostLoginScope('2fa-setup-only'); }
    else if (scope === 'change-password') { setPostLoginView('changePw'); setPostLoginScope('change-password'); }
    else setPostLoginScope('normal');
  }} />;
  return <AdminPanelInner role={role} onLogout={handleLogout} initialPage={postLoginView ?? 'dashboard'} scope={postLoginScope} />;
}
