/**
 * Telemetry page — usage analytics from desktop app (event type / device /
 * session / date) with type-filter dropdown and 3 KPI cards (total events
 * / today / event-type count).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-7 + 9-10 follow the same
 * convention).
 *
 * KPICard / EmptyState / Pagination are temporarily duplicated from page.tsx
 * (Strategy B). Task 11 lifts them into shared `@/components/` and call sites
 * flip to the import. DataTable conversion is Wave 3 Task 16 — keep the inline
 * `<table>` 1:1 with the monolith for now.
 *
 * Phase 6.10 W2.T8 — extracted from page.tsx.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    RefreshCw, BarChart2, Activity, Layers,
    ChevronLeft, ChevronRight,
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

export function TelemetryPage() {
    const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
    const [stats, setStats] = useState<any>(null);
    const [eventType, setEventType] = useState('');
    const [types, setTypes] = useState<string[]>([]);
    const [page, setPage] = useState(1);

    useEffect(() => { api.getTelemetryEventTypes().then(setTypes); }, []);

    const load = useCallback(async () => {
        const [d, s] = await Promise.all([api.getTelemetry(eventType || undefined, page), api.getTelemetryStats()]);
        setData(d); setStats(s);
    }, [eventType, page]);

    useEffect(() => { load(); }, [load]);

    return (
        <div className="animate-fade-in">
            <PageHeader title="Telemetry" subtitle="Usage analytics from desktop app">
                <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
            </PageHeader>

            <div className="grid grid-cols-2 lg:grid-cols-3 gap-4 mb-5">
                <KPICard label="Total Events" value={stats?.totalEvents ?? data.total ?? 0} icon={BarChart2} color="text-accent" />
                <KPICard label="Today" value={stats?.today ?? 0} icon={Activity} color="text-aura-green" />
                <KPICard label="Event Types" value={types.length} icon={Layers} color="text-aura-purple" />
            </div>

            <div className="glass-card p-5">
                <div className="flex items-center gap-4 mb-5">
                    <select value={eventType} onChange={e => setEventType(e.target.value)} className="input-dark">
                        <option value="">All event types</option>
                        {types.map(t => <option key={t} value={t}>{t}</option>)}
                    </select>
                </div>
                <table className="w-full text-sm">
                    <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
                        <th className="text-left py-3 px-4 font-medium">Event Type</th>
                        <th className="text-left py-3 px-4 font-medium">Device</th>
                        <th className="text-left py-3 px-4 font-medium">Session</th>
                        <th className="text-left py-3 px-4 font-medium">Date</th>
                    </tr></thead>
                    <tbody>
                        {(data.items || []).map((t: any, i: number) => (
                            <tr key={i} className="table-row">
                                <td className="py-3 px-4"><span className="badge badge-cyan">{t.eventType}</span></td>
                                <td className="py-3 px-4 font-mono text-xs text-white/40">{t.deviceId?.substring(0, 8)}...</td>
                                <td className="py-3 px-4 font-mono text-xs text-white/40">{t.sessionId?.substring(0, 8) || '-'}</td>
                                <td className="py-3 px-4 text-white/40">{new Date(t.createdAt).toLocaleString()}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
                {(data.items || []).length === 0 && <EmptyState icon={BarChart2} title="No telemetry data" subtitle="Events will appear once devices start sending data" />}
                <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
            </div>
        </div>
    );
}
