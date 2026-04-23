'use client';

import { useEffect } from 'react';
import { startConnection, on, off } from '@/lib/signalr';

/**
 * React lifecycle wrapper around the signalr client (Phase 6.10 W2.T12).
 * Subscribes to named events for the lifetime of the component, unsubscribes on unmount.
 * The connection itself is shared across all consumers (managed by signalr.ts singleton).
 *
 * SIGNALR_ENABLED is true as of Phase 6.10 W4.T21 (backend AdminHub ships in W4.T22).
 * Hook works regardless of the flag — signalr.ts no-ops when disabled.
 */
export interface SignalREvents {
    UserRegistered?: (payload: any) => void;
    UserLogin?: (payload: any) => void;
    Payment?: (payload: any) => void;
    CrashReport?: (payload: any) => void;
    Telemetry?: (payload: any) => void;
    AdminCount?: (payload: any) => void;
}

export function useSignalR(events: SignalREvents) {
    useEffect(() => {
        startConnection();
        const handlers: [string, Function][] = [];
        for (const [name, fn] of Object.entries(events)) {
            if (fn) {
                on(name, fn);
                handlers.push([name, fn]);
            }
        }
        return () => {
            for (const [name, fn] of handlers) off(name, fn);
        };
        // events object identity change re-subscribes — caller's responsibility to memoize
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);
}
