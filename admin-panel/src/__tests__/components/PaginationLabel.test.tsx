import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PaginationLabel } from '@/components/PaginationLabel';

describe('PaginationLabel', () => {
    it('shows "No results" when total is 0', () => {
        render(<PaginationLabel page={1} pageSize={50} total={0} />);
        expect(screen.getByText('No results')).toBeInTheDocument();
    });

    it('shows correct range for first page', () => {
        render(<PaginationLabel page={1} pageSize={50} total={123} />);
        // Format: "Showing 1–50 of 123" (en-dash U+2013)
        expect(screen.getByText('Showing 1\u201350 of 123')).toBeInTheDocument();
    });

    it('shows correct range for middle page', () => {
        render(<PaginationLabel page={2} pageSize={50} total={123} />);
        expect(screen.getByText('Showing 51\u2013100 of 123')).toBeInTheDocument();
    });

    it('shows correct range for last (partial) page', () => {
        render(<PaginationLabel page={3} pageSize={50} total={123} />);
        expect(screen.getByText('Showing 101\u2013123 of 123')).toBeInTheDocument();
    });

    it('caps end at total when total < page * pageSize', () => {
        render(<PaginationLabel page={1} pageSize={50} total={10} />);
        expect(screen.getByText('Showing 1\u201310 of 10')).toBeInTheDocument();
    });
});
