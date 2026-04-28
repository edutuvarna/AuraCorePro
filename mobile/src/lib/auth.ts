import * as LocalAuthentication from 'expo-local-authentication';
import {
  setJwt, setRefreshToken, setLastActiveAt, clearAuth, getJwt, getRefreshToken,
  isInactiveBeyondLimit,
  setCachedJwt, setCachedRefreshToken,
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
