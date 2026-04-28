import * as LocalAuthentication from 'expo-local-authentication';
import * as SecureStore from 'expo-secure-store';
import {
  persistLoginSuccess, logout, loadAuthFromStore, hydrateCacheFromStore,
} from '@/lib/auth';
import {
  getCachedJwt, getCachedRefreshToken, clearAuthCache, setCachedJwt, setCachedRefreshToken,
  clearAuth,
} from '@/lib/secureStore';

describe('auth.ts — Phase 6.15 cache integration', () => {
  beforeEach(async () => {
    await clearAuth();
    clearAuthCache();
    jest.clearAllMocks();
    (LocalAuthentication.hasHardwareAsync as jest.Mock).mockResolvedValue(true);
    (LocalAuthentication.isEnrolledAsync as jest.Mock).mockResolvedValue(true);
    (LocalAuthentication.authenticateAsync as jest.Mock).mockResolvedValue({ success: true });
  });

  it('persistLoginSuccess writes both SecureStore AND populates cache', async () => {
    await persistLoginSuccess('access.token.abc', 'refresh.token.xyz');
    expect(getCachedJwt()).toBe('access.token.abc');
    expect(getCachedRefreshToken()).toBe('refresh.token.xyz');
  });

  it('logout clears cache AND SecureStore', async () => {
    setCachedJwt('a'); setCachedRefreshToken('b');
    await SecureStore.setItemAsync('aura_jwt', 'a');
    await SecureStore.setItemAsync('aura_refresh', 'b');
    await logout();
    expect(getCachedJwt()).toBeNull();
    expect(getCachedRefreshToken()).toBeNull();
    expect(await SecureStore.getItemAsync('aura_jwt')).toBeNull();
    expect(await SecureStore.getItemAsync('aura_refresh')).toBeNull();
  });

  it('loadAuthFromStore does NOT call biometric (side-effect-free read)', async () => {
    await SecureStore.setItemAsync('aura_jwt', 'header.eyJyb2xlIjoiYWRtaW4ifQ.sig');
    await SecureStore.setItemAsync('aura_last_active', String(Date.now()));
    const result = await loadAuthFromStore();
    expect(result).not.toBeNull();
    expect(LocalAuthentication.authenticateAsync).not.toHaveBeenCalled();
  });

  it('hydrateCacheFromStore reads SecureStore into cache (post-biometric)', async () => {
    await SecureStore.setItemAsync('aura_jwt', 'jwt.value');
    await SecureStore.setItemAsync('aura_refresh', 'refresh.value');
    await hydrateCacheFromStore();
    expect(getCachedJwt()).toBe('jwt.value');
    expect(getCachedRefreshToken()).toBe('refresh.value');
  });
});
