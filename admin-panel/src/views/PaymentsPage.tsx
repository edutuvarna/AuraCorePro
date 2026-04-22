/**
 * Payments page — recent Stripe/crypto payment transactions + pending crypto
 * approval queue (Phase 6.10 W2.T7 — extracted from page.tsx).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-6 + 7-10 follow the same
 * convention).
 *
 * StatusBadge / EmptyState are temporarily duplicated from page.tsx
 * (Strategy B). Task 11 lifts them into shared `@/components/` and call sites
 * flip to the import. DataTable conversion is Wave 3 Task 16 — keep the inline
 * `<table>` 1:1 with the monolith for now.
 */

'use client';

import { useState, useEffect } from 'react';
import { AlertCircle, Check, X, CreditCard } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';

// Temporarily inlined — Task 11 will lift to @/components/StatusBadge
function StatusBadge({ status }: { status: string }) {
    const s = status.toLowerCase();
    const cls = s === 'active' || s === 'completed' || s === 'online' ? 'badge-green'
        : s === 'pending' ? 'badge-amber'
        : s === 'cancelled' || s === 'revoked' || s === 'failed' || s === 'refunded' ? 'badge-red'
        : s === 'pro' ? 'badge-cyan'
        : s === 'enterprise' ? 'badge-purple'
        : s === 'admin' ? 'badge-red'
        : s === 'free' ? 'badge-blue'
        : 'badge-blue';
    return <span className={`badge ${cls}`}>{status}</span>;
}

// Temporarily inlined — Task 11 will lift to @/components/EmptyState
function EmptyState({ icon: Icon, title, subtitle }: { icon: any; title: string; subtitle?: string }) {
    return (
        <div className="flex flex-col items-center justify-center py-16 text-center">
            <div className="w-14 h-14 rounded-2xl bg-white/[0.03] border border-white/[0.06] flex items-center justify-center mb-4">
                <Icon className="w-6 h-6 text-white/20" />
            </div>
            <p className="text-white/40 font-medium">{title}</p>
            {subtitle && <p className="text-white/25 text-sm mt-1">{subtitle}</p>}
        </div>
    );
}

export function PaymentsPage() {
    const [payments, setPayments] = useState<any[]>([]);
    const [pending, setPending] = useState<any[]>([]);

    useEffect(() => {
        const load = async () => {
            const [p, c] = await Promise.all([api.getRecentPayments(50), api.getPendingCrypto()]);
            setPayments(p || []); setPending(c || []);
        };
        load();
    }, []);

    return (
        <div className="animate-fade-in">
            <PageHeader title="Payments" subtitle="All payment transactions" />

            {pending.length > 0 && (
                <div className="glass-card p-5 mb-5 border-aura-amber/20">
                    <h3 className="font-display font-semibold text-sm mb-4 flex items-center gap-2">
                        <AlertCircle className="w-4 h-4 text-aura-amber" />Pending Crypto ({pending.length})
                    </h3>
                    <div className="space-y-2">
                        {pending.map((p: any) => (
                            <div key={p.id} className="flex items-center justify-between py-2.5 px-3 rounded-lg bg-white/[0.02]">
                                <div>
                                    <p className="text-sm text-white/80">{p.userEmail}</p>
                                    <p className="text-xs text-white/30">${p.amount} - {p.crypto}</p>
                                </div>
                                <div className="flex gap-2">
                                    <button onClick={async () => { await api.verifyCryptoPayment(p.id); setPending(pr => pr.filter(x => x.id !== p.id)); }}
                                        className="btn-ghost text-aura-green border-aura-green/20 flex items-center gap-1"><Check className="w-3 h-3" />Approve</button>
                                    <button onClick={async () => { await api.rejectCryptoPayment(p.id); setPending(pr => pr.filter(x => x.id !== p.id)); }}
                                        className="btn-danger flex items-center gap-1"><X className="w-3 h-3" />Reject</button>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <div className="glass-card p-5">
                <table className="w-full text-sm">
                    <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                        <th className="text-left py-3 px-4 font-medium">User</th>
                        <th className="text-left py-3 px-4 font-medium">Provider</th>
                        <th className="text-left py-3 px-4 font-medium">Amount</th>
                        <th className="text-left py-3 px-4 font-medium">Status</th>
                        <th className="text-left py-3 px-4 font-medium">Date</th>
                    </tr></thead>
                    <tbody>
                        {payments.map((p: any, i: number) => (
                            <tr key={i} className="table-row">
                                <td className="py-3 px-4 text-white/80">{p.userEmail || p.email || '-'}</td>
                                <td className="py-3 px-4 text-white/50">{p.provider}</td>
                                <td className="py-3 px-4 font-semibold text-accent">${(p.amount ?? 0).toFixed(2)}</td>
                                <td className="py-3 px-4"><StatusBadge status={p.status || 'pending'} /></td>
                                <td className="py-3 px-4 text-white/40">{new Date(p.createdAt || p.date).toLocaleDateString()}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {payments.length === 0 && <EmptyState icon={CreditCard} title="No payments recorded" />}
            </div>
        </div>
    );
}
