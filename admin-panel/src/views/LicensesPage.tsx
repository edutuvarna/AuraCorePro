/**
 * Licenses page — list/search/revoke/activate license keys & device activations
 * (Phase 6.10 W2.T6 — extracted from page.tsx).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-5 + 7-10 follow the same
 * convention).
 *
 * KPICard + StatusBadge + EmptyState lifted in W2.T11 to shared
 * `@/components/`. SearchBar + Pagination + TierBadge remain inline — they're
 * outside the plan's primitive lift list (TierBadge is just a 1-line
 * StatusBadge wrapper; SearchBar/Pagination wait for Wave 5 visual sweep).
 *
 * Wave 3 / Task 16: inline `<table>` swapped for `<DataTable>` primitive
 * (responsive: table on desktop ≥768px, card list below). ConfirmDialog wired
 * for revoke (UX upgrade per Phase 6.9 CTP-4 — was a one-click destructive
 * action prior).
 *
 * Note on `License.deviceCount`: Phase 6.10 W5.T25 retired the Phase 6.8
 * `activeDevices` alias backend-side; rendering cell now reads `deviceCount`
 * directly.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Key, CheckCircle2, XCircle, RefreshCw, Search,
    ChevronLeft, ChevronRight,
} from 'lucide-react';
import { api } from '@/lib/api';
import { License } from '@/lib/types';
import { PageHeader } from '@/components/PageHeader';
import { KPICard } from '@/components/KpiCard';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';
import { ConfirmDialog } from '@/components/ConfirmDialog';

// Inline (not in plan's lift list — Wave 5 visual sweep will absorb).
function SearchBar({ value, onChange, placeholder = 'Search...', onSubmit }: {
    value: string; onChange: (v: string) => void; placeholder?: string; onSubmit?: () => void;
}) {
    return (
        <div className="relative">
            <Search className="absolute left-3.5 top-1/2 -translate-y-1/2 w-4 h-4 text-white/25" />
            <input type="text" value={value} onChange={e => onChange(e.target.value)} placeholder={placeholder}
                onKeyDown={e => e.key === 'Enter' && onSubmit?.()}
                className="input-dark w-full pl-10" />
        </div>
    );
}

// Inline (not in plan's lift list — Wave 5 visual sweep will absorb).
function Pagination({ page, pages, onChange }: { page: number; pages: number; onChange: (p: number) => void }) {
    if (pages <= 1) return null;
    return (
        <div className="flex items-center justify-center gap-2 mt-6">
            <button onClick={() => onChange(page - 1)} disabled={page <= 1} className="btn-ghost px-3 py-1.5 disabled:opacity-30">
                <ChevronLeft className="w-4 h-4" />
            </button>
            <span className="text-sm text-white/40 px-3">{page} / {pages}</span>
            <button onClick={() => onChange(page + 1)} disabled={page >= pages} className="btn-ghost px-3 py-1.5 disabled:opacity-30">
                <ChevronRight className="w-4 h-4" />
            </button>
        </div>
    );
}

// Inline (1-line wrapper around StatusBadge — kept for now per Task 11 spec).
function TierBadge({ tier }: { tier: string }) {
    return <StatusBadge status={tier} />;
}

export function LicensesPage() {
    const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
    const [search, setSearch] = useState('');
    const [page, setPage] = useState(1);
    const [confirmRevoke, setConfirmRevoke] = useState<{ id: string; key: string } | null>(null);

    const load = useCallback(async () => {
        const d = await api.getLicenses(page, search || undefined);
        setData(d);
    }, [page, search]);

    useEffect(() => { load(); }, [load]);

    const items: License[] = data.items || [];

    const columns: DataTableColumn<License>[] = [
        {
            key: 'key',
            header: 'Key',
            isCardTitle: true,
            render: (l) => <span className="font-mono text-xs text-white/50">{l.key?.substring(0, 12)}...</span>,
        },
        {
            key: 'user',
            header: 'User',
            render: (l) => <span className="text-white/70">{l.userEmail || '-'}</span>,
        },
        {
            key: 'tier',
            header: 'Tier',
            render: (l) => <TierBadge tier={l.tier} />,
        },
        {
            key: 'devices',
            header: 'Devices',
            render: (l) => (
                <span className="text-white/50">{l.deviceCount ?? 0}/{l.maxDevices ?? 1}</span>
            ),
        },
        {
            key: 'status',
            header: 'Status',
            render: (l) => <StatusBadge status={l.status} />,
        },
        {
            key: 'created',
            header: 'Created',
            render: (l) => <span className="text-white/40">{new Date(l.createdAt).toLocaleDateString()}</span>,
        },
        {
            key: 'actions',
            header: 'Actions',
            cellClassName: 'text-right',
            render: (l) => (
                <div className="flex justify-end">
                    {l.status === 'active' ? (
                        <button onClick={() => setConfirmRevoke({ id: l.id, key: l.key })}
                            className="btn-action btn-danger text-xs px-3 py-1">Revoke</button>
                    ) : (
                        <button onClick={async () => { await api.activateLicense(l.id); load(); }}
                            className="btn-action btn-ghost text-xs px-3 py-1 text-aura-green border-aura-green/20">Activate</button>
                    )}
                </div>
            ),
        },
    ];

    return (
        <div className="animate-fade-in">
            <PageHeader title="Licenses" subtitle="Manage license keys and device activations">
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>

            <div className="grid grid-cols-3 gap-4 mb-5">
                <KPICard label="Total Licenses" value={data.total || 0} icon={Key} color="text-accent" />
                <KPICard label="Active" value={items.filter((l) => l.status === 'active').length} icon={CheckCircle2} color="text-aura-green" />
                <KPICard label="Revoked" value={items.filter((l) => l.status === 'revoked').length} icon={XCircle} color="text-aura-red" />
            </div>

            <div className="glass-card p-5">
                <div className="mb-5 max-w-sm">
                    <SearchBar value={search} onChange={setSearch} placeholder="Search by key, email, tier..." onSubmit={load} />
                </div>
                <DataTable<License>
                    columns={columns}
                    rows={items}
                    rowKey={(l) => l.id}
                    emptyState={<EmptyState icon={Key} title="No licenses found" />}
                />
                <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
            </div>

            <ConfirmDialog
                open={confirmRevoke !== null}
                title="Revoke license"
                message={confirmRevoke ? `Revoke license ${confirmRevoke.key?.substring(0, 12)}...? Active devices will be disabled.` : ''}
                confirmLabel="Revoke"
                cancelLabel="Cancel"
                destructive
                onConfirm={async () => {
                    if (!confirmRevoke) return;
                    await api.revokeLicense(confirmRevoke.id);
                    setConfirmRevoke(null);
                    load();
                }}
                onCancel={() => setConfirmRevoke(null)}
            />
        </div>
    );
}
