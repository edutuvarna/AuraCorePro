'use client';

/**
 * ActivityFeedProvider — hoists the Live Activity feed state + SignalR
 * subscriptions above the page-switcher so navigating between tabs no longer
 * unmounts the feed and discards events that arrived while the user was away
 * (Phase 6.11 Bug #6 fix).
 *
 * Previously `DashboardPage` owned the `activities` array with `useState([])`
 * and wired SignalR via `useSignalR(...)`. When a sibling tab was selected,
 * `ActivePage` unmounted DashboardPage, the state was garbage-collected and
 * the hook cleanup ran `off()` on every handler. The next time the user came
 * back to Dashboard a fresh empty array mounted.
 *
 * By lifting the state into a context that lives inside `AdminPanelInner`
 * (above `<main>`, which never unmounts while the admin panel is open) the
 * subscriptions persist for the lifetime of the session and the feed keeps
 * its sliding 50-entry window across tab switches.
 */

import { createContext, useContext, useEffect, useState, useCallback, useMemo } from 'react';
import type { ReactNode } from 'react';
import { startConnection, on, off } from '@/lib/signalr';

export interface ActivityEvent {
    id: number;
    type: string;
    message: string;
    time: Date;
    color: string;
}

export type SignalRStatus = 'connected' | 'connecting' | 'disconnected';

interface ActivityFeedContextValue {
    activities: ActivityEvent[];
    signalrStatus: SignalRStatus;
}

const ActivityFeedContext = createContext<ActivityFeedContextValue>({
    activities: [],
    signalrStatus: 'connecting',
});

const MAX_ENTRIES = 50;

export function ActivityFeedProvider({ children }: { children: ReactNode }) {
    const [activities, setActivities] = useState<ActivityEvent[]>([]);
    const [signalrStatus, setSignalrStatus] = useState<SignalRStatus>('connecting');

    const addActivity = useCallback((type: string, message: string, color: string) => {
        setActivities(prev => {
            const nextId = (prev[0]?.id ?? 0) + 1;
            return [{ id: nextId, type, message, time: new Date(), color }, ...prev].slice(0, MAX_ENTRIES);
        });
    }, []);

    useEffect(() => {
        // Handlers — defined inside effect so cleanup can off() the exact references.
        const onUserRegistered = (d: any) => addActivity('register', `New user: ${d?.email ?? 'unknown'}`, 'text-aura-green');
        const onUserLogin = (d: any) => addActivity(
            'login',
            `${d?.success ? 'Login' : 'Failed login'}: ${d?.email ?? 'unknown'}`,
            d?.success ? 'text-aura-blue' : 'text-aura-amber'
        );
        const onPayment = (d: any) => addActivity('payment', `Payment $${d?.amount ?? 0} from ${d?.email ?? 'unknown'}`, 'text-accent');
        const onCrashReport = (d: any) => addActivity('crash', `Crash: ${d?.type ?? 'unknown'} (v${d?.version ?? '?'})`, 'text-aura-red');
        const onTelemetry = (d: any) => addActivity('telemetry', `Telemetry batch: ${d?.count ?? 0} events`, 'text-aura-purple');
        const onAdminCount = () => { /* reserved for header online count */ };

        on('UserRegistered', onUserRegistered);
        on('UserLogin', onUserLogin);
        on('Payment', onPayment);
        on('CrashReport', onCrashReport);
        on('Telemetry', onTelemetry);
        on('AdminCount', onAdminCount);

        startConnection();
        setSignalrStatus('connected');

        return () => {
            off('UserRegistered', onUserRegistered);
            off('UserLogin', onUserLogin);
            off('Payment', onPayment);
            off('CrashReport', onCrashReport);
            off('Telemetry', onTelemetry);
            off('AdminCount', onAdminCount);
            // The SignalR connection itself is owned by app/page.tsx
            // (start on login, stop on logout) — we only manage our handlers.
        };
    }, [addActivity]);

    const value = useMemo(() => ({ activities, signalrStatus }), [activities, signalrStatus]);

    return (
        <ActivityFeedContext.Provider value={value}>
            {children}
        </ActivityFeedContext.Provider>
    );
}

export function useActivityFeed() {
    return useContext(ActivityFeedContext);
}
