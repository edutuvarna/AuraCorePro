import '../global.css';
import { useEffect, useState } from 'react';
import { View, Text, ActivityIndicator } from 'react-native';
import { Slot, useRouter } from 'expo-router';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from '@/lib/queryClient';
import { loadAuthFromStore, tryBiometricUnlock, AuthState } from '@/lib/auth';
import { RoleProvider } from '@/lib/roleContext';
import { attachForegroundListener, attachTapListener } from '@/lib/notifications';
import { InAppNotificationBanner } from '@/components/InAppNotificationBanner';

export default function RootLayout() {
  const [auth, setAuth] = useState<AuthState | null>(null);
  const [checking, setChecking] = useState(true);
  const [banner, setBanner] = useState<{ title: string; body: string; data: Record<string, unknown> } | null>(null);
  const router = useRouter();

  useEffect(() => {
    (async () => {
      const cached = await loadAuthFromStore();
      if (!cached) { setChecking(false); router.replace('/(auth)/login'); return; }
      const ok = await tryBiometricUnlock();
      if (!ok) { setChecking(false); router.replace('/(auth)/login'); return; }
      setAuth(cached);
      setChecking(false);
      router.replace('/(app)');
    })();
  }, []);

  useEffect(() => {
    if (!auth) return;
    const fgSub = attachForegroundListener((data) => {
      setBanner({
        title: 'Permission request',
        body: typeof data.body === 'string' ? data.body : 'New request',
        data,
      });
    });
    const tapSub = attachTapListener((data) => {
      if (data.type === 'permission-request') {
        router.push('/(app)/permissions');
      }
    });
    return () => { fgSub.remove(); tapSub.remove(); };
  }, [auth]);

  if (checking) {
    return (
      <View className="flex-1 items-center justify-center bg-surface-900">
        <ActivityIndicator size="large" color="#22d3ee" />
        <Text className="text-white/40 text-xs mt-2">Unlocking…</Text>
      </View>
    );
  }

  return (
    <QueryClientProvider client={queryClient}>
      <RoleProvider value={auth?.role ?? 'admin'}>
        <Slot />
        <InAppNotificationBanner
          visible={banner !== null}
          title={banner?.title ?? ''}
          body={banner?.body ?? ''}
          onDismiss={() => setBanner(null)}
          onTap={() => {
            if (banner?.data?.type === 'permission-request') router.push('/(app)/permissions');
            setBanner(null);
          }}
        />
      </RoleProvider>
    </QueryClientProvider>
  );
}
