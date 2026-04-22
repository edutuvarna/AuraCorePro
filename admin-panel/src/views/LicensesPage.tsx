/**
 * Licenses page — list/search/revoke/activate license keys & device activations
 * (Phase 6.10 W2.T6 — extracted from page.tsx).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-5 + 7-10 follow the same
 * convention).
 *
 * KPICard / SearchBar / EmptyState / Pagination / StatusBadge / TierBadge are
 * temporarily duplicated from page.tsx (Strategy B). Task 11 lifts them into
 * shared `@/components/` and call sites flip to the import. DataTable
 * conversion is Wave 3 Task 16 — keep the inline `<table>` 1:1 with the
 * monolith for now.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Key, CheckCircle2, XCircle, RefreshCw, Search,
    ArrowUpRight, ArrowDownRight, ChevronLeft, ChevronRight
} from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';

// Temporarily inlined — Task 11 will lift to @/components/KPICard
function KPICard({ label, value, icon: Icon, color = 'text-accent', trend, sub, span = 1 }: {
    label: string; value: string | number; icon: any; color?: string; trend?: number; sub?: string; span?: number;
}) {
    return (
        <div className={`glass-card-hover p-5 ${span === 2 ? 'col-span-2' : ''}`}>
            <div className="flex items-start justify-between mb-3">
                <span className="text-[11px] font-semibold text-white/35 uppercase tracking-wider">{label}</span>
                <div className={`w-9 h-9 rounded-xl ${color.includes('accent') ? 'bg-accent/10' : color.includes('green') ? 'bg-aura-green/10' : color.includes('purple') ? 'bg-aura-purple/10' : color.includes('amber') ? 'bg-aura-amber/10' : color.includes('blue') ? 'bg-aura-blue/10' : color.includes('red') ? 'bg-aura-red/10' : 'bg-white/5'} flex items-center justify-center`}>
                    <Icon className={`w-[18px] h-[18px] ${color}`} />
                </div>
            </div>
            <div className="stat-value">{value}</div>
            <div className="flex items-center gap-2 mt-2">
                {trend !== undefined && (
                    <span className={`flex items-center gap-0.5 text-xs font-medium ${trend >= 0 ? 'text-aura-green' : 'text-aura-red'}`}>
                        {trend >= 0 ? <ArrowUpRight className="w-3 h-3" /> : <ArrowDownRight className="w-3 h-3" />}
                        {Math.abs(trend)}%
                    </span>
                )}
                {sub && <span className="text-xs text-white/30">{sub}</span>}
            </div>
        </div>
    );
}

// Temporarily inlined — Task 11 will lift to @/components/SearchBar
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

// Temporarily inlined — Task 11 will lift to @/components/Pagination
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

// Temporarily inlined — Task 11 will lift to @/components/TierBadge (or fold into StatusBadge)
function TierBadge({ tier }: { tier: string }) {
    return <StatusBadge status={tier} />;
}

export function LicensesPage() {
    const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
    const [search, setSearch] = useState('');
    const [page, setPage] = useState(1);

    const load = useCallback(async () => {
        const d = await api.getLicenses(page, search || undefined);
        setData(d);
    }, [page, search]);

    useEffect(() => { load(); }, [load]);

    return (
        <div className="animate-fade-in">
            <PageHeader title="Licenses" subtitle="Manage license keys and device activations">
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>

            <div className="grid grid-cols-3 gap-4 mb-5">
                <KPICard label="Total Licenses" value={data.total || 0} icon={Key} color="text-accent" />
                <KPICard label="Active" value={data.items?.filter?.((l: any) => l.status === 'active')?.length ?? 0} icon={CheckCircle2} color="text-aura-green" />
                <KPICard label="Revoked" value={data.items?.filter?.((l: any) => l.status === 'revoked')?.length ?? 0} icon={XCircle} color="text-aura-red" />
            </div>

            <div className="glass-card p-5">
                <div className="mb-5 max-w-sm">
                    <SearchBar value={search} onChange={setSearch} placeholder="Search by key, email, tier..." onSubmit={load} />
                </div>
                <table className="w-full text-sm">
                    <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                        <th className="text-left py-3 px-4 font-medium">Key</th>
                        <th className="text-left py-3 px-4 font-medium">User</th>
                        <th className="text-left py-3 px-4 font-medium">Tier</th>
                        <th className="text-left py-3 px-4 font-medium">Devices</th>
                        <th className="text-left py-3 px-4 font-medium">Status</th>
                        <th className="text-left py-3 px-4 font-medium">Created</th>
                        <th className="text-right py-3 px-4 font-medium">Actions</th>
                    </tr></thead>
                    <tbody>
                        {(data.items || []).map((l: any) => (
                            <tr key={l.id} className="table-row">
                                <td className="py-3 px-4 font-mono text-xs text-white/50">{l.key?.substring(0, 12)}...</td>
                                <td className="py-3 px-4 text-white/70">{l.userEmail || '-'}</td>
                                <td className="py-3 px-4"><TierBadge tier={l.tier} /></td>
                                <td className="py-3 px-4 text-white/50">{l.activeDevices ?? 0}/{l.maxDevices ?? 1}</td>
                                <td className="py-3 px-4"><StatusBadge status={l.status} /></td>
                                <td className="py-3 px-4 text-white/40">{new Date(l.createdAt).toLocaleDateString()}</td>
                                <td className="py-3 px-4 text-right">
                                    {l.status === 'active' ? (
                                        <button onClick={async () => { await api.revokeLicense(l.id); load(); }} className="btn-danger text-xs px-3 py-1">Revoke</button>
                                    ) : (
                                        <button onClick={async () => { await api.activateLicense(l.id); load(); }} className="btn-ghost text-xs px-3 py-1 text-aura-green border-aura-green/20">Activate</button>
                                    )}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {(data.items || []).length === 0 && <EmptyState icon={Key} title="No licenses found" />}
                <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
            </div>
        </div>
    );
}
