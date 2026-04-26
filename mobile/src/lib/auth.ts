import * as LocalAuthentication from 'expo-local-authentication';
import { setJwt, setRefreshToken, setLastActiveAt, clearAuth, getJwt, isInactiveBeyondLimit } from './secureStore';
import { api } from './api';
import { unregisterPush } from './notifications';

export type Role = 'admin' | 'superadmin';

export interface AuthState {
  authenticated: boolean;
  role: Role;
  jwt: string | null;
}

export const INACTIVITY_LIMIT_DAYS = 30;

export function decodeRoleFromJwt(token: string | null): Role {
  if (!token) return 'admin';
  try {
    const parts = token.split('.');
    if (parts.length < 2) return 'admin';
    const payload = JSON.parse(atob(parts[1]));
    const claim = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];
    const roles: string[] = Array.isArray(claim) ? claim : [claim].filter(Boolean);
    if (roles.includes('superadmin')) return 'superadmin';
    if (roles.includes('admin')) return 'admin';
  } catch {}
  return 'admin';
}

export async function tryBiometricUnlock(): Promise<boolean> {
  const hardware = await LocalAuthentication.hasHardwareAsync();
  const enrolled = await LocalAuthentication.isEnrolledAsync();
  if (!hardware || !enrolled) return false;
  const res = await LocalAuthentication.authenticateAsync({
    promptMessage: 'Unlock AuraCore Admin',
    fallbackLabel: 'Use password',
    disableDeviceFallback: false,
  });
  return res.success;
}

export async function loadAuthFromStore(): Promise<AuthState | null> {
  if (await isInactiveBeyondLimit(INACTIVITY_LIMIT_DAYS)) {
    await clearAuth();
    return null;
  }
  const jwt = await getJwt();
  if (!jwt) return null;
  return { authenticated: true, role: decodeRoleFromJwt(jwt), jwt };
}

export async function persistLoginSuccess(accessToken: string, refreshToken: string) {
  await setJwt(accessToken);
  await setRefreshToken(refreshToken);
  await setLastActiveAt(Date.now());
}

export async function logout() {
  try { await unregisterPush(); } catch {}
  await clearAuth();
}
