'use client';

import { useEffect, useRef } from 'react';

export interface ConfirmDialogProps {
    open: boolean;
    title: string;
    message: string;
    confirmLabel?: string;
    cancelLabel?: string;
    destructive?: boolean;
    onConfirm: () => void | Promise<void>;
    onCancel: () => void;
}

/**
 * Shared destructive-action confirmation dialog (Phase 6.9 CTP-4).
 *
 * - Escape key cancels.
 * - Enter triggers confirm.
 * - Clicking backdrop cancels.
 * - Auto-focus on confirm button.
 * - destructive=true (default) gives the confirm button red styling.
 * - min-h-[44px] on both buttons handles T1.6 tap-target accessibility.
 */
export function ConfirmDialog({
    open,
    title,
    message,
    confirmLabel = 'Confirm',
    cancelLabel = 'Cancel',
    destructive = true,
    onConfirm,
    onCancel,
}: ConfirmDialogProps) {
    const confirmRef = useRef<HTMLButtonElement>(null);

    useEffect(() => {
        if (!open) return;
        const onKey = (e: KeyboardEvent) => {
            if (e.key === 'Escape') onCancel();
            if (e.key === 'Enter') confirmRef.current?.click();
        };
        document.addEventListener('keydown', onKey);
        setTimeout(() => confirmRef.current?.focus(), 0);
        return () => document.removeEventListener('keydown', onKey);
    }, [open, onCancel]);

    if (!open) return null;

    return (
        <div
            className="fixed inset-0 z-50 flex items-center justify-center bg-black/60"
            onClick={onCancel}
            role="dialog"
            aria-modal="true"
            aria-labelledby="confirm-dialog-title"
        >
            <div
                className="bg-zinc-900 border border-zinc-700 rounded-lg shadow-xl p-6 max-w-md w-full mx-4"
                onClick={(e) => e.stopPropagation()}
            >
                <h2 id="confirm-dialog-title" className="text-lg font-semibold text-white mb-2">
                    {title}
                </h2>
                <p className="text-zinc-300 mb-6 whitespace-pre-line">{message}</p>
                <div className="flex justify-end gap-3">
                    <button
                        type="button"
                        onClick={onCancel}
                        className="px-4 py-2 rounded bg-zinc-700 hover:bg-zinc-600 text-white min-h-[44px]"
                    >
                        {cancelLabel}
                    </button>
                    <button
                        ref={confirmRef}
                        type="button"
                        onClick={() => void onConfirm()}
                        className={`px-4 py-2 rounded text-white min-h-[44px] ${
                            destructive
                                ? 'bg-red-600 hover:bg-red-700'
                                : 'bg-blue-600 hover:bg-blue-700'
                        }`}
                    >
                        {confirmLabel}
                    </button>
                </div>
            </div>
        </div>
    );
}
