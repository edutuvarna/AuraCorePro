import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ConfirmDialog } from '@/components/ConfirmDialog';

describe('ConfirmDialog', () => {
    it('renders nothing when closed', () => {
        const { container } = render(
            <ConfirmDialog
                open={false}
                title="Title"
                message="Msg"
                onConfirm={vi.fn()}
                onCancel={vi.fn()}
            />
        );
        expect(container.firstChild).toBeNull();
    });

    it('renders title + message when open', () => {
        render(
            <ConfirmDialog
                open={true}
                title="Delete user?"
                message="Cannot be undone."
                onConfirm={vi.fn()}
                onCancel={vi.fn()}
            />
        );
        expect(screen.getByText('Delete user?')).toBeInTheDocument();
        expect(screen.getByText('Cannot be undone.')).toBeInTheDocument();
    });

    it('calls onConfirm when confirm button clicked', () => {
        const onConfirm = vi.fn();
        render(
            <ConfirmDialog
                open={true}
                title="t"
                message="m"
                confirmLabel="Yes"
                onConfirm={onConfirm}
                onCancel={vi.fn()}
            />
        );
        fireEvent.click(screen.getByText('Yes'));
        expect(onConfirm).toHaveBeenCalledTimes(1);
    });

    it('calls onCancel when cancel button clicked', () => {
        const onCancel = vi.fn();
        render(
            <ConfirmDialog
                open={true}
                title="t"
                message="m"
                cancelLabel="No"
                onConfirm={vi.fn()}
                onCancel={onCancel}
            />
        );
        fireEvent.click(screen.getByText('No'));
        expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('uses default Confirm/Cancel labels when not provided', () => {
        render(
            <ConfirmDialog
                open={true}
                title="t"
                message="m"
                onConfirm={vi.fn()}
                onCancel={vi.fn()}
            />
        );
        expect(screen.getByText('Confirm')).toBeInTheDocument();
        expect(screen.getByText('Cancel')).toBeInTheDocument();
    });
});
