'use client';

import { useState, useEffect } from 'react';
import {
  LayoutDashboard, Users, CreditCard, Shield,
  Crown, Check,
  RefreshCw, Zap,
  ShieldCheck,
  Monitor, BarChart2, Settings2, Key, Bug,
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
import { IpWhitelistPage } from '@/views/IpWhitelistPage';
import { ConfigurationPage } from '@/views/ConfigurationPage';

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
// REUSABLE COMPONENTS (still inline — only StatusBadge is consumed by the
// remaining SecurityPage. KPICard / SearchBar / EmptyState / Pagination /
// TierBadge moved out with the extracted views in Tasks 4-10. Task 11 will
// lift StatusBadge into shared `@/components/` and Task 13 finishes the trim.)
// ────────────────────────────────────────────────
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

// ????????????????????????????????????????????????
// SECURITY PAGE (2FA) — still inline pending Task 13 cleanup. Skipped from
// Task 10 extraction because the plan only calls for IpWhitelistPage +
// ConfigurationPage; the surplus inline page is acceptable per task spec
// ("leave them inline … Task 13 wraps up final cleanup").
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


// ════════════════════════════════════════════════
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
      case 'whitelist': return <IpWhitelistPage />;
      case 'config': return <ConfigurationPage />;
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
