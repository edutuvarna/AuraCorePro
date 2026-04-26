import '../global.css';
import { useEffect, useState, ReactNode } from 'react';
import { Slot, useRouter } from 'expo-router';
import { QueryClientProvider } from '@tanstack/react-query';
import { queryClient } from '@/lib/queryClient';
import { AuthProvider, useAuth } from '@/lib/authContext';
import { RoleProvider } from '@/lib/roleContext';
import { attachForegroundListener, attachTapListener } from '@/lib/notifications';
import { InAppNotificationBanner } from '@/components/InAppNotificationBanner';

// Bridges read useAuth() and supply downstream props. They must live INSIDE
// AuthProvider, hence the wrapper components below.

function RoleBridge({ children }: { children: ReactNode }) {
  const { auth } = useAuth();
  return <RoleProvider value={auth?.role ?? 'admin'}>{children}</RoleProvider>;
}

function NotificationsBridge({ children }: { children: ReactNode }) {
  const { auth } = useAuth();
  const router = useRouter();
  const [banner, setBanner] = useState<{ title: string; body: string; data: Record<string, unknown> } | null>(null);

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

  return (
    <>
      {children}
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
    </>
  );
}

export default function RootLayout() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <RoleBridge>
          <NotificationsBridge>
            <Slot />
          </NotificationsBridge>
        </RoleBridge>
      </AuthProvider>
    </QueryClientProvider>
  );
}
