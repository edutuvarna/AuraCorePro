import * as Notifications from 'expo-notifications';
import Constants from 'expo-constants';
import { api } from './api';
import { queryClient } from './queryClient';

let registeredToken: string | null = null;

// Foreground custom-banner mode (Option C from Phase 6.14 brainstorm).
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowBanner: false,
    shouldShowList: false,
    shouldPlaySound: true,
    shouldSetBadge: false,
  }),
});

export async function registerForPush(): Promise<string | null> {
  let settings = await Notifications.getPermissionsAsync();
  if (settings.status !== 'granted') {
    settings = await Notifications.requestPermissionsAsync();
    if (settings.status !== 'granted') return null;
  }
  const projectId = (Constants.expoConfig?.extra as any)?.eas?.projectId
    ?? (Constants as any).easConfig?.projectId;
  const tokenObj = await Notifications.getExpoPushTokenAsync(projectId ? { projectId } : undefined);
  registeredToken = tokenObj.data;
  await api.registerFcmToken(tokenObj.data);
  return tokenObj.data;
}

export async function unregisterPush() {
  if (registeredToken) {
    await api.unregisterFcmToken(registeredToken);
    registeredToken = null;
  }
}

type IncomingHandler = (data: Record<string, unknown>) => void;

export function attachForegroundListener(onIncoming: IncomingHandler) {
  return Notifications.addNotificationReceivedListener(notification => {
    const data = notification.request.content.data ?? {};
    if (data.type === 'permission-request') {
      queryClient.invalidateQueries({ queryKey: ['permissionRequests'] });
    }
    onIncoming(data);
  });
}

export function attachTapListener(onTap: (data: Record<string, unknown>) => void) {
  return Notifications.addNotificationResponseReceivedListener(response => {
    onTap(response.notification.request.content.data ?? {});
  });
}
