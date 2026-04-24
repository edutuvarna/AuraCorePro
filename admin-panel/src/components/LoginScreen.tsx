'use client';

import { useState } from 'react';
import { Shield, AlertCircle, RefreshCw, Lock } from 'lucide-react';
import { api, setToken } from '@/lib/api';
import type { UserRole } from '@/lib/types';

export interface LoginScreenProps {
    onLogin: (role: UserRole) => void;
}

export function LoginScreen({ onLogin }: LoginScreenProps) {
    const [email, setEmail] = useState('');
    const [password, setPassword] = useState('');
    const [totpCode, setTotpCode] = useState('');
    const [needs2fa, setNeeds2fa] = useState(false);
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        setLoading(true); setError('');
        if (needs2fa && totpCode) {
            const API = process.env.NEXT_PUBLIC_API_URL || 'https://api.auracore.pro';
            const res = await fetch(`${API}/api/auth/login`, {
                method: 'POST', headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ email, password, totpCode })
            });
            const result = await res.json();
            setLoading(false);
            if (res.ok && result.accessToken) {
                setToken(result.accessToken);
                localStorage.setItem('aura_token', result.accessToken);
                const role2fa: UserRole | undefined = result.user?.role;
                if (role2fa === 'admin' || role2fa === 'superadmin') { onLogin(role2fa); return; }
                setError('Access denied. Admin role required.'); setToken(null); return;
            }
            setError(result.error || '2FA verification failed'); return;
        }
        const { ok, data } = await api.login(email, password);
        setLoading(false);
        if (data.requires2fa && !totpCode) { setNeeds2fa(true); return; }
        const role: UserRole | undefined = data.user?.role;
        if (ok && (role === 'admin' || role === 'superadmin')) { onLogin(role); }
        else if (ok) { setError('Access denied. Admin role required.'); setToken(null); }
        else { setError(data.error || 'Authentication failed'); }
    };

    return (
        <div className="min-h-screen flex items-center justify-center p-4 relative overflow-hidden">
            {/* Ambient background */}
            <div className="absolute inset-0">
                <div className="absolute top-0 left-1/3 w-[600px] h-[600px] bg-accent/[0.07] rounded-full blur-[120px] animate-pulse" />
                <div className="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-aura-purple/[0.05] rounded-full blur-[100px]" />
                <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-accent/[0.03] rounded-full blur-[150px]" />
            </div>

            <div className="relative w-full max-w-md">
                {/* Logo */}
                <div className="text-center mb-8">
                    <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-accent/10 border border-accent/20 mb-4 accent-glow">
                        <Shield className="w-8 h-8 text-accent" />
                    </div>
                    <h1 className="text-2xl font-display font-bold">AuraCore Pro</h1>
                    <p className="text-white/40 text-sm mt-1">Administration Console</p>
                </div>

                {/* Login Card */}
                <form onSubmit={handleSubmit} className="glass-card p-8 space-y-5">
                    <div>
                        <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Email</label>
                        <input type="email" value={email} onChange={e => setEmail(e.target.value)}
                            className="input-dark w-full" placeholder="admin@auracore.pro" required />
                    </div>
                    <div>
                        <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Password</label>
                        <input type="password" value={password} onChange={e => setPassword(e.target.value)}
                            className="input-dark w-full" placeholder="Enter password" required />
                    </div>
                    {needs2fa && (
                        <div className="animate-fade-in">
                            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">2FA Code</label>
                            <input type="text" value={totpCode} onChange={e => setTotpCode(e.target.value)}
                                className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" maxLength={6} autoFocus />
                        </div>
                    )}
                    {error && (
                        <div className="flex items-center gap-2 text-aura-red text-sm bg-aura-red/10 border border-aura-red/20 rounded-xl px-4 py-3">
                            <AlertCircle className="w-4 h-4 shrink-0" />{error}
                        </div>
                    )}
                    <button type="submit" disabled={loading} className="btn-primary w-full flex items-center justify-center gap-2">
                        {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Lock className="w-4 h-4" />}
                        {loading ? 'Authenticating...' : needs2fa ? 'Verify 2FA' : 'Sign In'}
                    </button>
                </form>
            </div>
        </div>
    );
}
