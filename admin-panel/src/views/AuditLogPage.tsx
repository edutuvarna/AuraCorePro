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
 * / ip / time). For Wave 2 Task 9 this is just a 1:1 lift of the current
 * implementation — body rewrite is explicitly out of scope.
 *
 * KPICard / SearchBar / EmptyState / Pagination are temporarily duplicated
 * from page.tsx (Strategy B). Task 11 lifts them into shared `@/components/`
 * and call sites flip to the import. DataTable conversion is Wave 3 Task 16
 * — keep the inline `<table>` 1:1 with the monolith for now.
 *
 * Phase 6.10 W2.T9 — extracted from page.tsx.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    Search, RefreshCw, FileText, Globe, AlertTriangle,
    CheckCircle2, XCircle, ChevronLeft, ChevronRight,
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
                <table className="w-full text-sm">
                    <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                        <th className="text-left py-3 px-4 font-medium">Email</th>
                        <th className="text-left py-3 px-4 font-medium">IP Address</th>
                        <th className="text-left py-3 px-4 font-medium">Status</th>
                        <th className="text-left py-3 px-4 font-medium">Time</th>
                    </tr></thead>
                    <tbody>
                        {(data.attempts || []).map((a: any, i: number) => (
                            <tr key={i} className="table-row">
                                <td className="py-3 px-4 text-white/80">{a.email}</td>
                                <td className="py-3 px-4 font-mono text-xs text-white/40">{a.ipAddress}</td>
                                <td className="py-3 px-4">
                                    {a.success ? <span className="badge badge-green">Success</span> : <span className="badge badge-red">Failed</span>}
                                </td>
                                <td className="py-3 px-4 text-white/40">{new Date(a.createdAt).toLocaleString()}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {(data.attempts || []).length === 0 && <EmptyState icon={FileText} title="No login attempts" />}
                <Pagination page={page} pages={Math.ceil((data.total || 0) / 50)} onChange={setPage} />
            </div>
        </div>
    );
}
