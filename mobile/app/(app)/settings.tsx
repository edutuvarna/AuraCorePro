import { View, Text, Pressable, Alert } from 'react-native';
import { useRouter } from 'expo-router';
import { logout } from '@/lib/auth';
import { useRole } from '@/lib/roleContext';

export default function Settings() {
  const router = useRouter();
  const role = useRole();

  const onLogout = async () => {
    Alert.alert('Sign out?', 'You will need to log in again.', [
      { text: 'Cancel', style: 'cancel' },
      {
        text: 'Sign out',
        style: 'destructive',
        onPress: async () => {
          await logout();
          router.replace('/(auth)/login');
        },
      },
    ]);
  };

  return (
    <View className="flex-1 bg-surface-900 p-4 pt-12">
      <Text className="text-xs text-white/40 uppercase tracking-widest font-mono mb-3">Account</Text>
      <View className="bg-white/[0.03] border border-white/[0.06] rounded-lg p-3 mb-4">
        <Text className="text-white/50 text-xs">Role</Text>
        <Text className="text-white text-sm font-mono mt-1">{role}</Text>
      </View>
      <Pressable
        onPress={onLogout}
        className="bg-red-500/10 border border-red-500/30 rounded-md py-3 items-center"
      >
        <Text className="text-red-400 text-sm font-semibold">Sign out</Text>
      </Pressable>
    </View>
  );
}
