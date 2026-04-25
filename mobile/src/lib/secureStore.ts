import * as SecureStore from 'expo-secure-store';

const KEY_JWT = 'aura_jwt';
const KEY_REFRESH = 'aura_refresh';
const KEY_LAST_ACTIVE = 'aura_last_active';

export async function setJwt(token: string) {
  await SecureStore.setItemAsync(KEY_JWT, token, { requireAuthentication: true });
}

export async function getJwt(): Promise<string | null> {
  return SecureStore.getItemAsync(KEY_JWT);
}

export async function setRefreshToken(token: string) {
  await SecureStore.setItemAsync(KEY_REFRESH, token, { requireAuthentication: true });
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
  await SecureStore.deleteItemAsync(KEY_JWT);
  await SecureStore.deleteItemAsync(KEY_REFRESH);
  await SecureStore.deleteItemAsync(KEY_LAST_ACTIVE);
}
