/**
 * Audit Log page — login attempts and security events with 4 KPI cards
 * (successful 24h / failed 24h / unique IPs / suspicious IPs), search +
 * success/failed filter, paginated table.
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-8 + 10 follow the same
 * convention).
 *
 * Wave 5 Task 23 will redesign the body: drop the api.ts login_attempts adapter
 * (the Phase 6.9 hotfix that transforms audit_log rows into a login_attempts-
 * shaped object) and render audit_log columns natively (actor / action / target
 * / ip / time). Wave 3 / Task 17 sweep is intentionally minimum here — only
 * the inline `<table>` swap to `<DataTable>` and class polish; the api adapter
 * + column shape stay 1:1 with the Phase 6.9 hotfix until Task 23.
 *
 * KPICard + EmptyState lifted in W2.T11 to shared `@/components/`. SearchBar
 * and Pagination remain inline — they're outside the plan's primitive lift
 * list.
 *
 * Phase 6.10 W2.T9 — extracted from page.tsx; W2.T11 — KPICard/EmptyState lifted.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Search, RefreshCw, FileText, Globe, AlertTriangle,
    CheckCircle2, XCircle, ChevronLeft, ChevronRight,
} from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { KPICard } from '@/components/KpiCard';
import { EmptyState } from '@/components/EmptyState';
import { DataTable, DataTableColumn } from '@/components/DataTable';

// Inline (not in plan's lift list — Wave 3 / Task 16 will absorb).
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

// Inline (not in plan's lift list — Wave 3 / Task 16 will absorb).
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

export function AuditLogPage() {
    const [data, setData] = useState<any>({ attempts: [], total: 0 });
    const [stats, setStats] = useState<any>(null);
    const [search, setSearch] = useState('');
    const [filter, setFilter] = useState<boolean | undefined>(undefined);
    const [page, setPage] = useState(1);

    const load = useCallback(async () => {
        const [d, s] = await Promise.all([api.getLoginAttempts(search || undefined, filter, page), api.getLoginAttemptStats()]);
        setData(d); setStats(s);
    }, [search, filter, page]);

    useEffect(() => { load(); }, [load]);

    const attempts: any[] = data.attempts || [];

    const columns: DataTableColumn<any>[] = [
        {
            key: 'email',
            header: 'Email',
            isCardTitle: true,
            render: (a) => <span className="text-white/80">{a.email}</span>,
        },
        {
            key: 'ip',
            header: 'IP Address',
            render: (a) => <span className="font-mono text-xs text-white/40">{a.ipAddress}</span>,
        },
        {
            key: 'status',
            header: 'Status',
            render: (a) => a.success
                ? <span className="badge badge-green">Success</span>
                : <span className="badge badge-red">Failed</span>,
        },
        {
            key: 'time',
            header: 'Time',
            render: (a) => <span className="text-white/40">{new Date(a.createdAt).toLocaleString()}</span>,
        },
    ];

    return (
        <div className="animate-fade-in">
            <PageHeader title="Audit Log" subtitle="Login attempts and security events">
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>

            {stats && (
                <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-5">
                    <KPICard label="Successful (24h)" value={stats.successful24h ?? 0} icon={CheckCircle2} color="text-aura-green" />
                    <KPICard label="Failed (24h)" value={stats.failed24h ?? 0} icon={XCircle} color="text-aura-red" />
                    <KPICard label="Unique IPs" value={stats.uniqueIps ?? 0} icon={Globe} color="text-accent" />
                    <KPICard label="Suspicious IPs" value={stats.suspiciousIps ?? 0} icon={AlertTriangle} color="text-aura-amber" />
                </div>
            )}

            <div className="glass-card p-5">
                <div className="flex items-center gap-4 mb-5 flex-wrap">
                    <div className="max-w-xs flex-1">
                        <SearchBar value={search} onChange={setSearch} placeholder="Search email or IP..." onSubmit={load} />
                    </div>
                    <div className="flex gap-2">
                        {[
                            { label: 'All', value: undefined },
                            { label: 'Success', value: true },
                            { label: 'Failed', value: false },
                        ].map(f => (
                            <button key={f.label} onClick={() => { setFilter(f.value); setPage(1); }}
                                className={`px-3 py-1.5 rounded-lg text-xs font-medium transition-all ${filter === f.value ? 'bg-accent/15 text-accent border border-accent/30' : 'btn-ghost'}`}>
                                {f.label}
                            </button>
                        ))}
                    </div>
                </div>
                <DataTable<any>
                    columns={columns}
                    rows={attempts}
                    rowKey={(a) => a.id ?? `${a.email}-${a.createdAt}-${a.ipAddress}`}
                    emptyState={<EmptyState icon={FileText} title="No login attempts" />}
                />
                <Pagination page={page} pages={Math.ceil((data.total || 0) / 50)} onChange={setPage} />
            </div>
        </div>
    );
}
