import { render, fireEvent, waitFor } from '@testing-library/react-native';
import LoginScreen from '../../app/(auth)/login';
import { api } from '@/lib/api';

jest.mock('@/lib/api', () => ({
  api: {
    login: jest.fn(),
    registerFcmToken: jest.fn(async () => true),
  },
}));

jest.mock('@/lib/notifications', () => ({
  registerForPush: jest.fn(async () => 'ExpoPushToken[fake]'),
  attachForegroundListener: jest.fn(() => ({ remove: jest.fn() })),
  attachTapListener: jest.fn(() => ({ remove: jest.fn() })),
}));

describe('LoginScreen', () => {
  beforeEach(() => jest.clearAllMocks());

  it('shows email + password fields and Sign In button on initial render', () => {
    const { getByPlaceholderText, getByText } = render(<LoginScreen />);
    expect(getByPlaceholderText(/email/i)).toBeTruthy();
    expect(getByPlaceholderText(/password/i)).toBeTruthy();
    expect(getByText(/sign in/i)).toBeTruthy();
  });

  it('reveals 2FA input when backend returns requires2fa', async () => {
    (api.login as jest.Mock).mockResolvedValueOnce({
      ok: true,
      data: { requires2fa: true },
    });

    const { getByPlaceholderText, getByText } = render(<LoginScreen />);
    fireEvent.changeText(getByPlaceholderText(/email/i), 'admin@x.test');
    fireEvent.changeText(getByPlaceholderText(/password/i), 'pass1234567');
    fireEvent.press(getByText(/sign in/i));

    await waitFor(() => {
      expect(getByPlaceholderText(/2fa code|totp|6-digit/i)).toBeTruthy();
    });
  });

  it('calls api.login with email + password on submit', async () => {
    (api.login as jest.Mock).mockResolvedValueOnce({ ok: true, data: { accessToken: 'jwt', refreshToken: 'r', user: { id: '1', email: 'x', role: 'admin' } } });

    const { getByPlaceholderText, getByText } = render(<LoginScreen />);
    fireEvent.changeText(getByPlaceholderText(/email/i), 'x@y.test');
    fireEvent.changeText(getByPlaceholderText(/password/i), 'p1234567890');
    fireEvent.press(getByText(/sign in/i));

    await waitFor(() => {
      expect(api.login).toHaveBeenCalledWith('x@y.test', 'p1234567890', undefined);
    });
  });
});
