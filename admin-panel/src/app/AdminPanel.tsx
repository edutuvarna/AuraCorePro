'use client';

/**
 * AdminPanel shell — role-aware NAV_GROUPS composition.
 *
 * Extracted out of `page.tsx` because Next.js App Router forbids non-standard
 * exports from a page file (only `default`, `metadata`, `generateStaticParams`,
 * etc. are allowed). Tests need direct access to the inner component with a
 * synthetic `role` prop, so we expose `AdminPanelForTest` here and let
 * `page.tsx` import `AdminPanelInner` for the authenticated render path.
 */

import { useState } from 'react';
import {
  LayoutDashboard, Users, CreditCard, Shield, Crown, Zap, ShieldCheck, Monitor,
  BarChart2, Settings2, Key, Bug, FileText,
  Inbox, UserCog, ArrowRightLeft, Lock, Gauge, Mail,
} from 'lucide-react';
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
import { PermissionRequestsPage } from '@/views/PermissionRequestsPage';
import { AdminActionLogPage } from '@/views/AdminActionLogPage';
import { AdminManagementPage } from '@/views/AdminManagementPage';
import { InvitationsPage } from '@/views/InvitationsPage';
import { RoleChangePage } from '@/views/RoleChangePage';
import { SecurityPolicyPage } from '@/views/SecurityPolicyPage';
import { APIRateLimitsPage } from '@/views/APIRateLimitsPage';
import { MyPermissionsPage } from '@/views/MyPermissionsPage';
import { ChangePasswordPage } from '@/views/ChangePasswordPage';
import { Enable2FAPage } from '@/views/Enable2FAPage';
import { RedeemInvitationPage } from '@/views/RedeemInvitationPage';

import type { UserRole } from '@/lib/types';
import { RoleContext } from '@/lib/roleContext';
import { Toaster } from 'react-hot-toast';
import { ActivityFeedProvider } from '@/lib/activityFeed';
import { PermissionNotificationsProvider } from '@/lib/permissionNotifications';
import { ScopeLimitedBanner } from '@/components/ScopeLimitedBanner';

export type Page =
  'dashboard'|'users'|'payments'|'subscriptions'|'licenses'|'updates'|'devices'|'crashes'|'telemetry'|'audit'|'whitelist'|'config'|'security'|
  // superadmin-only
  'permReq'|'adminActionLog'|'adminMgmt'|'invitations'|'roleChange'|'securityPolicy'|'rateLimits'|
  // cross-role
  'myPerms'|'changePw'|'enable2fa'|'redeemInvite';

export const ADMIN_NAV_GROUPS: NavGroup[] = [
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

export const SUPERADMIN_EXTRA_GROUPS: NavGroup[] = [
  { title: 'Superadmin', items: [
    { id: 'permReq', icon: Inbox, label: 'Permission Requests' },
    { id: 'adminActionLog', icon: FileText, label: 'Admin Action Log' },
    { id: 'adminMgmt', icon: UserCog, label: 'Admin Management' },
    { id: 'invitations', icon: Mail, label: 'Invitations' },
    { id: 'roleChange', icon: ArrowRightLeft, label: 'Role Change' },
    { id: 'securityPolicy', icon: Lock, label: 'Security Policy' },
    { id: 'rateLimits', icon: Gauge, label: 'API Rate Limits' },
  ] },
];

export const SETUP_2FA_GROUPS: NavGroup[] = [
  { title: 'Setup', items: [{ id: 'enable2fa', icon: ShieldCheck, label: 'Enable 2FA' }] },
];

export const CHANGE_PW_GROUPS: NavGroup[] = [
  { title: 'Setup', items: [{ id: 'changePw', icon: Key, label: 'Change Password' }] },
];

const PAGES: Record<Page, () => JSX.Element> = {
  dashboard: DashboardPage, users: UsersPage, payments: PaymentsPage, subscriptions: SubscriptionsPage,
  licenses: LicensesPage, updates: UpdatesPage, devices: DevicesPage, crashes: CrashReportsPage,
  telemetry: TelemetryPage, audit: AuditLogPage, whitelist: IpWhitelistPage, config: ConfigurationPage,
  security: SecurityPage,
  permReq: PermissionRequestsPage, adminActionLog: AdminActionLogPage, adminMgmt: AdminManagementPage,
  invitations: InvitationsPage, roleChange: RoleChangePage, securityPolicy: SecurityPolicyPage, rateLimits: APIRateLimitsPage,
  myPerms: MyPermissionsPage, changePw: ChangePasswordPage, enable2fa: Enable2FAPage, redeemInvite: RedeemInvitationPage,
};

interface AdminPanelProps {
  onLogout: () => void;
  role: UserRole;
  initialPage?: Page;
  currentUserEmail?: string;
  scope?: 'normal' | '2fa-setup-only' | 'change-password';
}

function decodeEmailFromJwt(): string | undefined {
  if (typeof window === 'undefined') return undefined;
  const token = localStorage.getItem('aura_token');
  if (!token) return undefined;
  try {
    const payload = JSON.parse(atob(token.split('.')[1]!));
    return payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress']
        ?? payload.email
        ?? undefined;
  } catch { return undefined; }
}

export function AdminPanelInner({ onLogout, role, initialPage, currentUserEmail, scope = 'normal' }: AdminPanelProps) {
  const [page, setPage] = useState<Page>(initialPage ?? 'dashboard');
  const groups = scope === '2fa-setup-only' ? SETUP_2FA_GROUPS
    : scope === 'change-password' ? CHANGE_PW_GROUPS
    : role === 'superadmin' ? [...ADMIN_NAV_GROUPS, ...SUPERADMIN_EXTRA_GROUPS]
    : ADMIN_NAV_GROUPS;
  const ActivePage = PAGES[page] ?? DashboardPage;
  const email = currentUserEmail ?? decodeEmailFromJwt();
  return (
    <RoleContext.Provider value={role}>
      <ActivityFeedProvider>
        <PermissionNotificationsProvider>
          <Toaster
            position="top-right"
            toastOptions={{
              className: 'glass-card',
              style: {
                background: 'rgba(20,20,24,0.9)',
                color: '#fff',
                border: '1px solid rgba(255,255,255,0.08)',
              },
            }}
          />
          {scope !== 'normal' && <ScopeLimitedBanner scope={scope} onLogout={onLogout} />}
          <div className="flex h-screen overflow-hidden">
            <Sidebar
              groups={groups}
              activePage={page}
              onSelect={(p) => setPage(p as Page)}
              onLogout={onLogout}
              currentUserEmail={email}
              onOpenMyPermissions={scope === 'normal' && role === 'admin' ? () => setPage('myPerms') : undefined}
            />
            <main className="flex-1 overflow-y-auto">
              <div className="max-w-[1400px] mx-auto p-6 lg:p-8 pb-20 md:pb-0"><ActivePage /></div>
            </main>
          </div>
        </PermissionNotificationsProvider>
      </ActivityFeedProvider>
    </RoleContext.Provider>
  );
}

// Exported for tests (forces the AdminPanel tree with a synthetic role prop).
export function AdminPanelForTest({ role }: { role: UserRole }) {
  return <AdminPanelInner role={role} onLogout={() => {}} />;
}
