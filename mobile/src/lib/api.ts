import Constants from 'expo-constants';
import { getJwt } from './secureStore';

const API = (Constants.expoConfig?.extra as any)?.apiUrl
  ?? process.env.EXPO_PUBLIC_API_URL
  ?? 'https://api.auracore.pro';

// Phase 6.14: mobile-client CAPTCHA bypass. Backend's auth endpoints (login,
// register, redeem-invitation, forgot-password) require Cloudflare Turnstile
// in strict mode. RN can't render Turnstile without WebView + bridge, so we
// send a shared secret in this header and the backend's CheckCaptchaAsync
// short-circuits when MOBILE_CLIENT_SECRET env var matches. See
// app.json extra.mobileClientSecret + /etc/auracore-api.env on origin.
const MOBILE_CLIENT_SECRET = (Constants.expoConfig?.extra as any)?.mobileClientSecret
  ?? process.env.EXPO_PUBLIC_MOBILE_CLIENT_SECRET
  ?? '';

async function request(path: string, init: RequestInit = {}) {
  const token = await getJwt();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init.headers as Record<string, string>) ?? {}),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
  if (MOBILE_CLIENT_SECRET) headers['X-Auracore-Mobile-Client'] = MOBILE_CLIENT_SECRET;
  const res = await fetch(`${API}${path}`, { ...init, headers });
  return res;
}

async function safeJson(res: Response) {
  try { return await res.json(); } catch { return null; }
}

export const api = {
  async login(email: string, password: string, totpCode?: string) {
    const res = await request('/api/auth/login', {
      method: 'POST',
      body: JSON.stringify({ email, password, totpCode }),
    });
    return { ok: res.ok, status: res.status, data: await safeJson(res) };
  },

  async superadminLogin(email: string, password: string, totpCode?: string) {
    // Stricter endpoint: 3 fails / 60 min IP-rate-limit, mandatory 2FA, audit-logs
    // to audit_log on every attempt. Same DTO shape as /api/auth/login.
    const res = await request('/api/auth/superadmin/login', {
      method: 'POST',
      body: JSON.stringify({ email, password, totpCode }),
    });
    return { ok: res.ok, status: res.status, data: await safeJson(res) };
  },

  async getStats() {
    const res = await request('/api/admin/dashboard/stats');
    return res.ok ? await safeJson(res) : null;
  },

  async getPermissionRequests() {
    const res = await request('/api/superadmin/permission-requests?status=pending');
    return res.ok ? await safeJson(res) : { items: [] };
  },

  async approvePermissionRequest(id: string, expiresAt?: string) {
    const res = await request(`/api/superadmin/permission-requests/${id}/approve`, {
      method: 'POST',
      body: JSON.stringify({ expiresAt }),
    });
    return { ok: res.ok, status: res.status };
  },

  async denyPermissionRequest(id: string, reviewNote: string) {
    const res = await request(`/api/superadmin/permission-requests/${id}/deny`, {
      method: 'POST',
      body: JSON.stringify({ reviewNote }),
    });
    return { ok: res.ok, status: res.status };
  },

  async registerFcmToken(token: string, deviceId?: string) {
    const res = await request('/api/admin/me/fcm-token', {
      method: 'POST',
      body: JSON.stringify({ token, platform: 'android', deviceId }),
    });
    return res.ok;
  },

  async unregisterFcmToken(token: string) {
    // Body-as-DELETE (not query) so the FCM token doesn't land in nginx access logs.
    const res = await request('/api/admin/me/fcm-token', {
      method: 'DELETE',
      body: JSON.stringify({ token, platform: 'android' }),
    });
    return res.ok;
  },
};
