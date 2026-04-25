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

  it('renders KPI cards from getStats response', async () => {
    (api.getStats as jest.Mock).mockResolvedValue({
      activeUsers: 1247,
      activeSubscriptions: 312,
      mrr: 8200,
      recentPaymentsCount: 14,
    });
    const { getByText } = render(wrap(<Dashboard />));
    await waitFor(() => {
      expect(getByText('1,247')).toBeTruthy();
      expect(getByText('312')).toBeTruthy();
    });
  });
});
