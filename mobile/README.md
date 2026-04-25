# AuraCore Admin Mobile (Phase 6.14)

Android-only sideload companion app for `admin.auracore.pro`.

## Stack

Expo SDK 54+, React Native 0.81+, Expo Router 6, NativeWind v4, TanStack Query v5,
expo-notifications (FCM), expo-local-authentication, expo-secure-store.

## Local development

```bash
cd mobile
npm install
npx expo start
# scan QR with Expo Go (limited — push notifications need a dev client build)
```

For full feature testing (push, biometric), use a dev client:

```bash
eas build --profile preview --platform android
# install resulting APK on a real Android device
```

## Building for distribution

```bash
eas build --profile preview --platform android  # signed APK download URL
# share URL with admin team via secure channel; users install manually
```

After install, "Install from unknown sources" must be enabled once on the device.

## Environment

Backend URL is read from `expo-constants` (`extra.apiUrl`) or `EXPO_PUBLIC_API_URL`.
Default: `https://api.auracore.pro`. Override in `app.json` `extra` or via env at build time.

FCM service account JSON lives on the BACKEND (`/etc/auracore-api.env` `FCM_SERVICE_ACCOUNT_JSON`).
The mobile app does NOT carry FCM credentials — it just registers its Expo push token
with the backend; the backend sends pushes via FCM HTTP v1.

## Test

```bash
npm test
```

Jest + jest-expo + @testing-library/react-native. Tests in `__tests__/`.

## Phase 6.14 scope

- Auth (email + password + 2FA + biometric unlock)
- Dashboard (KPI cards, pull-to-refresh)
- Permission Requests (list + approve/deny)
- FCM push (foreground custom banner via `setNotificationHandler` override; background system banner)

Carry-forward → 6.15: incident-response pack (Users list, ban, force-logout), iOS port,
Play Store Internal Testing migration, OTA updates via `eas update`.
