/**
 * Dashboard page — KPI cards + revenue chart + live activity feed + recent payments + server status + conversion funnel
 * (Phase 6.10 W2.T4 — extracted from page.tsx; W2.T11 — primitives lifted to @/components/).
 *
 * Lives under src/views/ rather than src/pages/ because Next.js auto-detects
 * src/pages/ as the legacy Pages Router and rejects non-default-exported
 * components there. Sibling tab extractions (Tasks 5-10) should follow the
 * same convention.
 *
 * KPICard / StatusBadge / EmptyState now live in @/components/ (W2.T11).
 * SignalR live-activity wiring uses the useSignalR hook (Phase 6.10 W4.T21);
 * signalr.ts singleton owns the connection at login/logout (app/page.tsx).
 */

'use client';

import { useState, useEffect, useMemo } from 'react';
import {
    Users, Crown, Shield, DollarSign, TrendingUp, Clock,
    Circle, Radio, Activity, CreditCard, CheckCircle2, XCircle, ChevronRight
} from 'lucide-react';
import {
    AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer
} from 'recharts';
import { api } from '@/lib/api';
import { useActivityFeed } from '@/lib/activityFeed';
import { PageHeader } from '@/components/PageHeader';
import { KPICard } from '@/components/KpiCard';
import { StatusBadge } from '@/components/StatusBadge';
import { EmptyState } from '@/components/EmptyState';

export function DashboardPage() {
    const [stats, setStats] = useState<any>(null);
    const [payments, setPayments] = useState<any[]>([]);
    const [health, setHealth] = useState<any>(null);
    const [revenue, setRevenue] = useState<any[]>([]);
    // Phase 6.11 Bug #6: ActivityFeedProvider lives in AdminPanelInner so the
    // feed + SignalR subscriptions survive sidebar-tab navigations. Previously
    // this page owned the state and events got discarded on unmount.
    const { activities, signalrStatus } = useActivityFeed();

    useEffect(() => {
        const load = async () => {
            const [s, p, h, r] = await Promise.all([
                api.getStats(), api.getRecentPayments(5), api.getHealth(), api.getRevenueChart(30)
            ]);
            if (s) setStats(s);
            if (p) setPayments(p);
            if (h) setHealth(h);
            if (r) setRevenue(r);
        };
        load();
        const interval = setInterval(load, 60000);
        return () => clearInterval(interval);
    }, []);

    const chartData = useMemo(() => {
        return revenue.map((d: any) => ({
            date: new Date(d.date).toLocaleDateString('en', { month: 'short', day: 'numeric' }),
            revenue: d.total || d.revenue || 0
        }));
    }, [revenue]);

    return (
        <div className="animate-fade-in">
            <PageHeader title="Dashboard" subtitle="Overview of your AuraCore Pro platform">
                <div className="flex items-center gap-2">
                    <div className={`w-2 h-2 rounded-full ${signalrStatus === 'connected' ? 'bg-aura-green animate-pulse' : signalrStatus === 'connecting' ? 'bg-aura-amber animate-pulse' : 'bg-aura-red'}`} />
                    <span className="text-xs text-white/40">{signalrStatus === 'connected' ? 'Live' : signalrStatus === 'connecting' ? 'Connecting' : 'Offline'}</span>
                </div>
            </PageHeader>

            {/* KPI Row */}
            <div className="grid grid-cols-2 lg:grid-cols-4 xl:grid-cols-6 gap-4 mb-6">
                <KPICard label="Total Users" value={stats?.totalUsers ?? '-'} icon={Users} color="text-aura-blue" />
                <KPICard label="Pro Users" value={stats?.proUsers ?? '-'} icon={Crown} color="text-accent" />
                <KPICard label="Enterprise" value={stats?.enterpriseUsers ?? '-'} icon={Shield} color="text-aura-purple" />
                <KPICard label="Revenue" value={`$${(stats?.totalRevenue ?? 0).toFixed(2)}`} icon={DollarSign} color="text-aura-green" />
                <KPICard label="Monthly Rev" value={`$${(stats?.monthlyRevenue ?? 0).toFixed(2)}`} icon={TrendingUp} color="text-aura-amber" />
                <KPICard label="Pending Crypto" value={stats?.pendingCrypto ?? 0} icon={Clock} color="text-aura-purple" />
            </div>

            {/* Main Grid — Bento Style */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
                {/* Revenue Chart — spans 2 cols */}
                <div className="lg:col-span-2 glass-card p-5">
                    <div className="flex items-center justify-between mb-4">
                        <h3 className="font-display font-semibold text-sm">Revenue (Last 30 Days)</h3>
                        <span className="flex items-center gap-1.5 text-xs text-aura-green"><Circle className="w-2 h-2 fill-aura-green" />Daily revenue</span>
                    </div>
                    <div className="h-[240px]">
                        <ResponsiveContainer width="100%" height="100%">
                            <AreaChart data={chartData}>
                                <defs>
                                    <linearGradient id="revGrad" x1="0" y1="0" x2="0" y2="1">
                                        <stop offset="0%" stopColor="#06B6D4" stopOpacity={0.3} />
                                        <stop offset="95%" stopColor="#06B6D4" stopOpacity={0} />
                                    </linearGradient>
                                </defs>
                                <CartesianGrid strokeDasharray="3 3" stroke="rgba(255,255,255,0.04)" />
                                <XAxis dataKey="date" tick={{ fill: 'rgba(255,255,255,0.3)', fontSize: 11 }} tickLine={false} axisLine={false} />
                                <YAxis tick={{ fill: 'rgba(255,255,255,0.3)', fontSize: 11 }} tickLine={false} axisLine={false} tickFormatter={v => `$${v}`} width={50} />
                                <Tooltip contentStyle={{ background: '#141927', border: '1px solid rgba(255,255,255,0.1)', borderRadius: '12px', fontSize: '12px', color: '#fff' }} />
                                <Area type="monotone" dataKey="revenue" stroke="#06B6D4" strokeWidth={2} fill="url(#revGrad)" />
                            </AreaChart>
                        </ResponsiveContainer>
                    </div>
                </div>

                {/* Live Activity Feed */}
                <div className="glass-card p-5 flex flex-col max-h-[340px]">
                    <div className="flex items-center justify-between mb-4 shrink-0">
                        <h3 className="font-display font-semibold text-sm">Live Activity</h3>
                        <div className="flex items-center gap-1.5">
                            <Radio className="w-3 h-3 text-aura-green animate-pulse" />
                            <span className="text-[10px] text-aura-green uppercase tracking-wider font-semibold">Live</span>
                        </div>
                    </div>
                    <div className="flex-1 overflow-y-auto space-y-2 pr-1">
                        {activities.length === 0 ? (
                            <div className="flex flex-col items-center justify-center h-full text-white/20 text-sm">
                                <Activity className="w-8 h-8 mb-2 opacity-30" />
                                <span>Waiting for events...</span>
                            </div>
                        ) : activities.map(a => (
                            <div key={a.id} className="flex items-start gap-3 py-2 px-3 rounded-lg bg-white/[0.02] animate-slide-up">
                                <div className={`w-1.5 h-1.5 rounded-full mt-1.5 shrink-0 ${a.color.replace('text-', 'bg-')}`} />
                                <div className="min-w-0 flex-1">
                                    <p className="text-xs text-white/70 truncate">{a.message}</p>
                                    <p className="text-[10px] text-white/25 mt-0.5">{a.time.toLocaleTimeString()}</p>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            </div>

            {/* Bottom Row */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-4 mt-4">
                {/* Recent Payments */}
                <div className="lg:col-span-2 glass-card p-5">
                    <div className="flex items-center justify-between mb-4">
                        <h3 className="font-display font-semibold text-sm">Recent Payments</h3>
                        <span className="text-xs text-white/30">{payments.length} latest</span>
                    </div>
                    {payments.length > 0 ? (
                        <div className="space-y-2">
                            {payments.map((p: any, i: number) => (
                                <div key={i} className="flex items-center justify-between py-2.5 px-3 rounded-lg hover:bg-white/[0.02] transition-colors">
                                    <div className="flex items-center gap-3 min-w-0">
                                        <div className="w-8 h-8 rounded-lg bg-accent/10 flex items-center justify-center shrink-0">
                                            <DollarSign className="w-4 h-4 text-accent" />
                                        </div>
                                        <div className="min-w-0">
                                            <p className="text-sm text-white/80 truncate">{p.userEmail || p.email || 'Unknown'}</p>
                                            <p className="text-[11px] text-white/30">{p.provider} - {new Date(p.createdAt || p.date).toLocaleDateString()}</p>
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-3 shrink-0">
                                        <span className="text-sm font-semibold text-accent">${(p.amount ?? 0).toFixed(2)}</span>
                                        <StatusBadge status={p.status || 'pending'} />
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : <EmptyState icon={CreditCard} title="No payments yet" />}
                </div>

                {/* Server Status */}
                <div className="glass-card p-5">
                    <div className="flex items-center justify-between mb-4">
                        <h3 className="font-display font-semibold text-sm">Server Status</h3>
                        <div className={`w-2.5 h-2.5 rounded-full ${health?.status === 'healthy' ? 'bg-aura-green' : 'bg-aura-red'} animate-pulse`} />
                    </div>
                    <div className="space-y-4">
                        {[
                            { label: 'API Status', value: health?.status === 'healthy' ? 'Online' : 'Offline', ok: health?.status === 'healthy' },
                            { label: 'Database', value: health?.database === 'connected' ? 'Connected' : 'Error', ok: health?.database === 'connected' },
                            { label: 'Version', value: health?.version || '-', ok: true },
                            { label: 'Uptime', value: health?.uptime || '-', ok: true },
                            { label: 'Memory', value: health?.memoryMB ? `${health.memoryMB} MB` : '-', ok: true },
                        ].map(item => (
                            <div key={item.label} className="flex items-center justify-between">
                                <span className="text-sm text-white/40">{item.label}</span>
                                <div className="flex items-center gap-2">
                                    <span className="text-sm font-medium">{item.value}</span>
                                    {item.ok ? <CheckCircle2 className="w-3.5 h-3.5 text-aura-green" /> : <XCircle className="w-3.5 h-3.5 text-aura-red" />}
                                </div>
                            </div>
                        ))}
                    </div>
                    {health && <p className="text-[10px] text-white/20 mt-4">Last checked: {new Date().toLocaleString()}</p>}
                </div>
            </div>

            {/* Conversion Funnel */}
            {stats && (
                <div className="glass-card p-5 mt-4">
                    <h3 className="font-display font-semibold text-sm mb-4">Conversion Funnel</h3>
                    <div className="flex items-center gap-4">
                        {[
                            { label: 'Free', count: (stats.totalUsers ?? 0) - (stats.proUsers ?? 0) - (stats.enterpriseUsers ?? 0), color: 'bg-aura-blue' },
                            { label: 'Pro', count: stats.proUsers ?? 0, color: 'bg-accent' },
                            { label: 'Enterprise', count: stats.enterpriseUsers ?? 0, color: 'bg-aura-purple' },
                        ].map((tier, i) => (
                            <div key={tier.label} className="flex-1 flex items-center gap-3">
                                {i > 0 && <ChevronRight className="w-4 h-4 text-white/15 shrink-0 -ml-2" />}
                                <div className="flex-1">
                                    <div className="flex justify-between text-xs mb-1">
                                        <span className="text-white/50">{tier.label}</span>
                                        <span className="font-semibold">{tier.count}</span>
                                    </div>
                                    <div className="h-2 bg-white/[0.04] rounded-full overflow-hidden">
                                        <div className={`h-full ${tier.color} rounded-full transition-all duration-1000`}
                                            style={{ width: `${stats.totalUsers > 0 ? (tier.count / stats.totalUsers * 100) : 0}%` }} />
                                    </div>
                                </div>
                            </div>
                        ))}
                    </div>
                </div>
            )}
        </div>
    );
}
