import * as Notifications from 'expo-notifications';
import { registerForPush, attachForegroundListener, attachTapListener } from '@/lib/notifications';

// Capture handler arg at module load — before any beforeEach clears the mock history.
const handlerCallsAtImport = (Notifications.setNotificationHandler as jest.Mock).mock.calls.slice();

describe('notifications', () => {
  beforeEach(() => jest.clearAllMocks());

  it('setNotificationHandler is configured to suppress system banner in foreground', async () => {
    // The module sets the handler at import time. Use the snapshot captured before any clear.
    expect(handlerCallsAtImport.length).toBeGreaterThan(0);
    const handler = handlerCallsAtImport[0][0];
    const cfg = await handler.handleNotification();
    // expo-notifications 0.32+ uses shouldShowBanner + shouldShowList instead of the
    // deprecated shouldShowAlert. The production handler suppresses both so the
    // custom in-app <InAppNotificationBanner> can render the foreground UI.
    expect(cfg.shouldShowBanner).toBe(false);
    expect(cfg.shouldShowList).toBe(false);
    expect(cfg.shouldPlaySound).toBe(true);
  });

  it('registerForPush calls api.registerFcmToken with the Expo push token', async () => {
    const apiMock = require('@/lib/api');
    apiMock.api.registerFcmToken = jest.fn(async () => true);
    const token = await registerForPush();
    expect(token).toBe('ExpoPushToken[fake]');
    expect(apiMock.api.registerFcmToken).toHaveBeenCalledWith('ExpoPushToken[fake]');
  });

  it('attachForegroundListener registers via expo-notifications', () => {
    const stop = attachForegroundListener(() => {});
    expect(Notifications.addNotificationReceivedListener).toHaveBeenCalled();
    expect(stop).toBeDefined();
  });

  it('attachTapListener registers tap callback', () => {
    const stop = attachTapListener(() => {});
    expect(Notifications.addNotificationResponseReceivedListener).toHaveBeenCalled();
    expect(stop).toBeDefined();
  });
});
