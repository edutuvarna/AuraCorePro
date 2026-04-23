/**
 * Telemetry page — usage analytics from desktop app (event type / device /
 * session / date) with type-filter dropdown and 3 KPI cards (total events
 * / today / event-type count).
 *
 * Lives under src/views/ rather than src/pages/ to avoid Next.js auto-detecting
 * the legacy Pages Router (sibling extractions Tasks 4-7 + 9-10 follow the same
 * convention).
 *
 * KPICard + EmptyState lifted in W2.T11 to shared `@/components/`.
 * Pagination remains inline — outside the plan's primitive lift list and
 * Wave 3 Task 16 (DataTable conversion) will absorb it.
 *
 * Phase 6.10 W2.T8 — extracted from page.tsx; W2.T11 — KPICard/EmptyState lifted.
 */

'use client';

import { useState, useEffect, useCallback } from 'react';
import {
    RefreshCw, BarChart2, Activity, Layers,
    ChevronLeft, ChevronRight,
} from 'lucide-react';
import { api } from '@/lib/api';
import { PageHeader } from '@/components/PageHeader';
import { KPICard } from '@/components/KpiCard';
import { EmptyState } from '@/components/EmptyState';

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
