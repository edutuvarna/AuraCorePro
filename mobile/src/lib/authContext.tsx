import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import {
  loadAuthFromStore, tryBiometricUnlock, hydrateCacheFromStore, AuthState,
} from './auth';
import { clearAuth } from './secureStore';

interface AuthCtx {
  auth: AuthState | null;
  checking: boolean;
  setAuth: (a: AuthState | null) => void;
}

const AuthContext = createContext<AuthCtx>({
  auth: null,
  checking: true,
  setAuth: () => {},
});

/**
 * Phase 6.15.1: Single-gate biometric.
 *
 * Flow on cold start:
 *   1. loadAuthFromStore — side-effect-free SecureStore read, no biometric
 *   2. If no JWT or stale → checking=false, Index renders <Redirect href="/(auth)/login" />
 *   3. If JWT present → tryBiometricUnlock (THE single biometric prompt)
 *   4. On success → hydrateCacheFromStore + setAuth → Index renders /(app)
 *   5. On 3-fail/cancel → clearAuth (also clears cache) + checking=false → /(auth)/login
 *
 * Subsequent api.request() calls use getCachedJwt() — no SecureStore touch,
 * no biometric prompt. Tab switching, refreshes, pull-to-refresh = 0 prompts.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [auth, setAuth] = useState<AuthState | null>(null);
  const [checking, setChecking] = useState(true);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      try {
        const cached = await loadAuthFromStore();
        if (cancelled) return;
        if (!cached) {
          setChecking(false);
          return;
        }
        const ok = await tryBiometricUnlock();
        if (cancelled) return;
        if (!ok) {
          await clearAuth();  // also clears in-memory cache
          setChecking(false);
          return;
        }
        await hydrateCacheFromStore();
        if (cancelled) return;
        setAuth(cached);
        setChecking(false);
      } catch (e) {
        console.warn('Auth load failed:', e);
        if (!cancelled) setChecking(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  return <AuthContext.Provider value={{ auth, checking, setAuth }}>{children}</AuthContext.Provider>;
}

export const useAuth = () => useContext(AuthContext);
