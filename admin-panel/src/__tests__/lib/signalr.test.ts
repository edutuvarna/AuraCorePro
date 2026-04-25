import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock @microsoft/signalr BEFORE importing the module under test.
vi.mock('@microsoft/signalr', () => {
  const states = { Disconnected: 0, Connecting: 1, Connected: 2 };
  const mockBuild = vi.fn();
  class FakeBuilder {
    withUrl() { return this; }
    withAutomaticReconnect() { return this; }
    configureLogging() { return this; }
    build() {
      const conn = {
        state: states.Disconnected,
        start: vi.fn(() => Promise.resolve()),
        stop: vi.fn(() => Promise.resolve()),
        on: vi.fn(),
        off: vi.fn(),
      };
      mockBuild(conn);
      return conn;
    }
  }
  return {
    HubConnectionBuilder: FakeBuilder,
    HubConnectionState: states,
    LogLevel: { Warning: 3 },
    __mockBuild: mockBuild,
  };
});

vi.mock('@/lib/api', () => ({
  getToken: () => 'fake-jwt',
}));

import { startConnection, getConnection, stopConnection } from '@/lib/signalr';
import * as signalRMock from '@microsoft/signalr';

describe('signalr.startConnection — Connecting-state guard', () => {
  beforeEach(() => {
    stopConnection();
    (signalRMock as unknown as { __mockBuild: { mockClear: () => void } }).__mockBuild.mockClear();
  });

  it('does not rebuild the connection when state is Connecting (second concurrent call)', () => {
    startConnection();
    const first = getConnection();
    expect(first).not.toBeNull();
    // Simulate the in-flight Connecting state (start() Promise still pending).
    (first as unknown as { state: number }).state = signalRMock.HubConnectionState.Connecting;

    startConnection(); // second concurrent provider mount
    const second = getConnection();

    expect(second).toBe(first); // same reference, NOT a fresh builder.build()
    const build = (signalRMock as unknown as { __mockBuild: { mock: { calls: unknown[] } } }).__mockBuild;
    expect(build.mock.calls.length).toBe(1);
  });

  it('does not rebuild the connection when state is Connected', () => {
    startConnection();
    const first = getConnection();
    (first as unknown as { state: number }).state = signalRMock.HubConnectionState.Connected;

    startConnection();
    const second = getConnection();

    expect(second).toBe(first);
  });
});
