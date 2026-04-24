const API = process.env.NEXT_PUBLIC_API_URL || 'https://api.auracore.pro';

let token: string | null = null;

export function setToken(t: string | null) { token = t; }
export function getToken() { return token; }

async function request(path: string, options: RequestInit = {}) {
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...(options.headers as Record<string, string> || {}),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;

  try {
    const res = await fetch(`${API}${path}`, { ...options, headers });
    if (res.status === 401 && !path.includes('/auth/')) {
      console.warn(`401 on ${path} — token may be expired`);
    }
    return res;
  } catch (err: any) {
    throw new Error(`Cannot connect to backend at ${API}. Is the server running?`);
  }
}

export const api = {
  // Auth
  async login(email: string, password: string, totpCode?: string) {
    try {
      const res = await request('/api/auth/login', {
        method: 'POST', body: JSON.stringify({ email, password, totpCode })
      });
      const data = await res.json();
      if (res.ok && data.accessToken) {
        token = data.accessToken;
        if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
      }
      return { ok: res.ok, data };
    } catch (err: any) {
      return { ok: false, data: { error: err.message || 'Connection failed' } };
    }
  },

  // Dashboard
  async getStats() {
    try { const res = await request('/api/admin/dashboard/stats'); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  async getRecentPayments(count = 10) {
    try { const res = await request(`/api/admin/dashboard/recent-payments?count=${count}`); return res.ok ? await res.json() : []; }
    catch { return []; }
  },

  async getPendingCrypto() {
    try { const res = await request('/api/admin/dashboard/pending-crypto'); return res.ok ? await res.json() : []; }
    catch { return []; }
  },

  async verifyCryptoPayment(paymentId: string) {
    try {
      const res = await request(`/api/payment/crypto/admin/verify/${paymentId}`, { method: 'POST' });
      return res.ok ? await res.json() : null;
    } catch { return null; }
  },

  async rejectCryptoPayment(paymentId: string) {
    try {
      const res = await request(`/api/payment/crypto/admin/reject/${paymentId}`, { method: 'POST' });
      return res.ok ? await res.json() : null;
    } catch { return null; }
  },

  // Users
  async getUsers(search?: string, page = 1, pageSize = 50) {
    try {
      let url = `/api/admin/users?page=${page}&pageSize=${pageSize}`;
      if (search) url += `&search=${encodeURIComponent(search)}`;
      const res = await request(url);
      return res.ok ? await res.json() : { users: [], total: 0 };
    } catch { return { users: [], total: 0 }; }
  },

  async deleteUser(id: string) {
    try { const res = await request(`/api/admin/users/${id}`, { method: 'DELETE' }); return res.ok; }
    catch { return false; }
  },

  async resetPassword(email: string, newPassword: string) {
    try {
      const res = await request('/api/admin/users/reset-password', {
        method: 'POST', body: JSON.stringify({ email, newPassword })
      });
      return res.ok;
    } catch { return false; }
  },

  // Subscriptions
  async grantSubscription(userId: string, tier: string, days: number) {
    try {
      const res = await request('/api/admin/subscriptions/grant', {
        method: 'POST', body: JSON.stringify({ userId, tier, days })
      });
      // Always parse body — error responses carry { error: "..." } that the
      // UI surfaces (Phase 6.10 hotfix Bug 1c).
      let data: any = null;
      try { data = await res.json(); } catch { data = null; }
      return { ok: res.ok, data };
    } catch (err: any) { return { ok: false, data: { error: err?.message || 'Network error' } }; }
  },

  async revokeSubscription(userId: string) {
    try { const res = await request(`/api/admin/subscriptions/revoke/${userId}`, { method: 'POST' }); return res.ok; }
    catch { return false; }
  },

  // Health
  async getHealth() {
    try {
      const res = await fetch(`${API}/health`);
      return res.ok ? await res.json() : { status: 'error' };
    } catch { return { status: 'offline' }; }
  },

  // 2FA
  async setup2fa() {
    try {
      const res = await request('/api/2fa/setup', { method: 'POST' });
      if (res.ok) return await res.json();
      const err = await res.json().catch(() => ({}));
      return { error: err.error || `Server returned ${res.status}` };
    } catch (err: any) { return { error: err.message || 'Connection failed' }; }
  },

  async verify2fa(code: string) {
    try {
      const res = await request('/api/2fa/verify', {
        method: 'POST', body: JSON.stringify({ code })
      });
      return { ok: res.ok, data: await res.json() };
    } catch (err: any) {
      return { ok: false, data: { error: err.message || 'Connection failed' } };
    }
  },

  async disable2fa(code: string) {
    try {
      const res = await request('/api/2fa/disable', {
        method: 'POST', body: JSON.stringify({ code })
      });
      return { ok: res.ok, data: await res.json() };
    } catch (err: any) {
      return { ok: false, data: { error: err.message || 'Connection failed' } };
    }
  },

  async get2faStatus() {
    try {
      const res = await request('/api/2fa/status');
      return res.ok ? await res.json() : { enabled: false };
    } catch { return { enabled: false }; }
  },

  // Updates
  async getUpdates() {
    try { const res = await request('/api/admin/updates'); return res.ok ? await res.json() : []; }
    catch { return []; }
  },

  async publishUpdate(data: { version: string; downloadUrl: string; releaseNotes?: string; channel?: string; isMandatory: boolean }) {
    try {
      const res = await request('/api/admin/updates/publish', {
        method: 'POST', body: JSON.stringify(data)
      });
      return { ok: res.ok, data: await res.json() };
    } catch (err: any) { return { ok: false, data: { error: err.message } }; }
  },

  async deleteUpdate(id: string) {
    try { const res = await request(`/api/admin/updates/${id}`, { method: 'DELETE' }); return res.ok; }
    catch { return false; }
  },

  // Licenses
  async getLicenses(page = 1, search?: string) {
    try {
      const params = new URLSearchParams({ page: String(page) });
      if (search) params.set('search', search);
      const res = await request(`/api/admin/licenses?${params}`);
      return res.ok ? await res.json() : { items: [], total: 0, page: 1, pages: 0 };
    } catch { return { items: [], total: 0, page: 1, pages: 0 }; }
  },

  async revokeLicense(id: string) {
    try { const res = await request(`/api/admin/licenses/${id}/revoke`, { method: 'PUT' }); return res.ok; }
    catch { return false; }
  },

  async activateLicense(id: string, tier?: string) {
    try {
      const opts: RequestInit = { method: 'PUT' };
      if (tier) {
        opts.headers = { 'Content-Type': 'application/json' };
        opts.body = JSON.stringify({ tier });
      }
      const res = await request(`/api/admin/licenses/${id}/activate`, opts);
      return res.ok;
    } catch { return false; }
  },

  // Whitelist (Phase 6.10 W5.T25 — canonical path /api/admin/ip-whitelist;
  // Phase 6.8 alias /api/admin/whitelist retired backend-side).
  async getWhitelist() {
    // Backend returns { total, page, pageSize, items: [...] } (paginated);
    // frontend IpWhitelistPage treats the return as a flat array. Unwrap
    // .items. Defensive: also accept a bare array.
    try {
      const res = await request('/api/admin/ip-whitelist');
      if (!res.ok) return [];
      const data = await res.json();
      return Array.isArray(data) ? data : (data?.items ?? []);
    }
    catch { return []; }
  },

  async addWhitelistIp(ip: string, label?: string) {
    try {
      const res = await request('/api/admin/ip-whitelist', {
        method: 'POST', body: JSON.stringify({ ip, label })
      });
      return { ok: res.ok, data: await res.json() };
    } catch (err: any) { return { ok: false, data: { error: err.message } }; }
  },

  async removeWhitelistIp(ip: string) {
    try { const res = await request(`/api/admin/ip-whitelist/${encodeURIComponent(ip)}`, { method: 'DELETE' }); return res.ok; }
    catch { return false; }
  },

  async getMyIp() {
    try { const res = await request('/api/admin/ip-whitelist/my-ip'); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  // ═══════════════════════════════════════════════════════════
  // NEW: Charts
  // ═══════════════════════════════════════════════════════════
  async getRevenueChart(days = 30) {
    // Backend AdminChartController.Revenue returns { days, total, items: [...] }.
    // Frontend expects a flat array for .map; unwrap .items.
    try {
      const res = await request(`/api/admin/charts/revenue?days=${days}`);
      if (!res.ok) return [];
      const data = await res.json();
      return Array.isArray(data) ? data : (data?.items ?? []);
    }
    catch { return []; }
  },

  async getRegistrationChart(days = 30) {
    try {
      const res = await request(`/api/admin/charts/registrations?days=${days}`);
      if (!res.ok) return [];
      const data = await res.json();
      return Array.isArray(data) ? data : (data?.items ?? []);
    }
    catch { return []; }
  },

  // ═══════════════════════════════════════════════════════════
  // Audit Log (Phase 6.10 W5.T23 — native pass-through; previous Phase 6.9
  // login_attempts adapter retired now that AuditLogPage renders audit_log
  // columns natively.)
  // ═══════════════════════════════════════════════════════════
  async getAuditLog(actorEmail?: string, action?: string, page = 1, pageSize = 50) {
    try {
      let url = `/api/admin/audit-log?page=${page}&pageSize=${pageSize}`;
      if (actorEmail) url += `&actorEmail=${encodeURIComponent(actorEmail)}`;
      if (action) url += `&action=${encodeURIComponent(action)}`;
      const res = await request(url);
      if (!res.ok) return { total: 0, page: 1, pageSize, pages: 0, items: [] };
      return await res.json();
    } catch { return { total: 0, page: 1, pageSize, pages: 0, items: [] }; }
  },

  async getAuditLogStats() {
    try {
      const res = await request('/api/admin/audit-log/stats');
      if (!res.ok) return null;
      return await res.json();
    } catch { return null; }
  },

  // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  // Crash Reports
  // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  async getCrashReports(search?: string, version?: string, page = 1, pageSize = 50) {
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (search) params.set('search', search);
      if (version) params.set('version', version);
      const res = await request(`/api/admin/crash-reports?${params}`);
      return res.ok ? await res.json() : { items: [], total: 0, page: 1, pages: 0 };
    } catch { return { items: [], total: 0, page: 1, pages: 0 }; }
  },

  async getCrashReport(id: string) {
    try { const res = await request(`/api/admin/crash-reports/${id}`); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  async getCrashStats() {
    try { const res = await request('/api/admin/crash-reports/stats'); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  async deleteCrashReport(id: string) {
    try { const res = await request(`/api/admin/crash-reports/${id}`, { method: 'DELETE' }); return res.ok; }
    catch { return false; }
  },

  // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  // Devices
  // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  async getDevices(search?: string, page = 1, pageSize = 50) {
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (search) params.set('search', search);
      const res = await request(`/api/admin/devices?${params}`);
      return res.ok ? await res.json() : { items: [], total: 0, page: 1, pages: 0 };
    } catch { return { items: [], total: 0, page: 1, pages: 0 }; }
  },

  async getDevice(id: string) {
    try { const res = await request(`/api/admin/devices/${id}`); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  async getDeviceStats() {
    try { const res = await request('/api/admin/devices/stats'); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  async deleteDevice(id: string) {
    try { const res = await request(`/api/admin/devices/${id}`, { method: 'DELETE' }); return res.ok; }
    catch { return false; }
  },

  // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  // Telemetry
  // â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•â•
  async getTelemetry(eventType?: string, page = 1, pageSize = 50) {
    try {
      const params = new URLSearchParams({ page: String(page), pageSize: String(pageSize) });
      if (eventType) params.set('eventType', eventType);
      const res = await request(`/api/admin/telemetry?${params}`);
      return res.ok ? await res.json() : { items: [], total: 0, page: 1, pages: 0 };
    } catch { return { items: [], total: 0, page: 1, pages: 0 }; }
  },

  async getTelemetryStats() {
    try { const res = await request('/api/admin/telemetry/stats'); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  async getTelemetryEventTypes() {
    try { const res = await request('/api/admin/telemetry/event-types'); return res.ok ? await res.json() : []; }
    catch { return []; }
  },

  // Config
  async getConfig() {
    try { const res = await request('/api/admin/config'); return res.ok ? await res.json() : null; }
    catch { return null; }
  },

  async updateConfig(update: Record<string, any>) {
    try {
      const res = await request('/api/admin/config', {
        method: 'PUT', body: JSON.stringify(update)
      });
      return res.ok ? await res.json() : null;
    } catch { return null; }
  },

  // ── Phase 6.11 ───────────────────────────────────────

  async superadminLogin(email: string, password: string, totpCode?: string) {
    const res = await request('/api/auth/superadmin/login', {
      method: 'POST',
      body: JSON.stringify({ email, password, totpCode }),
    });
    const data = await res.json();
    if (res.ok && data.accessToken) {
      token = data.accessToken;
      if (typeof window !== 'undefined') localStorage.setItem('aura_token', data.accessToken);
    }
    return { ok: res.ok, data };
  },

  async getMyPermissions() {
    const res = await request('/api/admin/my-permissions');
    return res.ok ? await res.json() : null;
  },

  async createPermissionRequest(permissionKey: string, reason: string) {
    const res = await request('/api/admin/permission-requests', {
      method: 'POST',
      body: JSON.stringify({ permissionKey, reason }),
    });
    return { ok: res.ok, status: res.status, data: res.ok ? await res.json() : await safeJson(res) };
  },

  async listMyPermissionRequests() {
    const res = await request('/api/admin/permission-requests');
    return res.ok ? await res.json() : { items: [] };
  },

  async cancelPermissionRequest(id: string) {
    const res = await request(`/api/admin/permission-requests/${id}/cancel`, { method: 'POST' });
    return { ok: res.ok };
  },

  // Superadmin-only
  async listPermissionRequests(status = 'pending') {
    const res = await request(`/api/superadmin/permission-requests?status=${status}`);
    return res.ok ? await res.json() : { items: [] };
  },

  async approvePermissionRequest(id: string, expiresAt?: string | null, reviewNote?: string) {
    const res = await request(`/api/superadmin/permission-requests/${id}/approve`, {
      method: 'POST', body: JSON.stringify({ expiresAt, reviewNote }),
    });
    return { ok: res.ok };
  },

  async denyPermissionRequest(id: string, reviewNote?: string) {
    const res = await request(`/api/superadmin/permission-requests/${id}/deny`, {
      method: 'POST', body: JSON.stringify({ reviewNote }),
    });
    return { ok: res.ok };
  },

  async bulkApprovePermissionRequests(ids: string[]) {
    const res = await request('/api/superadmin/permission-requests/bulk/approve', {
      method: 'POST', body: JSON.stringify({ ids }),
    });
    return { ok: res.ok };
  },

  async bulkDenyPermissionRequests(ids: string[]) {
    const res = await request('/api/superadmin/permission-requests/bulk/deny', {
      method: 'POST', body: JSON.stringify({ ids }),
    });
    return { ok: res.ok };
  },

  async revokePermissionGrant(adminUserId: string, permissionKey: string, reason: string) {
    const res = await request('/api/superadmin/permission-grants/revoke', {
      method: 'POST', body: JSON.stringify({ adminUserId, permissionKey, reason }),
    });
    return { ok: res.ok };
  },

  async listAdminAccounts() {
    const res = await request('/api/superadmin/admins');
    return res.ok ? await res.json() : { items: [] };
  },

  async createAdminAccount(body: {
    email: string;
    sendInvitation: boolean;
    initialPassword?: string;
    forcePasswordChange: 'on_first_login' | 'within_7_days' | 'within_30_days' | 'never';
    template: 'Default' | 'Trusted' | 'ReadOnly' | 'Custom';
    customKeys?: { permissionKey: string; expiresAt?: string | null }[];
    require2fa: boolean;
  }) {
    const res = await request('/api/superadmin/admins', {
      method: 'POST', body: JSON.stringify(body),
    });
    return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
  },

  async promoteUserToAdmin(userId: string, body: {
    template: 'Default' | 'Trusted' | 'ReadOnly' | 'Custom';
    forcePasswordChange: 'on_first_login' | 'within_7_days' | 'within_30_days' | 'never';
    require2fa: boolean;
    customKeys?: { permissionKey: string; expiresAt?: string | null }[];
  }) {
    const res = await request(`/api/superadmin/users/${userId}/promote`, {
      method: 'POST', body: JSON.stringify(body),
    });
    return { ok: res.ok };
  },

  async demoteAdminToUser(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/demote`, { method: 'POST' });
    return { ok: res.ok };
  },

  async suspendAdmin(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/suspend`, { method: 'POST' });
    return { ok: res.ok };
  },

  async restoreAdmin(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/restore`, { method: 'POST' });
    return { ok: res.ok };
  },

  async deleteAdmin(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}`, { method: 'DELETE' });
    return { ok: res.ok };
  },

  async resetAdminPassword(adminId: string) {
    const res = await request(`/api/superadmin/admins/${adminId}/reset-password`, { method: 'POST' });
    return { ok: res.ok };
  },

  async applyAdminTemplate(adminId: string, body: {
    template: 'Default' | 'Trusted' | 'ReadOnly' | 'Custom';
    customKeys?: { permissionKey: string; expiresAt?: string | null }[];
  }) {
    const res = await request(`/api/superadmin/admins/${adminId}/apply-template`, {
      method: 'POST', body: JSON.stringify(body),
    });
    return { ok: res.ok, data: res.ok ? await res.json() : await safeJson(res) };
  },

  async changePassword(currentPassword: string, newPassword: string) {
    const res = await request('/api/auth/change-password', {
      method: 'POST', body: JSON.stringify({ currentPassword, newPassword }),
    });
    return { ok: res.ok, data: await safeJson(res) };
  },

  async redeemInvitation(token: string, email: string, newPassword: string) {
    const res = await request('/api/auth/redeem-invitation', {
      method: 'POST', body: JSON.stringify({ token, email, newPassword }),
    });
    return { ok: res.ok, data: await safeJson(res) };
  },

  async getSecurityPolicy() {
    const res = await request('/api/superadmin/security-policy');
    return res.ok ? await res.json() : null;
  },

  async updateSecurityPolicy(require2faForAllAdmins: boolean) {
    const res = await request('/api/superadmin/security-policy', {
      method: 'PUT', body: JSON.stringify({ require2faForAllAdmins }),
    });
    return { ok: res.ok };
  },

  async setAdminRequire2fa(adminId: string, require2fa: boolean) {
    const res = await request(`/api/superadmin/admins/${adminId}/require-2fa`, {
      method: 'PUT', body: JSON.stringify({ require2fa }),
    });
    return { ok: res.ok };
  },

  async getRateLimitPolicies() {
    const res = await request('/api/superadmin/rate-limits');
    return res.ok ? await res.json() : { items: [] };
  },

  async updateRateLimitPolicy(endpoint: string, requests: number, windowSeconds: number) {
    const res = await request(`/api/superadmin/rate-limits/${encodeURIComponent(endpoint)}`, {
      method: 'PUT', body: JSON.stringify({ requests, windowSeconds }),
    });
    return { ok: res.ok };
  },

  async listAdminActionLog(params: { actorEmail?: string; action?: string; dateFrom?: string; dateTo?: string; page?: number; pageSize?: number } = {}) {
    const qs = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => v != null && qs.append(k, String(v)));
    const res = await request(`/api/superadmin/admin-actions?${qs}`);
    return res.ok ? await res.json() : { items: [], total: 0 };
  },

  async getAdminActionStats() {
    const res = await request('/api/superadmin/admin-actions/stats');
    return res.ok ? await res.json() : null;
  },

  exportAuditLogCsvUrl(params: { dateFrom?: string; dateTo?: string; actorEmail?: string; action?: string } = {}) {
    const qs = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => v != null && qs.append(k, String(v)));
    return `${API}/api/admin/audit-log/export.csv?${qs}`;
  },

  exportAdminActionLogCsvUrl(params: { dateFrom?: string; dateTo?: string; actorEmail?: string; action?: string } = {}) {
    const qs = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => v != null && qs.append(k, String(v)));
    return `${API}/api/superadmin/admin-actions/export.csv?${qs}`;
  },
};

async function safeJson(res: Response) {
  try { return await res.json(); } catch { return {}; }
}