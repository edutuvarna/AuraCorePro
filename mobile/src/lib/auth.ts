import * as LocalAuthentication from 'expo-local-authentication';
import Constants from 'expo-constants';
import {
  setJwt, setRefreshToken, setLastActiveAt, clearAuth, getJwt, getRefreshToken,
  isInactiveBeyondLimit,
  setCachedJwt, setCachedRefreshToken,
  getCachedRefreshToken,
} from './secureStore';
import { unregisterPush } from './notifications';

export type Role = 'admin' | 'superadmin';

export interface AuthState {
  authenticated: boolean;
  role: Role;
  jwt: string | null;
}

export const INACTIVITY_LIMIT_DAYS = 30;

export function decodeRoleFromJwt(token: string | null): Role {
  try {
    if (!token) return 'admin';
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

/**
 * Side-effect-free read: returns AuthState if a JWT exists and the user
 * hasn't been inactive past the limit. Does NOT touch biometric — that's
 * AuthProvider's job (single-gate). After this returns truthy, AuthProvider
 * runs the biometric prompt, then calls hydrateCacheFromStore.
 */
export async function loadAuthFromStore(): Promise<AuthState | null> {
  if (await isInactiveBeyondLimit(INACTIVITY_LIMIT_DAYS)) {
    await clearAuth();
    return null;
  }
  const jwt = await getJwt();
  if (!jwt) return null;
  return { authenticated: true, role: decodeRoleFromJwt(jwt), jwt };
}

/**
 * Phase 6.15.1: called by AuthProvider after a successful biometric unlock.
 * Pulls JWT + refresh from SecureStore into the in-memory cache so the
 * `request()` path never touches SecureStore again this session.
 */
export async function hydrateCacheFromStore(): Promise<void> {
  const [jwt, refresh] = await Promise.all([getJwt(), getRefreshToken()]);
  setCachedJwt(jwt);
  setCachedRefreshToken(refresh);
}

export async function persistLoginSuccess(accessToken: string, refreshToken: string) {
  await setJwt(accessToken);
  await setRefreshToken(refreshToken);
  await setLastActiveAt(Date.now());
  // Phase 6.15.1: populate cache immediately so the next request() reads the
  // new token without round-tripping SecureStore.
  setCachedJwt(accessToken);
  setCachedRefreshToken(refreshToken);
}

export async function logout() {
  // unregister push BEFORE clearing — needs the JWT for the backend call.
  try { await unregisterPush(); } catch {}
  await clearAuth();  // clearAuth() also wipes the in-memory cache via clearAuthCache().
}

// ---------------------------------------------------------------------------
// Phase 6.15.2: JWT refresh-on-401 + auth-failure callback wiring.
// ---------------------------------------------------------------------------

const API_FOR_REFRESH = (Constants.expoConfig?.extra as any)?.apiUrl
  ?? process.env.EXPO_PUBLIC_API_URL
  ?? 'https://api.auracore.pro';

const MOBILE_SECRET = (Constants.expoConfig?.extra as any)?.mobileClientSecret
  ?? process.env.EXPO_PUBLIC_MOBILE_CLIENT_SECRET
  ?? '';

// Phase 6.15.2: AuthProvider registers a callback so api.ts can trigger
// logout when refresh fails (refresh-token expired or rotated by another
// session). Default no-op so unit tests don't crash on missing handler.
let onAuthFailure: () => void = () => {};

export function setOnAuthFailure(cb: () => void): void {
  onAuthFailure = cb;
}

export function fireAuthFailure(): void {
  try { onAuthFailure(); } catch (e) { console.warn('onAuthFailure handler threw:', e); }
}

export async function tryRefreshToken(): Promise<boolean> {
  const refresh = getCachedRefreshToken();
  if (!refresh) return false;
  try {
    const res = await fetch(`${API_FOR_REFRESH}/api/auth/refresh`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...(MOBILE_SECRET ? { 'X-Auracore-Mobile-Client': MOBILE_SECRET } : {}),
      },
      body: JSON.stringify({ refreshToken: refresh }),
    });
    if (!res.ok) return false;
    const data = await res.json().catch(() => null);
    if (!data?.accessToken || !data?.refreshToken) return false;
    await persistLoginSuccess(data.accessToken, data.refreshToken);
    return true;
  } catch {
    return false;
  }
}
