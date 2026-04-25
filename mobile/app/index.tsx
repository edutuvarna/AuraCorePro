import { Redirect } from 'expo-router';
import { View, ActivityIndicator, Text } from 'react-native';
import { useAuth } from '@/lib/authContext';

/**
 * Root index dispatcher — declarative <Redirect> based on auth state.
 *
 * Until AuthProvider's biometric-unlock effect resolves (`checking` true),
 * we render a splash overlay. After that, redirect to (app) or (auth)/login.
 * Using <Redirect> avoids the navigation-tree-not-ready race that kills
 * router.replace calls from a parent _layout's useEffect.
 */
export default function Index() {
  const { auth, checking } = useAuth();

  if (checking) {
    return (
      <View className="flex-1 items-center justify-center bg-surface-900">
        <ActivityIndicator size="large" color="#22d3ee" />
        <Text className="text-white/40 text-xs mt-2">Unlocking…</Text>
      </View>
    );
  }

  if (auth) return <Redirect href="/(app)" />;
  return <Redirect href="/(auth)/login" />;
}
