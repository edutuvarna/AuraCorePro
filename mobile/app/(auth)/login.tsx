import { useState } from 'react';
import { View, Text, TextInput, Pressable, ActivityIndicator } from 'react-native';
import { useRouter } from 'expo-router';
import { api } from '@/lib/api';
import { persistLoginSuccess, decodeRoleFromJwt } from '@/lib/auth';
import { registerForPush } from '@/lib/notifications';
import { useAuth } from '@/lib/authContext';

export default function LoginScreen() {
  const router = useRouter();
  const { setAuth } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [totpCode, setTotpCode] = useState('');
  const [needs2fa, setNeeds2fa] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const submit = async (mode: 'admin' | 'superadmin') => {
    setError(''); setLoading(true);
    try {
      const { ok, data } = mode === 'admin'
        ? await api.login(email, password, totpCode || undefined)
        : await api.superadminLogin(email, password, totpCode || undefined);
      if (data?.requires2fa && !totpCode) { setNeeds2fa(true); return; }
      if (ok && data?.accessToken && data?.refreshToken) {
        await persistLoginSuccess(data.accessToken, data.refreshToken);
        try { await registerForPush(); } catch (e: any) { /* push optional */ }
        // Update auth context BEFORE navigating so (app) tree sees a valid session.
        setAuth({
          authenticated: true,
          role: decodeRoleFromJwt(data.accessToken),
          jwt: data.accessToken,
        });
        router.replace('/(app)');
        return;
      }
      setError(data?.error ?? 'Authentication failed');
    } finally { setLoading(false); }
  };

  return (
    <View className="flex-1 items-center justify-center bg-surface-900 px-6">
      <View className="w-full max-w-md gap-4">
        <Text className="text-2xl font-bold text-white text-center mb-4">AuraCore Admin</Text>
        <Text className="text-xs text-white/40 uppercase tracking-wider">Email</Text>
        <TextInput
          value={email}
          onChangeText={setEmail}
          autoCapitalize="none"
          keyboardType="email-address"
          placeholder="email"
          placeholderTextColor="rgba(255,255,255,0.3)"
          className="bg-white/[0.03] border border-white/[0.08] rounded-md px-3 py-3 text-white text-sm"
        />
        <Text className="text-xs text-white/40 uppercase tracking-wider">Password</Text>
        <TextInput
          value={password}
          onChangeText={setPassword}
          secureTextEntry
          placeholder="password"
          placeholderTextColor="rgba(255,255,255,0.3)"
          className="bg-white/[0.03] border border-white/[0.08] rounded-md px-3 py-3 text-white text-sm"
        />
        {needs2fa && (
          <>
            <Text className="text-xs text-white/40 uppercase tracking-wider">2FA Code</Text>
            <TextInput
              value={totpCode}
              onChangeText={setTotpCode}
              keyboardType="number-pad"
              maxLength={6}
              placeholder="6-digit TOTP"
              placeholderTextColor="rgba(255,255,255,0.3)"
              className="bg-white/[0.03] border border-white/[0.08] rounded-md px-3 py-3 text-white text-base text-center tracking-widest"
            />
          </>
        )}
        {error ? (
          <Text className="text-red-400 text-xs">{error}</Text>
        ) : null}
        <Pressable
          onPress={() => submit('admin')}
          disabled={loading || !email || !password}
          className="bg-accent rounded-md py-3 items-center"
        >
          {loading
            ? <ActivityIndicator color="#0a0a0f" />
            : <Text className="text-surface-900 font-semibold">{needs2fa ? 'Verify 2FA' : 'Sign In as Admin'}</Text>}
        </Pressable>
        <Pressable
          onPress={() => submit('superadmin')}
          disabled={loading || !email || !password}
          className="bg-gradient-to-r rounded-md py-3 items-center border border-accent-secondary/40 bg-accent-secondary/[0.08]"
        >
          {loading
            ? <ActivityIndicator color="#a78bfa" />
            : <Text className="text-accent-secondary font-semibold">{needs2fa ? 'Verify 2FA (Superadmin)' : 'Sign In as Superadmin'}</Text>}
        </Pressable>
      </View>
    </View>
  );
}
