/**
 * Crash Reports page — list of desktop-app crashes (exception type +
 * version + date) with detail expand showing stack trace, plus 4 KPI
 * cards (total / today / this-week / unique types).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-7 + 9-10 follow the same
 * convention).
 *
 * KPICard + EmptyState lifted in W2.T11 to shared `@/components/`. SearchBar
 * and Pagination remain inline — they're outside the plan's primitive lift
 * list. Wave 3 / Task 17: inline `<table>` swapped for `<DataTable>`
 * (responsive card list below 768px). Per-row Eye/Trash buttons get
 * `btn-action` (T1.6 — 44px tap target on mobile).
 *
 * Phase 6.10 W2.T8 — extracted from page.tsx; W2.T11 — KPICard/EmptyState lifted.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Search, RefreshCw, Bug, AlertTriangle, TrendingUp, Layers,
    X, Eye, Trash2, ChevronLeft, ChevronRight,
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

export function CrashReportsPage() {
    const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
    const [stats, setCrashStats] = useState<any>(null);
    const [search, setSearch] = useState('');
    const [page, setPage] = useState(1);
    const [detail, setDetail] = useState<any>(null);

    const load = useCallback(async () => {
        const [d, s] = await Promise.all([api.getCrashReports(search || undefined, undefined, page), api.getCrashStats()]);
        setData(d); setCrashStats(s);
    }, [search, page]);

    useEffect(() => { load(); }, [load]);

    const items: any[] = data.items || [];

    const columns: DataTableColumn<any>[] = [
        {
            key: 'exception',
            header: 'Exception',
            isCardTitle: true,
            render: (c) => <span className="text-aura-red/80 font-mono text-xs">{c.exceptionType}</span>,
        },
        {
            key: 'version',
            header: 'Version',
            render: (c) => <span className="text-white/50">{c.appVersion}</span>,
        },
        {
            key: 'date',
            header: 'Date',
            render: (c) => <span className="text-white/40">{new Date(c.createdAt).toLocaleDateString()}</span>,
        },
        {
            key: 'actions',
            header: 'Actions',
            cellClassName: 'text-right',
            render: (c) => (
                <div className="flex items-center justify-end gap-2">
                    <button onClick={async () => { const d = await api.getCrashReport(c.id); if(d) setDetail(d); }}
                        className="btn-action p-1.5 rounded-lg hover:bg-accent/10 text-white/30 hover:text-accent transition-colors inline-flex items-center justify-center"><Eye className="w-4 h-4" /></button>
                    <button onClick={async () => { await api.deleteCrashReport(c.id); load(); }}
                        className="btn-action p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors inline-flex items-center justify-center"><Trash2 className="w-4 h-4" /></button>
                </div>
            ),
        },
    ];

    return (
        <div className="animate-fade-in">
            <PageHeader title="Crash Reports" subtitle="Application crash diagnostics">
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>

            <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-5">
                <KPICard label="Total Crashes" value={stats?.total ?? data.total ?? 0} icon={Bug} color="text-aura-red" />
                <KPICard label="Today" value={stats?.today ?? 0} icon={AlertTriangle} color="text-aura-amber" />
                <KPICard label="This Week" value={stats?.thisWeek ?? 0} icon={TrendingUp} color="text-accent" />
                <KPICard label="Unique Types" value={stats?.uniqueTypes ?? 0} icon={Layers} color="text-aura-purple" />
            </div>

            {detail && (
                <div className="glass-card p-5 mb-5 animate-slide-up">
                    <div className="flex items-center justify-between mb-4">
                        <h3 className="font-display font-semibold">Crash Detail</h3>
                        <button onClick={() => setDetail(null)} className="btn-ghost p-1.5"><X className="w-4 h-4" /></button>
                    </div>
                    <div className="grid grid-cols-2 gap-4 mb-4 text-sm">
                        <div><span className="text-white/40">Type:</span> <span className="ml-2 text-aura-red">{detail.exceptionType}</span></div>
                        <div><span className="text-white/40">Version:</span> <span className="ml-2">{detail.appVersion}</span></div>
                    </div>
                    <pre className="bg-surface-950 rounded-xl p-4 text-xs font-mono text-white/60 overflow-x-auto max-h-60">{detail.stackTrace}</pre>
                </div>
            )}

            <div className="glass-card p-5">
                <div className="mb-5 max-w-sm">
                    <SearchBar value={search} onChange={setSearch} placeholder="Search exception type..." onSubmit={load} />
                </div>
                <DataTable<any>
                    columns={columns}
                    rows={items}
                    rowKey={(c) => c.id}
                    emptyState={<EmptyState icon={Bug} title="No crash reports" subtitle="Great news - no crashes recorded!" />}
                />
                <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
            </div>
        </div>
    );
}
