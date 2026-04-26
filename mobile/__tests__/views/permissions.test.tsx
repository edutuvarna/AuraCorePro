import { render, fireEvent, waitFor } from '@testing-library/react-native';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import Permissions from '../../app/(app)/permissions';
import { api } from '@/lib/api';

jest.mock('@/lib/api', () => ({
  api: {
    getPermissionRequests: jest.fn(),
    approvePermissionRequest: jest.fn(),
    denyPermissionRequest: jest.fn(),
  },
}));

const wrap = (children: React.ReactNode) => {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
};

describe('Permissions screen', () => {
  beforeEach(() => jest.clearAllMocks());

  it('renders requests from getPermissionRequests', async () => {
    (api.getPermissionRequests as jest.Mock).mockResolvedValue({
      items: [{
        id: 'req-1',
        adminEmail: 'new-admin@x.test',
        permissionKey: 'tab:audit',
        reason: 'Need audit access for incident review',
        requestedAt: '2026-04-25T12:00:00Z',
      }],
    });
    const { getByText } = render(wrap(<Permissions />));
    await waitFor(() => {
      expect(getByText('new-admin@x.test')).toBeTruthy();
      expect(getByText('tab:audit')).toBeTruthy();
    });
  });

  it('approves a request when Approve is pressed', async () => {
    (api.getPermissionRequests as jest.Mock).mockResolvedValue({
      items: [{ id: 'req-1', adminEmail: 'a@x', permissionKey: 'tab:audit', reason: '', requestedAt: '2026-04-25T12:00:00Z' }],
    });
    (api.approvePermissionRequest as jest.Mock).mockResolvedValue({ ok: true, status: 200 });

    const { getByText } = render(wrap(<Permissions />));
    await waitFor(() => getByText('Approve'));
    fireEvent.press(getByText('Approve'));
    await waitFor(() => {
      expect(api.approvePermissionRequest).toHaveBeenCalledWith('req-1', undefined);
    });
  });
});
