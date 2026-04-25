import { render, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import Dashboard from '../../app/(app)/index';
import { api } from '@/lib/api';

jest.mock('@/lib/api', () => ({
  api: { getStats: jest.fn() },
}));

const wrap = (children: React.ReactNode) => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
};

describe('Dashboard', () => {
  beforeEach(() => jest.clearAllMocks());

  it('renders KPI cards from getStats response (real backend shape)', async () => {
    // Pin against AdminDashboardController.GetStats actual response shape —
    // totalUsers / proUsers / enterpriseUsers / monthlyRevenue / pendingCryptoPayments.
    (api.getStats as jest.Mock).mockResolvedValue({
      totalUsers: 1247,
      proUsers: 250,
      enterpriseUsers: 62,
      freeUsers: 935,
      totalRevenue: 91400,
      monthlyRevenue: 8200,
      pendingCryptoPayments: 14,
    });
    const { getByText } = render(wrap(<Dashboard />));
    await waitFor(() => {
      expect(getByText('1,247')).toBeTruthy();   // totalUsers
      expect(getByText('312')).toBeTruthy();      // proUsers + enterpriseUsers = 250 + 62
      expect(getByText('14')).toBeTruthy();       // pendingCryptoPayments
    });
  });
});
