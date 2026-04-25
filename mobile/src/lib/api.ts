import Constants from 'expo-constants';
import { getJwt } from './secureStore';

const API = (Constants.expoConfig?.extra as any)?.apiUrl
  ?? process.env.EXPO_PUBLIC_API_URL
  ?? 'https://api.auracore.pro';

async function request(path: string, init: RequestInit = {}) {
  const token = await getJwt();
  const headers: Record<string, string> = {
    'Content-Type': 'application/json',
    ...((init.headers as Record<string, string>) ?? {}),
  };
  if (token) headers['Authorization'] = `Bearer ${token}`;
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
    const res = await request(`/api/admin/me/fcm-token?token=${encodeURIComponent(token)}`, {
      method: 'DELETE',
    });
    return res.ok;
  },
};
