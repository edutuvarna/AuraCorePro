/**
 * Payments page — recent Stripe/crypto payment transactions + pending crypto
 * approval queue (Phase 6.10 W2.T7 — extracted from page.tsx).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-6 + 7-10 follow the same
 * convention).
 *
 * StatusBadge + EmptyState lifted in W2.T11 to shared `@/components/`.
 * Wave 3 / Task 17: inline `<table>` swapped for `<DataTable>` (responsive
 * card list below 768px). Pending-crypto approve/reject buttons get
 * `btn-action` (T1.6 — 44px tap target on mobile).
 */

'use client';

import { useState, useEffect } from 'react';
import { AlertCircle, Check, X, CreditCard } from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';

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

    const columns: DataTableColumn<any>[] = [
        {
            key: 'user',
            header: 'User',
            isCardTitle: true,
            render: (p) => <span className="text-white/80">{p.userEmail || p.email || '-'}</span>,
        },
        {
            key: 'provider',
            header: 'Provider',
            render: (p) => <span className="text-white/50">{p.provider}</span>,
        },
        {
            key: 'amount',
            header: 'Amount',
            render: (p) => <span className="font-semibold text-accent">${(p.amount ?? 0).toFixed(2)}</span>,
        },
        {
            key: 'status',
            header: 'Status',
            render: (p) => <StatusBadge status={p.status || 'pending'} />,
        },
        {
            key: 'date',
            header: 'Date',
            render: (p) => <span className="text-white/40">{new Date(p.createdAt || p.date).toLocaleDateString()}</span>,
        },
    ];

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
                                        className="btn-ghost btn-action text-aura-green border-aura-green/20 flex items-center gap-1"><Check className="w-3 h-3" />Approve</button>
                                    <button onClick={async () => { await api.rejectCryptoPayment(p.id); setPending(pr => pr.filter(x => x.id !== p.id)); }}
                                        className="btn-danger btn-action flex items-center gap-1"><X className="w-3 h-3" />Reject</button>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            <div className="glass-card p-5">
                <DataTable<any>
                    columns={columns}
                    rows={payments}
                    rowKey={(p) => p.id ?? `${p.userEmail || p.email}-${p.createdAt || p.date}`}
                    emptyState={<EmptyState icon={CreditCard} title="No payments recorded" />}
                />
            </div>
        </div>
    );
}
