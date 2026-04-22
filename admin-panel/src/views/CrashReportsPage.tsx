/**
 * Crash Reports page — list of desktop-app crashes (exception type +
 * version + date) with detail expand showing stack trace, plus 4 KPI
 * cards (total / today / this-week / unique types).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-7 + 9-10 follow the same
 * convention).
 *
 * KPICard / SearchBar / EmptyState / Pagination are temporarily duplicated
 * from page.tsx (Strategy B). Task 11 lifts them into shared `@/components/`
 * and call sites flip to the import. DataTable conversion is Wave 3 Task 16
 * — keep the inline `<table>` 1:1 with the monolith for now.
 *
 * Phase 6.10 W2.T8 — extracted from page.tsx.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Search, RefreshCw, Bug, AlertTriangle, TrendingUp, Layers,
    X, Eye, Trash2, ChevronLeft, ChevronRight,
    ArrowUpRight, ArrowDownRight,
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
                <table className="w-full text-sm">
                    <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                        <th className="text-left py-3 px-4 font-medium">Exception</th>
                        <th className="text-left py-3 px-4 font-medium">Version</th>
                        <th className="text-left py-3 px-4 font-medium">Date</th>
                        <th className="text-right py-3 px-4 font-medium">Actions</th>
                    </tr></thead>
                    <tbody>
                        {(data.items || []).map((c: any) => (
                            <tr key={c.id} className="table-row">
                                <td className="py-3 px-4 text-aura-red/80 font-mono text-xs">{c.exceptionType}</td>
                                <td className="py-3 px-4 text-white/50">{c.appVersion}</td>
                                <td className="py-3 px-4 text-white/40">{new Date(c.createdAt).toLocaleDateString()}</td>
                                <td className="py-3 px-4 text-right flex justify-end gap-2">
                                    <button onClick={async () => { const d = await api.getCrashReport(c.id); if(d) setDetail(d); }}
                                        className="p-1.5 rounded-lg hover:bg-accent/10 text-white/30 hover:text-accent transition-colors"><Eye className="w-4 h-4" /></button>
                                    <button onClick={async () => { await api.deleteCrashReport(c.id); load(); }}
                                        className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors"><Trash2 className="w-4 h-4" /></button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {(data.items || []).length === 0 && <EmptyState icon={Bug} title="No crash reports" subtitle="Great news - no crashes recorded!" />}
                <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
            </div>
        </div>
    );
}
