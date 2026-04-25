import '../global.css';
import { useEffect, useState } from 'react';
import { View, Text, ActivityIndicator } from 'react-native';
import { Slot, useRouter } from 'expo-router';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from '@/lib/queryClient';
import { loadAuthFromStore, tryBiometricUnlock, AuthState } from '@/lib/auth';
import { RoleProvider } from '@/lib/roleContext';

export default function RootLayout() {
  const [auth, setAuth] = useState<AuthState | null>(null);
  const [checking, setChecking] = useState(true);
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
      </RoleProvider>
    </QueryClientProvider>
  );
}
