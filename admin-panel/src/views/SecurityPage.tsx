/**
 * Security page — Two-Factor Authentication (TOTP) setup / verify / disable
 * flow. Backend issues a QR code + manual entry key on setup, then verifies
 * a 6-digit TOTP code; disable also requires a current 6-digit code.
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-10 follow the same
 * convention).
 *
 * StatusBadge lifted in W2.T11 to shared `@/components/`. No other inline
 * primitives needed (the QR/code/buttons are bespoke to the 2FA flow).
 *
 * Phase 6.10 W2.T13 — extracted from page.tsx (final wave 2 cleanup; Task 10
 * left this inline pending Task 13's monolith trim to ≤ 100 lines).
 */

'use client';

import { useState, useEffect } from 'react';
import {
    ShieldCheck, RefreshCw, Lock, Unlock, Check,
} from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';

export function SecurityPage() {
    const [status, setStatus] = useState<any>(null);
    const [setupData, setSetupData] = useState<any>(null);
    const [code, setCode] = useState('');
    const [msg, setMsg] = useState('');
    const [loading, setLoading] = useState(false);

    useEffect(() => { api.get2faStatus().then(setStatus); }, []);

    const handleSetup = async () => {
        setLoading(true); setMsg('');
        const data = await api.setup2fa();
        setLoading(false);
        if (data?.error) { setMsg(data.error); return; }
        setSetupData(data);
    };

    const handleVerify = async () => {
        if (code.length !== 6) { setMsg('Enter 6-digit code'); return; }
        setLoading(true); setMsg('');
        const { ok, data } = await api.verify2fa(code);
        setLoading(false);
        if (ok) { setMsg('2FA enabled!'); setStatus({ enabled: true }); setSetupData(null); setCode(''); }
        else setMsg(data?.error || 'Verification failed');
    };

    const handleDisable = async () => {
        if (code.length !== 6) { setMsg('Enter current 2FA code to disable'); return; }
        setLoading(true); setMsg('');
        const { ok, data } = await api.disable2fa(code);
        setLoading(false);
        if (ok) { setMsg('2FA disabled'); setStatus({ enabled: false }); setCode(''); }
        else setMsg(data?.error || 'Failed to disable');
    };

    return (
        <div className="animate-fade-in">
            <PageHeader title="Security" subtitle="Two-factor authentication and account security" />
            <div className="glass-card p-6 max-w-xl">
                <div className="flex items-center justify-between mb-6">
                    <div className="flex items-center gap-3">
                        <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${status?.enabled ? 'bg-aura-green/10' : 'bg-aura-amber/10'}`}>
                            <ShieldCheck className={`w-5 h-5 ${status?.enabled ? 'text-aura-green' : 'text-aura-amber'}`} />
                        </div>
                        <div>
                            <p className="font-medium">Two-Factor Authentication</p>
                            <p className="text-xs text-white/40">TOTP via Google Authenticator or similar</p>
                        </div>
                    </div>
                    <StatusBadge status={status?.enabled ? 'Active' : 'Disabled'} />
                </div>
                {!status?.enabled && !setupData && (
                    <div className="bg-white/[0.02] rounded-xl p-4 mb-4">
                        <p className="text-sm text-white/60 mb-3">Enable 2FA to add an extra layer of security. You will need an authenticator app like Google Authenticator or Authy.</p>
                        <button onClick={handleSetup} disabled={loading} className="btn-primary flex items-center gap-2">
                            {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Lock className="w-4 h-4" />}Enable 2FA
                        </button>
                    </div>
                )}
                {setupData && (
                    <div className="space-y-4 animate-slide-up">
                        <div className="bg-white/[0.02] rounded-xl p-4 text-center">
                            <p className="text-sm text-white/60 mb-3">Scan this QR code with your authenticator app:</p>
                            {setupData.qrCodeDataUrl && <img src={setupData.qrCodeDataUrl} alt="QR" className="mx-auto w-48 h-48 rounded-xl bg-white p-2" />}
                            {setupData.manualEntryKey && (
                                <div className="mt-3">
                                    <p className="text-[10px] text-white/30 uppercase tracking-wider mb-1">Manual entry key</p>
                                    <code className="text-xs font-mono text-accent bg-accent/10 px-3 py-1.5 rounded-lg select-all">{setupData.manualEntryKey}</code>
                                </div>
                            )}
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Verification Code</label>
                            <input type="text" value={code} onChange={e => setCode(e.target.value)} maxLength={6}
                                className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" />
                        </div>
                        <button onClick={handleVerify} disabled={loading} className="btn-primary w-full flex items-center justify-center gap-2">
                            {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}Verify and Enable
                        </button>
                    </div>
                )}
                {status?.enabled && !setupData && (
                    <div className="space-y-4">
                        <div className="bg-aura-green/5 border border-aura-green/10 rounded-xl p-4">
                            <p className="text-sm text-aura-green/80">2FA is active. Your account is protected with time-based one-time passwords.</p>
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Enter current 2FA code to disable</label>
                            <input type="text" value={code} onChange={e => setCode(e.target.value)} maxLength={6}
                                className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" />
                        </div>
                        <button onClick={handleDisable} disabled={loading} className="btn-danger w-full flex items-center justify-center gap-2">
                            {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Unlock className="w-4 h-4" />}Disable 2FA
                        </button>
                    </div>
                )}
                {msg && <p className={`text-sm mt-4 ${msg.includes('enabled') || msg.includes('disabled') ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</p>}
            </div>
        </div>
    );
}
