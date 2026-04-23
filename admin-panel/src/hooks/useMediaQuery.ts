'use client';

import { useEffect, useState } from 'react';

/**
 * SSR-safe media query hook (Phase 6.10 W2.T11/T12 — created here because DataTable consumes it).
 * Returns true if the viewport currently matches the given CSS media query.
 *
 * Initial render returns false (SSR-safe); the first useEffect tick syncs to actual matchMedia.
 * Subscribes to "change" so updates flow through on viewport resize.
 */
export function useMediaQuery(query: string): boolean {
    const [matches, setMatches] = useState(false);

    useEffect(() => {
        if (typeof window === 'undefined') return;
        const mq = window.matchMedia(query);
        const update = () => setMatches(mq.matches);
        update();
        mq.addEventListener('change', update);
        return () => mq.removeEventListener('change', update);
    }, [query]);

    return matches;
}
