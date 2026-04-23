import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DataTable, DataTableColumn } from '@/components/DataTable';

vi.mock('@/hooks/useMediaQuery', () => ({
    useMediaQuery: () => true,  // Force desktop layout
}));

interface TestRow { id: string; email: string; tier: string; }

describe('DataTable', () => {
    const columns: DataTableColumn<TestRow>[] = [
        { key: 'email', header: 'Email', render: (r) => r.email },
        { key: 'tier', header: 'Tier', render: (r) => r.tier },
    ];

    it('renders empty state when no rows and emptyState provided', () => {
        const empty = <div>Nothing here</div>;
        render(<DataTable columns={columns} rows={[]} rowKey={(r) => r.id} emptyState={empty} />);
        expect(screen.getByText('Nothing here')).toBeInTheDocument();
    });

    it('renders all rows on desktop', () => {
        const rows: TestRow[] = [
            { id: '1', email: 'a@a.com', tier: 'pro' },
            { id: '2', email: 'b@b.com', tier: 'free' },
        ];
        render(<DataTable columns={columns} rows={rows} rowKey={(r) => r.id} />);
        expect(screen.getByText('a@a.com')).toBeInTheDocument();
        expect(screen.getByText('b@b.com')).toBeInTheDocument();
        expect(screen.getByText('pro')).toBeInTheDocument();
        expect(screen.getByText('free')).toBeInTheDocument();
    });

    it('renders column headers on desktop', () => {
        const rows: TestRow[] = [{ id: '1', email: 'a@a.com', tier: 'pro' }];
        render(<DataTable columns={columns} rows={rows} rowKey={(r) => r.id} />);
        expect(screen.getByText('Email')).toBeInTheDocument();
        expect(screen.getByText('Tier')).toBeInTheDocument();
    });

    it('renders footer below rows', () => {
        const rows: TestRow[] = [{ id: '1', email: 'a@a.com', tier: 'pro' }];
        render(
            <DataTable
                columns={columns}
                rows={rows}
                rowKey={(r) => r.id}
                footer={<div>page 1 of 5</div>}
            />
        );
        expect(screen.getByText('page 1 of 5')).toBeInTheDocument();
    });
});
