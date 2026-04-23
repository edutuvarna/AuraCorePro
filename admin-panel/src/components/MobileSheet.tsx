'use client';

import { ReactNode, useEffect } from 'react';

/**
 * Bottom-sheet primitive for mobile-first overlays (Phase 6.10 W2.T11).
 *
 * Slides up from the bottom of the viewport with a tap-to-dismiss backdrop.
 * Escape key closes. `aria-modal` + `role="dialog"` for screen readers.
 *
 * No consumers in Task 11 — Wave 3 Task 15 wires the first migration target
 * (likely the row-detail drawer in mobile DataTable mode). Created here so
 * the lift commit owns every primitive in one place.
 */
export interface MobileSheetProps {
    open: boolean;
    onClose: () => void;
    title?: string;
    children: ReactNode;
    /** Maximum height as a vh percentage. Defaults to 85vh so the backdrop is still tappable above. */
    maxHeightVh?: number;
}

export function MobileSheet({ open, onClose, title, children, maxHeightVh = 85 }: MobileSheetProps) {
    useEffect(() => {
        if (!open) return;
        const onKey = (e: KeyboardEvent) => {
            if (e.key === 'Escape') onClose();
        };
        document.addEventListener('keydown', onKey);
        return () => document.removeEventListener('keydown', onKey);
    }, [open, onClose]);

    if (!open) return null;

    return (
        <div
            className="fixed inset-0 z-50 flex flex-col justify-end bg-black/60"
            onClick={onClose}
            role="dialog"
            aria-modal="true"
            aria-labelledby={title ? 'mobile-sheet-title' : undefined}
        >
            <div
                className="bg-zinc-900 border-t border-zinc-700 rounded-t-2xl shadow-xl overflow-y-auto animate-slide-up"
                style={{ maxHeight: `${maxHeightVh}vh` }}
                onClick={(e) => e.stopPropagation()}
            >
                {title && (
                    <div className="flex items-center justify-between px-5 py-4 border-b border-white/5 sticky top-0 bg-zinc-900">
                        <h2 id="mobile-sheet-title" className="text-base font-semibold text-white">{title}</h2>
                        <button
                            type="button"
                            onClick={onClose}
                            className="text-white/50 hover:text-white text-xl leading-none w-8 h-8 flex items-center justify-center"
                            aria-label="Close"
                        >
                            ×
                        </button>
                    </div>
                )}
                <div className="p-5">{children}</div>
            </div>
        </div>
    );
}
