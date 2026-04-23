import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { MobileSheet } from '@/components/MobileSheet';

describe('MobileSheet', () => {
    it('renders nothing when closed', () => {
        const { container } = render(
            <MobileSheet open={false} onClose={vi.fn()} title="T">
                child
            </MobileSheet>
        );
        expect(container.firstChild).toBeNull();
    });

    it('renders title + content when open', () => {
        render(
            <MobileSheet open={true} onClose={vi.fn()} title="All sections">
                grid here
            </MobileSheet>
        );
        expect(screen.getByText('All sections')).toBeInTheDocument();
        expect(screen.getByText('grid here')).toBeInTheDocument();
    });

    it('renders content without title when title omitted', () => {
        render(
            <MobileSheet open={true} onClose={vi.fn()}>
                untitled body
            </MobileSheet>
        );
        expect(screen.getByText('untitled body')).toBeInTheDocument();
    });

    it('calls onClose when Escape pressed', () => {
        const onClose = vi.fn();
        render(
            <MobileSheet open={true} onClose={onClose} title="T">
                x
            </MobileSheet>
        );
        fireEvent.keyDown(document, { key: 'Escape' });
        expect(onClose).toHaveBeenCalled();
    });

    it('calls onClose when backdrop clicked', () => {
        const onClose = vi.fn();
        render(
            <MobileSheet open={true} onClose={onClose} title="T">
                body
            </MobileSheet>
        );
        // The dialog root element is the backdrop
        const dialog = screen.getByRole('dialog');
        fireEvent.click(dialog);
        expect(onClose).toHaveBeenCalled();
    });

    it('calls onClose when close (×) button clicked', () => {
        const onClose = vi.fn();
        render(
            <MobileSheet open={true} onClose={onClose} title="With title">
                body
            </MobileSheet>
        );
        fireEvent.click(screen.getByLabelText('Close'));
        expect(onClose).toHaveBeenCalled();
    });
});
