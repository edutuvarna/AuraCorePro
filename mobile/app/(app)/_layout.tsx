import { Tabs } from 'expo-router';
import { useRole } from '@/lib/roleContext';

export default function AppLayout() {
  const role = useRole();
  const showPermissions = role === 'superadmin';
  return (
    <Tabs
      screenOptions={{
        headerShown: false,
        tabBarStyle: { backgroundColor: '#08080c', borderTopColor: 'rgba(255,255,255,0.05)' },
        tabBarActiveTintColor: '#22d3ee',
        tabBarInactiveTintColor: 'rgba(255,255,255,0.4)',
      }}
    >
      <Tabs.Screen name="index" options={{ title: 'Dashboard' }} />
      {showPermissions && <Tabs.Screen name="permissions" options={{ title: 'Permissions' }} />}
      <Tabs.Screen name="settings" options={{ title: 'Settings' }} />
    </Tabs>
  );
}
