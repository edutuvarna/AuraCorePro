'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';
import {
  LayoutDashboard, Users, CreditCard, Shield, Settings, LogOut,
  Search, Trash2, KeyRound, Crown, Activity, Server,
  TrendingUp, UserCheck, DollarSign, Clock, AlertCircle, Check,
  RefreshCw, Eye, Ban, Zap, Globe, ChevronRight, ChevronLeft, X,
  Cpu, HardDrive, Wifi, WifiOff, ShieldCheck,
  Monitor, AlertTriangle, BarChart2, Settings2, Key, Bug, Plus,
  CheckCircle2, XCircle, ChevronDown, ArrowUpRight, ArrowDownRight,
  Layers, Lock, Unlock, FileText, Send, Radio, Circle
} from 'lucide-react';
import {
  AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip,
  ResponsiveContainer, BarChart, Bar
} from 'recharts';
import { api, setToken, getToken } from '@/lib/api';
import { startConnection, stopConnection, on, off } from '@/lib/signalr';
import { formatCurrency } from '@/lib/format';
import { ConfirmDialog } from '@/components/ConfirmDialog';
import { PaginationLabel } from '@/components/PaginationLabel';
import { useDebouncedValue } from '@/hooks/useDebouncedValue';

// ────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────
type Page = 'dashboard'|'users'|'payments'|'subscriptions'|'licenses'|'updates'|'devices'|'crashes'|'telemetry'|'audit'|'whitelist'|'config'|'security';

interface ActivityEvent {
  id: number; type: string; message: string; time: Date; color: string;
}

// ────────────────────────────────────────────────
// LOGIN
// ────────────────────────────────────────────────
function LoginScreen({ onLogin }: { onLogin: () => void }) {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [needs2fa, setNeeds2fa] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true); setError('');
    if (needs2fa && totpCode) {
      const API = process.env.NEXT_PUBLIC_API_URL || 'https://api.auracore.pro';
      const res = await fetch(`${API}/api/auth/login`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password, totpCode })
      });
      const result = await res.json();
      setLoading(false);
      if (res.ok && result.accessToken) {
        setToken(result.accessToken);
        localStorage.setItem('aura_token', result.accessToken);
        if (result.user?.role === 'admin') { onLogin(); return; }
        setError('Access denied. Admin role required.'); setToken(null); return;
      }
      setError(result.error || '2FA verification failed'); return;
    }
    const { ok, data } = await api.login(email, password);
    setLoading(false);
    if (data.requires2fa && !totpCode) { setNeeds2fa(true); return; }
    if (ok && data.user?.role === 'admin') { onLogin(); }
    else if (ok) { setError('Access denied. Admin role required.'); setToken(null); }
    else { setError(data.error || 'Authentication failed'); }
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4 relative overflow-hidden">
      {/* Ambient background */}
      <div className="absolute inset-0">
        <div className="absolute top-0 left-1/3 w-[600px] h-[600px] bg-accent/[0.07] rounded-full blur-[120px] animate-pulse" />
        <div className="absolute bottom-0 right-1/4 w-[500px] h-[500px] bg-aura-purple/[0.05] rounded-full blur-[100px]" />
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[800px] h-[800px] bg-accent/[0.03] rounded-full blur-[150px]" />
      </div>

      <div className="relative w-full max-w-md">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 rounded-2xl bg-accent/10 border border-accent/20 mb-4 accent-glow">
            <Shield className="w-8 h-8 text-accent" />
          </div>
          <h1 className="text-2xl font-display font-bold">AuraCore Pro</h1>
          <p className="text-white/40 text-sm mt-1">Administration Console</p>
        </div>

        {/* Login Card */}
        <form onSubmit={handleSubmit} className="glass-card p-8 space-y-5">
          <div>
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Email</label>
            <input type="email" value={email} onChange={e => setEmail(e.target.value)}
              className="input-dark w-full" placeholder="admin@auracore.pro" required />
          </div>
          <div>
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Password</label>
            <input type="password" value={password} onChange={e => setPassword(e.target.value)}
              className="input-dark w-full" placeholder="Enter password" required />
          </div>
          {needs2fa && (
            <div className="animate-fade-in">
              <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">2FA Code</label>
              <input type="text" value={totpCode} onChange={e => setTotpCode(e.target.value)}
                className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" maxLength={6} autoFocus />
            </div>
          )}
          {error && (
            <div className="flex items-center gap-2 text-aura-red text-sm bg-aura-red/10 border border-aura-red/20 rounded-xl px-4 py-3">
              <AlertCircle className="w-4 h-4 shrink-0" />{error}
            </div>
          )}
          <button type="submit" disabled={loading} className="btn-primary w-full flex items-center justify-center gap-2">
            {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Lock className="w-4 h-4" />}
            {loading ? 'Authenticating...' : needs2fa ? 'Verify 2FA' : 'Sign In'}
          </button>
        </form>
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────
// SIDEBAR
// ────────────────────────────────────────────────
const NAV_GROUPS = [
  { label: 'Overview', items: [
    { id: 'dashboard' as Page, icon: LayoutDashboard, label: 'Dashboard' },
  ]},
  { label: 'Management', items: [
    { id: 'users' as Page, icon: Users, label: 'Users' },
    { id: 'payments' as Page, icon: CreditCard, label: 'Payments' },
    { id: 'subscriptions' as Page, icon: Crown, label: 'Subscriptions' },
    { id: 'licenses' as Page, icon: Key, label: 'Licenses' },
    { id: 'updates' as Page, icon: Zap, label: 'Updates' },
    { id: 'devices' as Page, icon: Monitor, label: 'Devices' },
  ]},
  { label: 'Analytics', items: [
    { id: 'crashes' as Page, icon: Bug, label: 'Crash Reports' },
    { id: 'telemetry' as Page, icon: BarChart2, label: 'Telemetry' },
    { id: 'audit' as Page, icon: FileText, label: 'Audit Log' },
  ]},
  { label: 'System', items: [
    { id: 'whitelist' as Page, icon: Shield, label: 'IP Whitelist' },
    { id: 'config' as Page, icon: Settings2, label: 'Configuration' },
    { id: 'security' as Page, icon: ShieldCheck, label: 'Security' },
  ]},
];

function Sidebar({ page, setPage, onLogout }: { page: Page; setPage: (p: Page) => void; onLogout: () => void }) {
  const [collapsed, setCollapsed] = useState(false);

  return (
    <aside className={`${collapsed ? 'w-[72px]' : 'w-[260px]'} h-screen bg-surface-950/80 backdrop-blur-xl border-r border-white/[0.04] flex flex-col transition-all duration-300 shrink-0 relative`}>
      {/* Logo */}
      <div className="h-16 flex items-center gap-3 px-5 border-b border-white/[0.04] shrink-0">
        <div className="w-9 h-9 rounded-xl bg-gradient-to-br from-accent to-aura-blue flex items-center justify-center shrink-0">
          <Layers className="w-5 h-5 text-white" />
        </div>
        {!collapsed && (
          <div className="animate-fade-in">
            <div className="font-display font-bold text-sm leading-none">AuraCore</div>
            <div className="text-[10px] text-white/30 uppercase tracking-widest mt-0.5">Admin Panel</div>
          </div>
        )}
      </div>

      {/* Navigation */}
      <nav className="flex-1 overflow-y-auto py-4 px-3 space-y-5">
        {NAV_GROUPS.map(group => (
          <div key={group.label}>
            {!collapsed && (
              <div className="text-[10px] font-semibold text-white/20 uppercase tracking-widest px-3 mb-2">{group.label}</div>
            )}
            <div className="space-y-0.5">
              {group.items.map(item => {
                const Icon = item.icon;
                const active = page === item.id;
                return (
                  <button key={item.id} onClick={() => setPage(item.id)}
                    className={`sidebar-item w-full ${active ? 'active' : ''} ${collapsed ? 'justify-center px-0' : ''}`}
                    title={collapsed ? item.label : undefined}>
                    <Icon className={`w-[18px] h-[18px] shrink-0 ${active ? 'text-accent' : ''}`} />
                    {!collapsed && <span>{item.label}</span>}
                    {active && !collapsed && <div className="ml-auto w-1.5 h-1.5 rounded-full bg-accent" />}
                  </button>
                );
              })}
            </div>
          </div>
        ))}
      </nav>

      {/* Footer */}
      <div className="border-t border-white/[0.04] p-3 shrink-0">
        <button onClick={onLogout}
          className={`sidebar-item w-full text-aura-red/60 hover:text-aura-red hover:bg-aura-red/[0.06] ${collapsed ? 'justify-center px-0' : ''}`}>
          <LogOut className="w-[18px] h-[18px]" />
          {!collapsed && <span>Sign Out</span>}
        </button>
      </div>

      {/* Collapse Toggle */}
      <button onClick={() => setCollapsed(!collapsed)}
        className="absolute -right-3 top-20 w-6 h-6 rounded-full bg-surface-800 border border-white/10 flex items-center justify-center text-white/40 hover:text-white hover:border-white/20 transition-all z-10">
        {collapsed ? <ChevronRight className="w-3 h-3" /> : <ChevronLeft className="w-3 h-3" />}
      </button>
    </aside>
  );
}

// ────────────────────────────────────────────────
// REUSABLE COMPONENTS
// ────────────────────────────────────────────────
function PageHeader({ title, subtitle, children }: { title: string; subtitle?: string; children?: React.ReactNode }) {
  return (
    <div className="flex items-start justify-between mb-8">
      <div>
        <h1 className="text-2xl font-display font-bold tracking-tight">{title}</h1>
        {subtitle && <p className="text-white/40 text-sm mt-1">{subtitle}</p>}
      </div>
      {children && <div className="flex items-center gap-3">{children}</div>}
    </div>
  );
}

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

function StatusBadge({ status }: { status: string }) {
  const s = (status || '').toLowerCase();
  const cls = s === 'active' || s === 'completed' || s === 'online' ? 'badge-green'
    : s === 'pending' || s === 'awaiting_payment' ? 'badge-amber'
    : s === 'confirming' ? 'badge-cyan'
    : s === 'disputed' ? 'badge-purple'
    : s === 'cancelled' || s === 'revoked' || s === 'failed' || s === 'refunded' || s === 'rejected' ? 'badge-red'
    : s === 'pro' ? 'badge-cyan'
    : s === 'enterprise' ? 'badge-purple'
    : s === 'admin' ? 'badge-red'
    : s === 'free' ? 'badge-blue'
    : 'badge-blue';
  return <span className={`badge ${cls}`}>{status}</span>;
}

function TierBadge({ tier }: { tier: string }) {
  return <StatusBadge status={tier} />;
}

// ────────────────────────────────────────────────
// DASHBOARD — Bento Grid
// ────────────────────────────────────────────────
function DashboardPage() {
  const [stats, setStats] = useState<any>(null);
  const [payments, setPayments] = useState<any[]>([]);
  const [health, setHealth] = useState<any>(null);
  const [revenue, setRevenue] = useState<any[]>([]);
  const [activities, setActivities] = useState<ActivityEvent[]>([]);
  const [activityId, setActivityId] = useState(0);
  const [signalrStatus, setSignalrStatus] = useState<'connected'|'connecting'|'disconnected'>('connecting');

  const addActivity = useCallback((type: string, msg: string, color: string) => {
    setActivityId(prev => {
      const id = prev + 1;
      setActivities(a => [{ id, type, message: msg, time: new Date(), color }, ...a].slice(0, 50));
      return id;
    });
  }, []);

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

    // SignalR
    startConnection();
    setSignalrStatus('connected');

    const onReg = (d: any) => addActivity('register', `New user: ${d.email}`, 'text-aura-green');
    const onLogin = (d: any) => addActivity('login', `${d.success ? 'Login' : 'Failed login'}: ${d.email}`, d.success ? 'text-aura-blue' : 'text-aura-amber');
    const onPayment = (d: any) => addActivity('payment', `Payment ${formatCurrency(d.amount, d.currency)} from ${d.email}`, 'text-accent');
    const onCrash = (d: any) => addActivity('crash', `Crash: ${d.type} (v${d.version})`, 'text-aura-red');
    const onTelemetry = (d: any) => addActivity('telemetry', `Telemetry batch: ${d.count} events`, 'text-aura-purple');

    const onAdminCount = () => {};
    on('UserRegistered', onReg);
    on('UserLogin', onLogin);
    on('Payment', onPayment);
    on('CrashReport', onCrash);
    on('Telemetry', onTelemetry);
    on('AdminCount', onAdminCount);  // suppress warning

    return () => {
      clearInterval(interval);
      off('UserRegistered', onReg); off('UserLogin', onLogin);
      off('Payment', onPayment); off('CrashReport', onCrash); off('Telemetry', onTelemetry);
      off('AdminCount', onAdminCount);
    };
  }, [addActivity]);

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
        <KPICard label="Revenue" value={formatCurrency(stats?.totalRevenue ?? 0, 'USD')} icon={DollarSign} color="text-aura-green" />
        <KPICard label="Monthly Rev" value={formatCurrency(stats?.monthlyRevenue ?? 0, 'USD')} icon={TrendingUp} color="text-aura-amber" />
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
                <YAxis tick={{ fill: 'rgba(255,255,255,0.3)', fontSize: 11 }} tickLine={false} axisLine={false} tickFormatter={v => formatCurrency(v, 'USD')} width={50} />
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
                    <span className="text-sm font-semibold text-accent">{formatCurrency(p.amount ?? 0, p.currency)}</span>
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

// ────────────────────────────────────────────────
// USERS PAGE
// ────────────────────────────────────────────────
function UsersPage() {
  const [users, setUsers] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 400);
  const [page, setPage] = useState(1);
  const [confirmRevoke, setConfirmRevoke] = useState<{ id: string; email: string } | null>(null);
  const [confirmDelete, setConfirmDelete] = useState<{ id: string; email: string } | null>(null);

  const load = useCallback(async () => {
    const data = await api.getUsers(debouncedSearch || undefined, page, 25);
    setUsers(data.users || []); setTotal(data.total || 0);
  }, [debouncedSearch, page]);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="animate-fade-in">
      <PageHeader title="Users" subtitle={`${total} registered users`}>
        <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
      </PageHeader>
      <div className="glass-card p-5">
        <div className="mb-5 max-w-sm">
          <SearchBar value={search} onChange={setSearch} placeholder="Search by email..." onSubmit={load} />
        </div>
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
              <th className="text-left py-3 px-4 font-medium">User</th>
              <th className="text-left py-3 px-4 font-medium">Role</th>
              <th className="text-left py-3 px-4 font-medium">Tier</th>
              <th className="text-left py-3 px-4 font-medium">Joined</th>
              <th className="text-right py-3 px-4 font-medium">Actions</th>
            </tr></thead>
            <tbody>
              {users.map((u: any) => (
                <tr key={u.id} className="table-row">
                  <td className="py-3 px-4">
                    <div className="flex items-center gap-3">
                      <div className="w-8 h-8 rounded-lg bg-gradient-to-br from-accent/20 to-aura-purple/20 flex items-center justify-center text-xs font-bold text-white/70">
                        {(u.email || '?')[0].toUpperCase()}
                      </div>
                      <span className="text-white/80">{u.email}</span>
                    </div>
                  </td>
                  <td className="py-3 px-4 text-white/50">{u.role}</td>
                  <td className="py-3 px-4"><TierBadge tier={u.tier || 'free'} /></td>
                  <td className="py-3 px-4 text-white/40">{new Date(u.createdAt).toLocaleDateString()}</td>
                  <td className="py-3 px-4">
                    <div className="flex items-center justify-end gap-2">
                      {u.role !== 'admin' && u.tier !== 'free' && (
                        <button onClick={() => setConfirmRevoke({ id: u.id, email: u.email })}
                          className="p-1.5 rounded-lg hover:bg-aura-amber/10 text-white/30 hover:text-aura-amber transition-colors" title="Revoke">
                          <Ban className="w-4 h-4" />
                        </button>
                      )}
                      {u.role !== 'admin' && (
                        <button onClick={() => setConfirmDelete({ id: u.id, email: u.email })}
                          className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors" title="Delete">
                          <Trash2 className="w-4 h-4" />
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {users.length === 0 && <EmptyState icon={Users} title="No users found" />}
        </div>
        <div className="flex items-center justify-between mt-4">
          <PaginationLabel page={page} pageSize={25} total={total} />
          <Pagination page={page} pages={Math.ceil(total / 25)} onChange={setPage} />
        </div>
      </div>
      <ConfirmDialog
        open={confirmRevoke !== null}
        title="Revoke subscription?"
        message={`This revokes ${confirmRevoke?.email}'s subscription and reverts to free tier.`}
        confirmLabel="Revoke"
        destructive
        onConfirm={async () => { await api.revokeSubscription(confirmRevoke!.id); setConfirmRevoke(null); load(); }}
        onCancel={() => setConfirmRevoke(null)}
      />
      <ConfirmDialog
        open={confirmDelete !== null}
        title="Delete user?"
        message={`This permanently deletes ${confirmDelete?.email}, their licenses, devices, payments, and subscriptions. Cannot be undone.`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => { await api.deleteUser(confirmDelete!.id); setConfirmDelete(null); load(); }}
        onCancel={() => setConfirmDelete(null)}
      />
    </div>
  );
}

// ────────────────────────────────────────────────
// PAYMENTS PAGE
// ────────────────────────────────────────────────
function PaymentsPage() {
  const [payments, setPayments] = useState<any[]>([]);
  const [pending, setPending] = useState<any[]>([]);
  const [confirmReject, setConfirmReject] = useState<{ id: string; userEmail: string; amount: number; crypto: string } | null>(null);

  useEffect(() => {
    const load = async () => {
      const [p, c] = await Promise.all([api.getRecentPayments(50), api.getPendingCrypto()]);
      setPayments(p || []); setPending(c || []);
    };
    load();
  }, []);

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
                  <button onClick={() => setConfirmReject({ id: p.id, userEmail: p.userEmail, amount: p.amount, crypto: p.crypto })}
                    className="btn-danger btn-action flex items-center gap-1"><X className="w-3 h-3" />Reject</button>
                </div>
              </div>
            ))}
          </div>
        </div>
      )}

      <div className="glass-card p-5">
        <table className="w-full text-sm">
          <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
            <th className="text-left py-3 px-4 font-medium">User</th>
            <th className="text-left py-3 px-4 font-medium">Provider</th>
            <th className="text-left py-3 px-4 font-medium">Amount</th>
            <th className="text-left py-3 px-4 font-medium">Status</th>
            <th className="text-left py-3 px-4 font-medium">Date</th>
          </tr></thead>
          <tbody>
            {payments.map((p: any, i: number) => (
              <tr key={i} className="table-row">
                <td className="py-3 px-4 text-white/80">{p.userEmail || p.email || '-'}</td>
                <td className="py-3 px-4 text-white/50">{p.provider}</td>
                <td className="py-3 px-4 font-semibold text-accent">{formatCurrency(p.amount ?? 0, p.currency)}</td>
                <td className="py-3 px-4"><StatusBadge status={p.status || 'pending'} /></td>
                <td className="py-3 px-4 text-white/40">{new Date(p.createdAt || p.date).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {payments.length === 0 && <EmptyState icon={CreditCard} title="No payments recorded" />}
      </div>
      <ConfirmDialog
        open={confirmReject !== null}
        title="Reject crypto payment?"
        message={`Mark this crypto payment as rejected. Customer will not receive the subscription.`}
        confirmLabel="Reject"
        destructive
        onConfirm={async () => { await api.rejectCryptoPayment(confirmReject!.id); setPending(pr => pr.filter(x => x.id !== confirmReject!.id)); setConfirmReject(null); }}
        onCancel={() => setConfirmReject(null)}
      />
    </div>
  );
}

// ────────────────────────────────────────────────
// SUBSCRIPTIONS PAGE
// ────────────────────────────────────────────────
function SubscriptionsPage() {
  const [userId, setUserId] = useState('');
  const [tier, setTier] = useState('pro');
  const [days, setDays] = useState('30');
  const [msg, setMsg] = useState('');

  const handleGrant = async () => {
    if (!userId) { setMsg('Enter a user ID'); return; }
    const { ok } = await api.grantSubscription(userId, tier, parseInt(days));
    setMsg(ok ? 'Subscription granted!' : 'Failed to grant subscription');
  };

  return (
    <div className="animate-fade-in">
      <PageHeader title="Subscriptions" subtitle="Grant or revoke user subscriptions" />
      <div className="glass-card p-6 max-w-xl">
        <h3 className="font-display font-semibold mb-5">Grant Subscription</h3>
        <div className="space-y-4">
          <div>
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">User ID</label>
            <input value={userId} onChange={e => setUserId(e.target.value)} className="input-dark w-full" placeholder="User GUID" />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Tier</label>
              <select value={tier} onChange={e => setTier(e.target.value)} className="input-dark w-full">
                <option value="pro">Pro</option>
                <option value="enterprise">Enterprise</option>
              </select>
            </div>
            <div>
              <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Days</label>
              <input type="number" value={days} onChange={e => setDays(e.target.value)} className="input-dark w-full" />
            </div>
          </div>
          <button onClick={handleGrant} className="btn-primary flex items-center gap-2"><Crown className="w-4 h-4" />Grant Access</button>
          {msg && <p className={`text-sm ${msg.includes('!') ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</p>}
        </div>
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────
// LICENSES PAGE
// ────────────────────────────────────────────────
function LicensesPage() {
  const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 400);
  const [page, setPage] = useState(1);
  const [confirmRevokeLicense, setConfirmRevokeLicense] = useState<{ id: string; keyPrefix: string } | null>(null);

  const load = useCallback(async () => {
    const d = await api.getLicenses(page, debouncedSearch || undefined);
    setData(d);
  }, [page, debouncedSearch]);

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
                    <button onClick={() => setConfirmRevokeLicense({ id: l.id, keyPrefix: l.key?.substring(0, 12) ?? l.id })} className="btn-danger btn-action text-xs px-3">Revoke</button>
                  ) : (
                    <button onClick={async () => { await api.activateLicense(l.id); load(); }} className="btn-ghost btn-action text-xs px-3 text-aura-green border-aura-green/20">Activate</button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {(data.items || []).length === 0 && <EmptyState icon={Key} title="No licenses found" />}
        <div className="flex items-center justify-between mt-4">
          <PaginationLabel page={data.page || 1} pageSize={25} total={data.total || 0} />
          <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
        </div>
      </div>
      <ConfirmDialog
        open={confirmRevokeLicense !== null}
        title="Revoke license?"
        message={`This revokes license ${confirmRevokeLicense?.keyPrefix}... (tier becomes free, status revoked).`}
        confirmLabel="Revoke"
        destructive
        onConfirm={async () => { await api.revokeLicense(confirmRevokeLicense!.id); setConfirmRevokeLicense(null); load(); }}
        onCancel={() => setConfirmRevokeLicense(null)}
      />
    </div>
  );
}

// ────────────────────────────────────────────────
// UPDATES PAGE
// ────────────────────────────────────────────────
function UpdatesPage() {
  const [updates, setUpdates] = useState<any[]>([]);
  const [form, setForm] = useState({ version: '', downloadUrl: '', releaseNotes: '', channel: 'stable', isMandatory: false, platforms: { Windows: true, Linux: false, macOS: false } });
  const [showForm, setShowForm] = useState(false);
  const [msg, setMsg] = useState('');
  const [confirmDeleteUpdate, setConfirmDeleteUpdate] = useState<{ id: string; version: string; platform?: string } | null>(null);

  useEffect(() => { api.getUpdates().then(setUpdates); }, []);

  const publish = async () => {
    if (!form.platforms.Windows && !form.platforms.Linux && !form.platforms.macOS) {
      setMsg('Select at least one platform before publishing');
      return;
    }
    const { ok, data } = await api.publishUpdate(form);
    if (ok) { setMsg('Update published!'); setShowForm(false); api.getUpdates().then(setUpdates); setForm({ version: '', downloadUrl: '', releaseNotes: '', channel: 'stable', isMandatory: false, platforms: { Windows: true, Linux: false, macOS: false } }); }
    else setMsg(data?.error || 'Failed');
  };

  return (
    <div className="animate-fade-in">
      <PageHeader title="Updates" subtitle="Manage app update releases">
        <button onClick={() => setShowForm(!showForm)} className="btn-primary flex items-center gap-2"><Plus className="w-4 h-4" />Publish Update</button>
      </PageHeader>

      {showForm && (
        <div className="glass-card p-6 mb-5 animate-slide-up max-w-2xl">
          <h3 className="font-display font-semibold mb-4">New Update</h3>
          <div className="grid grid-cols-2 gap-4 mb-4">
            <div>
              <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Version</label>
              <input value={form.version} onChange={e => setForm({ ...form, version: e.target.value })} className="input-dark w-full" placeholder="1.6.0" />
            </div>
            <div>
              <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Channel</label>
              <select value={form.channel} onChange={e => setForm({ ...form, channel: e.target.value })} className="input-dark w-full">
                <option value="stable">Stable</option><option value="beta">Beta</option>
              </select>
            </div>
          </div>
          <div className="mb-4">
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Download URL</label>
            <input value={form.downloadUrl} onChange={e => setForm({ ...form, downloadUrl: e.target.value })} className="input-dark w-full" placeholder="https://..." />
          </div>
          <div className="mb-4">
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Release Notes</label>
            <textarea value={form.releaseNotes} onChange={e => setForm({ ...form, releaseNotes: e.target.value })} className="input-dark w-full h-24 resize-none" />
          </div>
          <div className="mb-4">
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Platforms</label>
            <div className="flex items-center gap-5">
              {(['Windows', 'Linux', 'macOS'] as const).map(p => (
                <label key={p} className="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={form.platforms[p]} onChange={e => setForm({ ...form, platforms: { ...form.platforms, [p]: e.target.checked } })} className="rounded" />
                  <span className="text-sm text-white/60">{p}</span>
                </label>
              ))}
            </div>
          </div>
          <div className="flex items-center justify-between">
            <label className="flex items-center gap-2 cursor-pointer">
              <input type="checkbox" checked={form.isMandatory} onChange={e => setForm({ ...form, isMandatory: e.target.checked })} className="rounded" />
              <span className="text-sm text-white/60">Mandatory update</span>
            </label>
            <div className="flex gap-3">
              <button onClick={() => setShowForm(false)} className="btn-ghost">Cancel</button>
              <button onClick={publish} className="btn-primary flex items-center gap-2"><Send className="w-4 h-4" />Publish</button>
            </div>
          </div>
          {msg && <p className={`text-sm mt-3 ${msg.includes('!') ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</p>}
        </div>
      )}

      <div className="glass-card p-5">
        <table className="w-full text-sm">
          <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
            <th className="text-left py-3 px-4 font-medium">Version</th>
            <th className="text-left py-3 px-4 font-medium">Channel</th>
            <th className="text-left py-3 px-4 font-medium">Mandatory</th>
            <th className="text-left py-3 px-4 font-medium">Published</th>
            <th className="text-right py-3 px-4 font-medium">Actions</th>
          </tr></thead>
          <tbody>
            {updates.map((u: any) => (
              <tr key={u.id} className="table-row">
                <td className="py-3 px-4 font-mono font-semibold text-accent">{u.version}</td>
                <td className="py-3 px-4"><StatusBadge status={u.channel || 'stable'} /></td>
                <td className="py-3 px-4">{u.isMandatory ? <span className="text-aura-amber">Yes</span> : <span className="text-white/30">No</span>}</td>
                <td className="py-3 px-4 text-white/40">{new Date(u.createdAt).toLocaleDateString()}</td>
                <td className="py-3 px-4 text-right">
                  <button onClick={() => setConfirmDeleteUpdate({ id: u.id, version: u.version, platform: u.platform })}
                    className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors"><Trash2 className="w-4 h-4" /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {updates.length === 0 && <EmptyState icon={Zap} title="No updates published" subtitle="Click 'Publish Update' to create one" />}
      </div>
      <ConfirmDialog
        open={confirmDeleteUpdate !== null}
        title={`Delete update ${confirmDeleteUpdate?.version ?? ''}${confirmDeleteUpdate?.platform ? ` (${confirmDeleteUpdate.platform})` : ''}?`}
        message={`This removes the update record. Existing desktop clients won't be affected, but the release disappears from /api/updates/latest.`}
        confirmLabel="Delete"
        destructive
        onConfirm={async () => { await api.deleteUpdate(confirmDeleteUpdate!.id); setConfirmDeleteUpdate(null); api.getUpdates().then(setUpdates); }}
        onCancel={() => setConfirmDeleteUpdate(null)}
      />
    </div>
  );
}

// ────────────────────────────────────────────────
// DEVICES PAGE
// ────────────────────────────────────────────────
function DevicesPage() {
  const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
  const [stats, setStats] = useState<any>(null);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 400);
  const [page, setPage] = useState(1);

  const load = useCallback(async () => {
    const [d, s] = await Promise.all([api.getDevices(debouncedSearch || undefined, page), api.getDeviceStats()]);
    setData(d); setStats(s);
  }, [debouncedSearch, page]);

  useEffect(() => { load(); }, [load]);

  return (
    <div className="animate-fade-in">
      <PageHeader title="Devices" subtitle="Registered hardware across all licenses">
        <button onClick={load} className="btn-ghost flex items-center gap-2"><RefreshCw className="w-4 h-4" />Refresh</button>
      </PageHeader>

      <div className="grid grid-cols-2 lg:grid-cols-4 gap-4 mb-5">
        <KPICard label="Total Devices" value={stats?.totalDevices ?? data.total ?? 0} icon={Monitor} color="text-accent" />
        <KPICard label="Active Today" value={stats?.activeToday ?? 0} icon={Activity} color="text-aura-green" />
        <KPICard label="Active This Week" value={stats?.activeThisWeek ?? 0} icon={TrendingUp} color="text-aura-amber" />
        <KPICard label="New This Week" value={stats?.newThisWeek ?? 0} icon={Plus} color="text-aura-purple" />
      </div>

      <div className="glass-card p-5">
        <div className="mb-5 max-w-sm">
          <SearchBar value={search} onChange={setSearch} placeholder="Search machine name or OS..." onSubmit={load} />
        </div>
        <table className="w-full text-sm">
          <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
            <th className="text-left py-3 px-4 font-medium">Machine</th>
            <th className="text-left py-3 px-4 font-medium">OS</th>
            <th className="text-left py-3 px-4 font-medium">Crashes</th>
            <th className="text-left py-3 px-4 font-medium">Telemetry</th>
            <th className="text-left py-3 px-4 font-medium">Last Seen</th>
          </tr></thead>
          <tbody>
            {(data.items || []).map((d: any) => (
              <tr key={d.id} className="table-row">
                <td className="py-3 px-4"><div className="flex items-center gap-2"><Monitor className="w-4 h-4 text-white/30" /><span className="text-white/80">{d.machineName}</span></div></td>
                <td className="py-3 px-4 text-white/50 text-xs">{d.osVersion}</td>
                <td className="py-3 px-4 text-white/50">{d.crashCount ?? 0}</td>
                <td className="py-3 px-4 text-white/50">{d.telemetryCount ?? 0}</td>
                <td className="py-3 px-4 text-white/40">{d.lastSeenAt ? new Date(d.lastSeenAt).toLocaleDateString() : '-'}</td>
              </tr>
            ))}
          </tbody>
        </table>
        {(data.items || []).length === 0 && <EmptyState icon={Monitor} title="No devices registered yet" subtitle="Devices will appear after users login from the desktop app" />}
        <div className="flex items-center justify-between mt-4">
          <PaginationLabel page={data.page || 1} pageSize={25} total={data.total || 0} />
          <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
        </div>
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────
// CRASH REPORTS PAGE
// ────────────────────────────────────────────────
function CrashReportsPage() {
  const [data, setData] = useState<any>({ items: [], total: 0, page: 1, pages: 0 });
  const [stats, setCrashStats] = useState<any>(null);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 400);
  const [page, setPage] = useState(1);
  const [detail, setDetail] = useState<any>(null);
  const [confirmDeleteCrash, setConfirmDeleteCrash] = useState<{ id: string } | null>(null);

  const load = useCallback(async () => {
    const [d, s] = await Promise.all([api.getCrashReports(debouncedSearch || undefined, undefined, page), api.getCrashStats()]);
    setData(d); setCrashStats(s);
  }, [debouncedSearch, page]);

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
                  <button onClick={() => setConfirmDeleteCrash({ id: c.id })}
                    className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors"><Trash2 className="w-4 h-4" /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {(data.items || []).length === 0 && <EmptyState icon={Bug} title="No crash reports" subtitle="Great news - no crashes recorded!" />}
        <div className="flex items-center justify-between mt-4">
          <PaginationLabel page={data.page || 1} pageSize={25} total={data.total || 0} />
          <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
        </div>
      </div>
      <ConfirmDialog
        open={confirmDeleteCrash !== null}
        title="Delete crash report?"
        message="Remove this crash report permanently."
        confirmLabel="Delete"
        destructive
        onConfirm={async () => { await api.deleteCrashReport(confirmDeleteCrash!.id); setConfirmDeleteCrash(null); load(); }}
        onCancel={() => setConfirmDeleteCrash(null)}
      />
    </div>
  );
}

// ────────────────────────────────────────────────
// TELEMETRY PAGE
// ────────────────────────────────────────────────
function TelemetryPage() {
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
        <div className="flex items-center justify-between mt-4">
          <PaginationLabel page={data.page || 1} pageSize={25} total={data.total || 0} />
          <Pagination page={data.page || 1} pages={data.pages || 0} onChange={setPage} />
        </div>
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────
// AUDIT LOG PAGE
// ────────────────────────────────────────────────
function AuditLogPage() {
  const [data, setData] = useState<any>({ attempts: [], total: 0 });
  const [stats, setStats] = useState<any>(null);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebouncedValue(search, 400);
  const [filter, setFilter] = useState<boolean | undefined>(undefined);
  const [page, setPage] = useState(1);

  const load = useCallback(async () => {
    const [d, s] = await Promise.all([api.getLoginAttempts(debouncedSearch || undefined, filter, page), api.getLoginAttemptStats()]);
    setData(d); setStats(s);
  }, [debouncedSearch, filter, page]);

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
        <div className="flex items-center justify-between mt-4">
          <PaginationLabel page={page} pageSize={50} total={data.total || 0} />
          <Pagination page={page} pages={Math.ceil((data.total || 0) / 50)} onChange={setPage} />
        </div>
      </div>
    </div>
  );
}

// ────────────────────────────────────────────────
// IP WHITELIST PAGE
// ────────────────────────────────────────────────
function WhitelistPage() {
  const [ips, setIps] = useState<any[]>([]);
  const [newIp, setNewIp] = useState('');
  const [newLabel, setNewLabel] = useState('');
  const [msg, setMsg] = useState('');
  const [myIp, setMyIp] = useState('');
  const [confirmDeleteIp, setConfirmDeleteIp] = useState<{ ip: string; label?: string } | null>(null);

  const load = async () => {
    const [w, ip] = await Promise.all([api.getWhitelist(), api.getMyIp()]);
    setIps(w || []); if (ip?.ip) setMyIp(ip.ip);
  };

  useEffect(() => { load(); }, []);

  const addIp = async () => {
    if (!newIp) return;
    const { ok, data } = await api.addWhitelistIp(newIp, newLabel || undefined);
    if (ok) { setNewIp(''); setNewLabel(''); setMsg(''); load(); }
    else setMsg(data?.error || 'Failed');
  };

  return (
    <div className="animate-fade-in">
      <PageHeader title="IP Whitelist" subtitle="Manage admin access by IP address">
        <button onClick={async () => {
          if (myIp) {
            const { ok } = await api.addWhitelistIp(myIp, 'Auto-added');
            if (ok) load();
          }
        }} className="btn-primary flex items-center gap-2"><Globe className="w-4 h-4" />Whitelist My IP{myIp ? ` (${myIp})` : ''}</button>
      </PageHeader>

      {/* Add Form */}
      <div className="glass-card p-5 mb-5 max-w-xl">
        <h3 className="font-display font-semibold text-sm mb-4">Add IP Address</h3>
        <div className="flex items-end gap-3">
          <div className="flex-1">
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">IP Address</label>
            <input value={newIp} onChange={e => setNewIp(e.target.value)} className="input-dark w-full" placeholder="1.2.3.4" />
          </div>
          <div className="flex-1">
            <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Label (optional)</label>
            <input value={newLabel} onChange={e => setNewLabel(e.target.value)} className="input-dark w-full" placeholder="Office" />
          </div>
          <button onClick={addIp} className="btn-primary shrink-0"><Plus className="w-4 h-4" /></button>
        </div>
        {msg && <p className="text-sm text-aura-red mt-2">{msg}</p>}
      </div>

      {/* IP List */}
      <div className="glass-card p-5">
        <table className="w-full text-sm">
          <thead><tr className="text-[11px] text-white/30 uppercase tracking-wider border-b border-white/[0.06]">
            <th className="text-left py-3 px-4 font-medium">IP Address</th>
            <th className="text-left py-3 px-4 font-medium">Label</th>
            <th className="text-left py-3 px-4 font-medium">Added</th>
            <th className="text-right py-3 px-4 font-medium">Actions</th>
          </tr></thead>
          <tbody>
            {ips.map((ip: any, i: number) => (
              <tr key={i} className="table-row">
                <td className="py-3 px-4 font-mono text-accent">{ip.ip}</td>
                <td className="py-3 px-4 text-white/50">{ip.label || '-'}</td>
                <td className="py-3 px-4 text-white/40">{ip.addedAt ? new Date(ip.addedAt).toLocaleDateString() : '-'}</td>
                <td className="py-3 px-4 text-right">
                  <button onClick={() => setConfirmDeleteIp({ ip: ip.ip, label: ip.label })}
                    className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors"><Trash2 className="w-4 h-4" /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {ips.length === 0 && <EmptyState icon={Shield} title="No IPs whitelisted" subtitle="Add IPs to restrict admin access" />}
      </div>
      <ConfirmDialog
        open={confirmDeleteIp !== null}
        title="Remove IP from whitelist?"
        message={`Remove ${confirmDeleteIp?.ip}${confirmDeleteIp?.label ? ` (${confirmDeleteIp.label})` : ''} from the whitelist. This may lock out admin access from that address.`}
        confirmLabel="Remove"
        destructive
        onConfirm={async () => { await api.removeWhitelistIp(confirmDeleteIp!.ip); setConfirmDeleteIp(null); load(); }}
        onCancel={() => setConfirmDeleteIp(null)}
      />
    </div>
  );
}

// ────────────────────────────────────────────────
// CONFIG PAGE
// ────────────────────────────────────────────────
function ConfigPage() {
  const [config, setConfig] = useState<any>(null);
  const [saving, setSaving] = useState(false);
  const [msg, setMsg] = useState('');
  const [confirmMaintenance, setConfirmMaintenance] = useState(false);

  useEffect(() => { api.getConfig().then(setConfig); }, []);

  const applyToggle = async (key: string) => {
    if (!config) return;
    const newVal = !config[key];
    setSaving(true);
    const updated = await api.updateConfig({ [key]: newVal });
    setSaving(false);
    if (updated) { setConfig(updated); setMsg(''); }
    else setMsg('Failed to update');
  };

  const toggleFlag = (key: string) => {
    // Maintenance mode ON requires explicit confirmation — platform-outage footgun guard.
    if (key === 'isMaintenanceMode' && config && !config[key]) {
      setConfirmMaintenance(true);
      return;
    }
    void applyToggle(key);
  };

  const saveMessage = async () => {
    if (!config) return;
    setSaving(true);
    const updated = await api.updateConfig({ maintenanceMessage: config.maintenanceMessage });
    setSaving(false);
    if (updated) { setConfig(updated); setMsg('Saved!'); setTimeout(() => setMsg(''), 2000); }
    else setMsg('Failed');
  };

  const flags = [
    { key: 'isMaintenanceMode', label: 'Maintenance Mode', desc: 'Block all user logins and API access (admin panel stays accessible)', icon: AlertTriangle, danger: true },
    { key: 'newRegistrations', label: 'New Registrations', desc: 'Allow new users to register accounts', icon: UserCheck },
    { key: 'telemetryEnabled', label: 'Telemetry Collection', desc: 'Collect usage analytics from desktop app', icon: BarChart2 },
    { key: 'crashReportsEnabled', label: 'Crash Reports', desc: 'Accept crash report submissions from clients', icon: Bug },
    { key: 'autoUpdateEnabled', label: 'Auto-Update Delivery', desc: 'Deliver update notifications to desktop app', icon: Zap },
  ];

  if (!config) return <div className="flex items-center justify-center h-64"><RefreshCw className="w-6 h-6 text-white/20 animate-spin" /></div>;

  return (
    <div className="animate-fade-in">
      <PageHeader title="Configuration" subtitle="Feature flags and system settings">
        <span className="text-xs text-white/30">Last updated: {config.lastUpdated ? new Date(config.lastUpdated).toLocaleString() : '-'}</span>
      </PageHeader>

      <div className="glass-card p-6 max-w-2xl">
        <h3 className="text-[11px] font-semibold text-white/25 uppercase tracking-widest mb-5">Feature Flags</h3>
        <div className="space-y-1">
          {flags.map(flag => (
            <div key={flag.key} className="flex items-center justify-between py-4 px-4 -mx-4 rounded-xl hover:bg-white/[0.02] transition-colors">
              <div className="flex items-start gap-3">
                <flag.icon className={`w-5 h-5 mt-0.5 ${flag.danger ? 'text-aura-amber' : 'text-white/30'}`} />
                <div>
                  <p className="font-medium text-sm">{flag.label}</p>
                  <p className="text-xs text-white/35 mt-0.5">{flag.desc}</p>
                </div>
              </div>
              <button onClick={() => toggleFlag(flag.key)} disabled={saving}
                className={`relative w-12 h-6 rounded-full transition-all duration-300 ${config[flag.key] ? (flag.danger ? 'bg-aura-amber' : 'bg-accent') : 'bg-white/10'}`}>
                <div className={`absolute top-1 w-4 h-4 bg-white rounded-full shadow transition-all duration-300 ${config[flag.key] ? 'left-7' : 'left-1'}`} />
              </button>
            </div>
          ))}
        </div>

        <div className="mt-6 pt-6 border-t border-white/[0.06]">
          <h3 className="text-[11px] font-semibold text-white/25 uppercase tracking-widest mb-3">Maintenance Message</h3>
          <textarea value={config.maintenanceMessage || ''} onChange={e => setConfig({ ...config, maintenanceMessage: e.target.value })}
            className="input-dark w-full h-20 resize-none mb-3" placeholder="AuraCore Pro is currently under maintenance..." />
          <div className="flex items-center gap-3">
            <button onClick={saveMessage} disabled={saving} className="btn-primary flex items-center gap-2">
              {saving ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}Save Message
            </button>
            {msg && <span className={`text-sm ${msg === 'Saved!' ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</span>}
          </div>
        </div>
      </div>
      <ConfirmDialog
        open={confirmMaintenance}
        title="Enable maintenance mode?"
        message="This blocks ALL user logins and API access platform-wide until disabled. Admin panel remains accessible."
        confirmLabel="Enable Maintenance"
        destructive
        onConfirm={async () => { setConfirmMaintenance(false); await applyToggle('isMaintenanceMode'); }}
        onCancel={() => setConfirmMaintenance(false)}
      />
    </div>
  );
}

// ════════════════════════════════════════════════

// ????????????????????????????????????????????????
// SECURITY PAGE (2FA)
// ????????????????????????????????????????????????
function SecurityPage() {
  const [status, setStatus] = useState<any>(null);
  const [setupData, setSetupData] = useState<any>(null);
  const [code, setCode] = useState('');
  const [msg, setMsg] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => { api.get2faStatus().then(setStatus); }, []);

  const handleSetup = async () => {
    setLoading(true); setMsg('');
    const data = await api.setup2fa();
    setLoading(false);
    if (data?.error) { setMsg(data.error); return; }
    setSetupData(data);
  };

  const handleVerify = async () => {
    if (code.length !== 6) { setMsg('Enter 6-digit code'); return; }
    setLoading(true); setMsg('');
    const { ok, data } = await api.verify2fa(code);
    setLoading(false);
    if (ok) { setMsg('2FA enabled!'); setStatus({ enabled: true }); setSetupData(null); setCode(''); }
    else setMsg(data?.error || 'Verification failed');
  };

  const handleDisable = async () => {
    if (code.length !== 6) { setMsg('Enter current 2FA code to disable'); return; }
    setLoading(true); setMsg('');
    const { ok, data } = await api.disable2fa(code);
    setLoading(false);
    if (ok) { setMsg('2FA disabled'); setStatus({ enabled: false }); setCode(''); }
    else setMsg(data?.error || 'Failed to disable');
  };

  return (
    <div className="animate-fade-in">
      <PageHeader title="Security" subtitle="Two-factor authentication and account security" />
      <div className="glass-card p-6 max-w-xl">
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-3">
            <div className={`w-10 h-10 rounded-xl flex items-center justify-center ${status?.enabled ? 'bg-aura-green/10' : 'bg-aura-amber/10'}`}>
              <ShieldCheck className={`w-5 h-5 ${status?.enabled ? 'text-aura-green' : 'text-aura-amber'}`} />
            </div>
            <div>
              <p className="font-medium">Two-Factor Authentication</p>
              <p className="text-xs text-white/40">TOTP via Google Authenticator or similar</p>
            </div>
          </div>
          <StatusBadge status={status?.enabled ? 'Active' : 'Disabled'} />
        </div>
        {!status?.enabled && !setupData && (
          <div className="bg-white/[0.02] rounded-xl p-4 mb-4">
            <p className="text-sm text-white/60 mb-3">Enable 2FA to add an extra layer of security. You will need an authenticator app like Google Authenticator or Authy.</p>
            <button onClick={handleSetup} disabled={loading} className="btn-primary flex items-center gap-2">
              {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Lock className="w-4 h-4" />}Enable 2FA
            </button>
          </div>
        )}
        {setupData && (
          <div className="space-y-4 animate-slide-up">
            <div className="bg-white/[0.02] rounded-xl p-4 text-center">
              <p className="text-sm text-white/60 mb-3">Scan this QR code with your authenticator app:</p>
              {setupData.qrCodeDataUrl && <img src={setupData.qrCodeDataUrl} alt="QR" className="mx-auto w-48 h-48 rounded-xl bg-white p-2" />}
              {setupData.manualEntryKey && (
                <div className="mt-3">
                  <p className="text-[10px] text-white/30 uppercase tracking-wider mb-1">Manual entry key</p>
                  <code className="text-xs font-mono text-accent bg-accent/10 px-3 py-1.5 rounded-lg select-all">{setupData.manualEntryKey}</code>
                </div>
              )}
            </div>
            <div>
              <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Verification Code</label>
              <input type="text" value={code} onChange={e => setCode(e.target.value)} maxLength={6}
                className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" />
            </div>
            <button onClick={handleVerify} disabled={loading} className="btn-primary w-full flex items-center justify-center gap-2">
              {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Check className="w-4 h-4" />}Verify and Enable
            </button>
          </div>
        )}
        {status?.enabled && !setupData && (
          <div className="space-y-4">
            <div className="bg-aura-green/5 border border-aura-green/10 rounded-xl p-4">
              <p className="text-sm text-aura-green/80">2FA is active. Your account is protected with time-based one-time passwords.</p>
            </div>
            <div>
              <label className="block text-xs font-medium text-white/40 uppercase tracking-wider mb-2">Enter current 2FA code to disable</label>
              <input type="text" value={code} onChange={e => setCode(e.target.value)} maxLength={6}
                className="input-dark w-full text-center tracking-[0.5em] text-lg" placeholder="000000" />
            </div>
            <button onClick={handleDisable} disabled={loading} className="btn-danger w-full flex items-center justify-center gap-2">
              {loading ? <RefreshCw className="w-4 h-4 animate-spin" /> : <Unlock className="w-4 h-4" />}Disable 2FA
            </button>
          </div>
        )}
        {msg && <p className={`text-sm mt-4 ${msg.includes('enabled') || msg.includes('disabled') ? 'text-aura-green' : 'text-aura-red'}`}>{msg}</p>}
      </div>
    </div>
  );
}



// ADMIN PANEL — Layout + Page Routing
// ════════════════════════════════════════════════
function AdminPanel({ onLogout }: { onLogout: () => void }) {
  const [page, setPage] = useState<Page>('dashboard');

  const renderPage = () => {
    switch (page) {
      case 'dashboard': return <DashboardPage />;
      case 'users': return <UsersPage />;
      case 'payments': return <PaymentsPage />;
      case 'subscriptions': return <SubscriptionsPage />;
      case 'licenses': return <LicensesPage />;
      case 'updates': return <UpdatesPage />;
      case 'devices': return <DevicesPage />;
      case 'crashes': return <CrashReportsPage />;
      case 'telemetry': return <TelemetryPage />;
      case 'audit': return <AuditLogPage />;
      case 'whitelist': return <WhitelistPage />;
      case 'config': return <ConfigPage />;
      case 'security': return <SecurityPage />;
      default: return <DashboardPage />;
    }
  };

  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar page={page} setPage={setPage} onLogout={onLogout} />
      <main className="flex-1 overflow-y-auto">
        <div className="max-w-[1400px] mx-auto p-6 lg:p-8">
          {renderPage()}
        </div>
      </main>
    </div>
  );
}

// ════════════════════════════════════════════════
// HOME — Auth Wrapper
// ════════════════════════════════════════════════
export default function Home() {
  const [authenticated, setAuthenticated] = useState(false);
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    const saved = typeof window !== 'undefined' ? localStorage.getItem('aura_token') : null;
    if (saved) {
      setToken(saved);
      // Verify token by fetching stats
      api.getStats().then(data => {
        if (data) { setAuthenticated(true); startConnection(); }
        else { setToken(null); localStorage.removeItem('aura_token'); }
        setChecking(false);
      });
    } else {
      setChecking(false);
    }
  }, []);

  const handleLogout = () => {
    setToken(null);
    localStorage.removeItem('aura_token');
    stopConnection();
    setAuthenticated(false);
  };

  if (checking) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="flex flex-col items-center gap-4">
          <div className="w-12 h-12 rounded-2xl bg-accent/10 border border-accent/20 flex items-center justify-center animate-pulse-glow">
            <Layers className="w-6 h-6 text-accent" />
          </div>
          <p className="text-sm text-white/30">Loading...</p>
        </div>
      </div>
    );
  }

  if (!authenticated) {
    return <LoginScreen onLogin={() => { setAuthenticated(true); startConnection(); }} />;
  }

  return <AdminPanel onLogout={handleLogout} />;
}
