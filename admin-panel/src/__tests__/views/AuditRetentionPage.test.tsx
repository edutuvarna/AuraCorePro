import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AuditRetentionPage } from '@/views/AuditRetentionPage';

vi.mock('@/lib/api', () => ({
  api: {
    getAuditRetentionPolicy: vi.fn().mockResolvedValue({
      retentionDays: 180,
      lastRunAt: '2026-04-28T03:00:00.000+00:00',
      lastRunDeletedRows: 1234,
      totalRows: 50000,
      oldestAt: '2025-01-01T00:00:00.000+00:00',
    }),
    setAuditRetentionPolicy: vi.fn().mockResolvedValue({ ok: true, data: { retentionDays: 90 } }),
    runAuditRetentionNow: vi.fn().mockResolvedValue({ ok: true, data: { deleted: 12 } }),
  },
}));

describe('AuditRetentionPage', () => {
  it('renders current policy + KPIs', async () => {
    render(<AuditRetentionPage />);
    await waitFor(() => screen.getByDisplayValue('180'));
    // Expected to find totalRows and lastRunDeletedRows formatted with locale separators
    expect(screen.queryByText(/50,?000/)).toBeTruthy();
    expect(screen.queryByText(/1,?234/)).toBeTruthy();
  });

  it('saving a new retention value calls api.setAuditRetentionPolicy', async () => {
    const { api } = await import('@/lib/api');
    render(<AuditRetentionPage />);
    await waitFor(() => screen.getByDisplayValue('180'));
    const input = screen.getByDisplayValue('180') as HTMLInputElement;
    fireEvent.change(input, { target: { value: '90' } });
    fireEvent.click(screen.getByText(/^Save$/i));
    await waitFor(() => expect(api.setAuditRetentionPolicy).toHaveBeenCalledWith(90));
  });

  it('Run Now button calls api.runAuditRetentionNow', async () => {
    const { api } = await import('@/lib/api');
    render(<AuditRetentionPage />);
    await waitFor(() => screen.getByDisplayValue('180'));
    fireEvent.click(screen.getByText(/run cleanup now/i));
    await waitFor(() => expect(api.runAuditRetentionNow).toHaveBeenCalled());
  });
});
