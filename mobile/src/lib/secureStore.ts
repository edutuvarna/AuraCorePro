import * as SecureStore from 'expo-secure-store';

const KEY_JWT = 'aura_jwt';
const KEY_REFRESH = 'aura_refresh';
const KEY_LAST_ACTIVE = 'aura_last_active';

// Phase 6.15.1: in-memory cache populated by AuthProvider after the single
// biometric unlock. All request paths read from the cache, so SecureStore
// reads (and any biometric prompt) happen exactly once per app session.
let cachedJwt: string | null = null;
let cachedRefresh: string | null = null;

export function setCachedJwt(token: string | null): void {
  cachedJwt = token;
}

export function getCachedJwt(): string | null {
  return cachedJwt;
}

export function setCachedRefreshToken(token: string | null): void {
  cachedRefresh = token;
}

export function getCachedRefreshToken(): string | null {
  return cachedRefresh;
}

export function clearAuthCache(): void {
  cachedJwt = null;
  cachedRefresh = null;
}

// Phase 6.15.1: requireAuthentication dropped on writes. Single-gate biometric
// happens via expo-local-authentication in AuthProvider mount; the OS Keystore
// master key still encrypts at rest. Threat model: physical-device-theft +
// screen-unlock-bypass exposes both the SecureStore item and the in-memory
// cache equivalently — no marginal security loss vs. double-gate.
export async function setJwt(token: string) {
  await SecureStore.setItemAsync(KEY_JWT, token);
}

export async function getJwt(): Promise<string | null> {
  return SecureStore.getItemAsync(KEY_JWT);
}

export async function setRefreshToken(token: string) {
  await SecureStore.setItemAsync(KEY_REFRESH, token);
}

export async function getRefreshToken(): Promise<string | null> {
  return SecureStore.getItemAsync(KEY_REFRESH);
}

export async function setLastActiveAt(epochMs: number) {
  await SecureStore.setItemAsync(KEY_LAST_ACTIVE, String(epochMs));
}

export async function getLastActiveAt(): Promise<number | null> {
  const v = await SecureStore.getItemAsync(KEY_LAST_ACTIVE);
  return v ? Number(v) : null;
}

export async function isInactiveBeyondLimit(days: number): Promise<boolean> {
  const last = await getLastActiveAt();
  if (last == null) return true;
  const ageMs = Date.now() - last;
  return ageMs > days * 24 * 60 * 60 * 1000;
}

export async function clearAuth() {
  clearAuthCache();
  await SecureStore.deleteItemAsync(KEY_JWT);
  await SecureStore.deleteItemAsync(KEY_REFRESH);
  await SecureStore.deleteItemAsync(KEY_LAST_ACTIVE);
}
