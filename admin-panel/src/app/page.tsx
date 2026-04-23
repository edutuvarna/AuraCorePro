'use client';

import { useState, useEffect } from 'react';
import { LayoutDashboard, Users, CreditCard, Shield, Crown, Zap, ShieldCheck, Monitor, BarChart2, Settings2, Key, Bug, Layers, FileText } from 'lucide-react';
import { api, setToken } from '@/lib/api';
import { startConnection, stopConnection } from '@/lib/signalr';
import { LoginScreen } from '@/components/LoginScreen';
import { Sidebar, NavGroup } from '@/components/Sidebar';
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
import { SecurityPage } from '@/views/SecurityPage';

type Page = 'dashboard'|'users'|'payments'|'subscriptions'|'licenses'|'updates'|'devices'|'crashes'|'telemetry'|'audit'|'whitelist'|'config'|'security';

const NAV_GROUPS: NavGroup[] = [
  { title: 'Overview', items: [{ id: 'dashboard', icon: LayoutDashboard, label: 'Dashboard' }] },
  { title: 'Management', items: [
    { id: 'users', icon: Users, label: 'Users' }, { id: 'payments', icon: CreditCard, label: 'Payments' },
    { id: 'subscriptions', icon: Crown, label: 'Subscriptions' }, { id: 'licenses', icon: Key, label: 'Licenses' },
    { id: 'updates', icon: Zap, label: 'Updates' }, { id: 'devices', icon: Monitor, label: 'Devices' },
  ] },
  { title: 'Analytics', items: [
    { id: 'crashes', icon: Bug, label: 'Crash Reports' }, { id: 'telemetry', icon: BarChart2, label: 'Telemetry' },
    { id: 'audit', icon: FileText, label: 'Audit Log' },
  ] },
  { title: 'System', items: [
    { id: 'whitelist', icon: Shield, label: 'IP Whitelist' }, { id: 'config', icon: Settings2, label: 'Configuration' },
    { id: 'security', icon: ShieldCheck, label: 'Security' },
  ] },
];

const PAGES: Record<Page, () => JSX.Element> = {
  dashboard: DashboardPage, users: UsersPage, payments: PaymentsPage, subscriptions: SubscriptionsPage,
  licenses: LicensesPage, updates: UpdatesPage, devices: DevicesPage, crashes: CrashReportsPage,
  telemetry: TelemetryPage, audit: AuditLogPage, whitelist: IpWhitelistPage, config: ConfigurationPage,
  security: SecurityPage,
};

function AdminPanel({ onLogout: _onLogout }: { onLogout: () => void }) {
  const [page, setPage] = useState<Page>('dashboard');
  const ActivePage = PAGES[page] ?? DashboardPage;
  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar groups={NAV_GROUPS} activePage={page} onSelect={(p) => setPage(p as Page)} />
      <main className="flex-1 overflow-y-auto">
        <div className="max-w-[1400px] mx-auto p-6 lg:p-8"><ActivePage /></div>
      </main>
    </div>
  );
}

export default function Home() {
  const [authenticated, setAuthenticated] = useState(false);
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    const saved = typeof window !== 'undefined' ? localStorage.getItem('aura_token') : null;
    if (saved) {
      setToken(saved);
      api.getStats().then(data => {
        if (data) { setAuthenticated(true); startConnection(); }
        else { setToken(null); localStorage.removeItem('aura_token'); }
        setChecking(false);
      });
    } else { setChecking(false); }
  }, []);

  const handleLogout = () => {
    setToken(null); localStorage.removeItem('aura_token'); stopConnection(); setAuthenticated(false);
  };

  if (checking) return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="flex flex-col items-center gap-4">
        <div className="w-12 h-12 rounded-2xl bg-accent/10 border border-accent/20 flex items-center justify-center animate-pulse-glow">
          <Layers className="w-6 h-6 text-accent" />
        </div>
        <p className="text-sm text-white/30">Loading...</p>
      </div>
    </div>
  );
  if (!authenticated) return <LoginScreen onLogin={() => { setAuthenticated(true); startConnection(); }} />;
  return <AdminPanel onLogout={handleLogout} />;
}
