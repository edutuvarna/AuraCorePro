import { createContext, useContext, useEffect, useState, ReactNode } from 'react';
import { loadAuthFromStore, tryBiometricUnlock, AuthState } from './auth';

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
 * Centralizes auth-state loading + biometric unlock so RootLayout doesn't have
 * to call router.replace from a useEffect (which races with Expo Router's
 * navigation-tree initialization, leaving the splash spinner visible forever).
 *
 * Pattern: AuthProvider runs the auth check on mount. The root index route
 * (mobile/app/index.tsx) reads useAuth() and renders <Redirect> based on the
 * decision. The redirect is declarative — no router.replace timing surprises.
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
          setChecking(false);
          return;
        }
        setAuth(cached);
        setChecking(false);
      } catch (e) {
        // Auth load failures land on LoginScreen, not stuck-spinner.
        console.warn('Auth load failed:', e);
        if (!cancelled) setChecking(false);
      }
    })();
    return () => { cancelled = true; };
  }, []);

  return <AuthContext.Provider value={{ auth, checking, setAuth }}>{children}</AuthContext.Provider>;
}

export const useAuth = () => useContext(AuthContext);
