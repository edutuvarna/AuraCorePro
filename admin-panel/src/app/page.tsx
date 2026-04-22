'use client';

import { useState, useEffect } from 'react';
import {
  LayoutDashboard, Users, CreditCard, Shield,
  Search, Trash2, Crown,
  UserCheck, Check,
  RefreshCw, Zap, Globe, ChevronRight, ChevronLeft,
  ShieldCheck,
  Monitor, AlertTriangle, BarChart2, Settings2, Key, Bug, Plus,
  ArrowUpRight, ArrowDownRight,
  Layers, Lock, Unlock, FileText
} from 'lucide-react';
import { api, setToken, getToken } from '@/lib/api';
import { startConnection, stopConnection } from '@/lib/signalr';
import { LoginScreen } from '@/components/LoginScreen';
import { Sidebar, NavGroup } from '@/components/Sidebar';
import { PageHeader } from '@/components/PageHeader';
import { DashboardPage } from '@/views/DashboardPage';
import { UsersPage } from '@/views/UsersPage';
import { SubscriptionsPage } from '@/views/SubscriptionsPage';
import { LicensesPage } from '@/views/LicensesPage';
import { PaymentsPage } from '@/views/PaymentsPage';
import { DevicesPage } from '@/views/DevicesPage';
import { UpdatesPage } from '@/views/UpdatesPage';
import { CrashReportsPage } from '@/views/CrashReportsPage';
import { TelemetryPage } from '@/views/TelemetryPage';
import { AuditLogPage } from '@/views/AuditLogPage';

// ────────────────────────────────────────────────
// Types
// ────────────────────────────────────────────────
type Page = 'dashboard'|'users'|'payments'|'subscriptions'|'licenses'|'updates'|'devices'|'crashes'|'telemetry'|'audit'|'whitelist'|'config'|'security';

// ────────────────────────────────────────────────
// SIDEBAR NAV CONFIG
// ────────────────────────────────────────────────
const NAV_GROUPS: NavGroup[] = [
  { title: 'Overview', items: [
    { id: 'dashboard', icon: LayoutDashboard, label: 'Dashboard' },
  ]},
  { title: 'Management', items: [
    { id: 'users', icon: Users, label: 'Users' },
    { id: 'payments', icon: CreditCard, label: 'Payments' },
    { id: 'subscriptions', icon: Crown, label: 'Subscriptions' },
    { id: 'licenses', icon: Key, label: 'Licenses' },
    { id: 'updates', icon: Zap, label: 'Updates' },
    { id: 'devices', icon: Monitor, label: 'Devices' },
  ]},
  { title: 'Analytics', items: [
    { id: 'crashes', icon: Bug, label: 'Crash Reports' },
    { id: 'telemetry', icon: BarChart2, label: 'Telemetry' },
    { id: 'audit', icon: FileText, label: 'Audit Log' },
  ]},
  { title: 'System', items: [
    { id: 'whitelist', icon: Shield, label: 'IP Whitelist' },
    { id: 'config', icon: Settings2, label: 'Configuration' },
    { id: 'security', icon: ShieldCheck, label: 'Security' },
  ]},
];

// ────────────────────────────────────────────────
// REUSABLE COMPONENTS
// ────────────────────────────────────────────────
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

function TierBadge({ tier }: { tier: string }) {
  return <StatusBadge status={tier} />;
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
      <PageHeader title="IP Whitelist" subtitle="Trusted IPs bypass the login rate limit (3 fails/30min per-IP, 5 fails/30min per-email)">
        <button onClick={async () => {
          if (myIp) {
            const { ok } = await api.addWhitelistIp(myIp, 'Auto-added');
            if (ok) load();
          }
        }} className="btn-primary flex items-center gap-2"><Globe className="w-4 h-4" />Whitelist My IP{myIp ? ` (${myIp})` : ''}</button>
      </PageHeader>

      {/* Explanation banner */}
      <div className="glass-card p-4 mb-5 text-xs text-white/60 leading-relaxed max-w-3xl">
        <strong className="text-white/80">How this works:</strong> whitelisted IPs{' '}
        <strong className="text-accent">skip the login rate limit</strong>. Admin access is NOT restricted — all IPs
        can still log in with valid credentials + 2FA. Use this for trusted operational IPs (e.g. office, office VPN) so
        a distributed brute-force attempt from the same network doesn&apos;t lock you out. Whitelisting your own IP is safe
        and recommended.
      </div>

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
                  <button onClick={async () => { await api.removeWhitelistIp(ip.ip); load(); }}
                    className="p-1.5 rounded-lg hover:bg-aura-red/10 text-white/30 hover:text-aura-red transition-colors"><Trash2 className="w-4 h-4" /></button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        {ips.length === 0 && <EmptyState icon={Shield} title="No trusted IPs yet" subtitle="Add trusted IPs (e.g. office, home, office VPN) to exempt them from login rate-limit lockouts" />}
      </div>
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

  useEffect(() => { api.getConfig().then(setConfig); }, []);

  const toggleFlag = async (key: string) => {
    if (!config) return;
    const newVal = !config[key];
    setSaving(true);
    const updated = await api.updateConfig({ [key]: newVal });
    setSaving(false);
    if (updated) { setConfig(updated); setMsg(''); }
    else setMsg('Failed to update');
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
      <Sidebar groups={NAV_GROUPS} activePage={page} onSelect={(p) => setPage(p as Page)} />
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
