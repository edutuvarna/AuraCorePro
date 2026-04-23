/**
 * Subscriptions page — grant Pro/Enterprise subscriptions by user ID
 * (Phase 6.10 W2.T6 — extracted from page.tsx).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-5 + 7-10 follow the same
 * convention).
 *
 * Tiny form-only tab — no shared primitives are used here, so Strategy B
 * (inline KPICard/StatusBadge/EmptyState/SearchBar/Pagination/TierBadge
 * duplication) does not apply. Future revoke-list table is out of scope for
 * Wave 2; this is a 1:1 lift from the monolith.
 */

'use client';

import { useState } from 'react';
import { Crown } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';

export function SubscriptionsPage() {
    const [userId, setUserId] = useState('');
    const [tier, setTier] = useState('pro');
    const [days, setDays] = useState('30');
    const [msg, setMsg] = useState('');

    const handleGrant = async () => {
        if (!userId) { setMsg('Enter a user ID'); return; }
        const { ok } = await api.grantSubscription(userId, tier, parseInt(days));
        setMsg(ok ? 'Subscription granted!' : 'Failed to grant subscription');
    };

    return (
        <div className="animate-fade-in">
            <PageHeader title="Subscriptions" subtitle="Grant or revoke user subscriptions" />
            <div className="glass-card p-6 max-w-xl">
                <h3 className="font-display font-semibold mb-5">Grant Subscription</h3>
                <div className="space-y-4">
                    <div>
                        <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">User ID</label>
                        <input value={userId} onChange={e => setUserId(e.target.value)} className="input-dark w-full" placeholder="User GUID" />
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                        <div>
                            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Tier</label>
                            <select value={tier} onChange={e => setTier(e.target.value)} className="input-dark w-full">
                                <option value="pro">Pro</option>
                                <option value="enterprise">Enterprise</option>
                            </select>
                        </div>
                        <div>
                            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Days</label>
                            <input type="number" value={days} onChange={e => setDays(e.target.value)} className="input-dark w-full" />
                        </div>
                    </div>
                    <button onClick={handleGrant} className="btn-primary flex items-center gap-2"><Crown className="w-4 h-4" />Grant Access</button>
                    {msg && <p className={`text-sm ${msg.includes('!') ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</p>}
                </div>
            </div>
        </div>
    );
}
