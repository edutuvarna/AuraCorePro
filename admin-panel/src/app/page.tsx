'use client';

import { useState, useEffect } from 'react';
import { Layers } from 'lucide-react';
import { api, setToken } from '@/lib/api';
import { startConnection, stopConnection } from '@/lib/signalr';
import { LoginScreen } from '@/components/LoginScreen';
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

  useEffect(() => {
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
  if (!authenticated) return <LoginScreen onLogin={(r, scope) => {
    setRole(r); setAuthenticated(true); startConnection();
    if (scope === '2fa-setup-only') setPostLoginView('enable2fa');
    else if (scope === 'change-password') setPostLoginView('changePw');
  }} />;
  return <AdminPanelInner role={role} onLogout={handleLogout} initialPage={postLoginView ?? 'dashboard'} />;
}
