import { act, render, screen } from '@testing-library/react';
import { getAlerts, getAlertSignals } from '../../services/apiService';
import AlertMonitor from './AlertMonitor';

jest.mock('../../context/AuthContext', () => ({ useAuth: () => ({ user: { id: 1 } }) }));
jest.mock('../../services/apiService', () => ({ getAlerts: jest.fn(), getAlertSignals: jest.fn() }));

// Gerçek bağlantı yerine: testten anlık olay gönderebilir ve yedek polling'i elle çalıştırabiliriz.
let mockRealtimeHandler;
let mockFallbackPoll;
jest.mock('../../context/RealtimeContext', () => ({
  useRealtimeEvent: (eventName, handler) => {
    if (eventName === 'alertTriggered') mockRealtimeHandler = handler;
  },
  usePollingFallback: (poll) => {
    mockFallbackPoll = poll;
  },
}));

const alertWithCount = (signalCount) => ({ data: [{ id: 1, symbol: 'BTCUSDT', signalCount }] });
const signalsResponse = (...ids) => ({
  data: { signals: ids.map((id) => ({ id, priceAtTrigger: 150, triggeredAt: '2026-10-20T10:00:00Z' })) },
});
const event = (id) => ({ id, alertId: 1, symbol: 'BTCUSDT', priceAtTrigger: 150, triggeredAt: '2026-10-20T10:00:00Z' });
const toasts = () => screen.queryAllByText(/BTCUSDT sinyal/);

beforeEach(() => {
  getAlerts.mockReset();
  getAlertSignals.mockReset();
  // İlk yükleme: alarmın henüz sinyali yok.
  getAlerts.mockResolvedValue(alertWithCount(0));
});

test('anlık gelen sinyal bir kez gösterilir, aynı sinyal polling ile gelince tekrar gösterilmez', async () => {
  render(<AlertMonitor />);
  await act(async () => {});

  act(() => mockRealtimeHandler(event(5)));
  expect(toasts()).toHaveLength(1);

  getAlerts.mockResolvedValue(alertWithCount(2));
  getAlertSignals.mockResolvedValue(signalsResponse(5));
  await act(async () => { await mockFallbackPoll(); });

  expect(toasts()).toHaveLength(1);
});

test('polling ile önce gelen sinyal, sonra SignalR ile gelirse tekrar gösterilmez', async () => {
  render(<AlertMonitor />);
  await act(async () => {});

  getAlerts.mockResolvedValue(alertWithCount(1));
  getAlertSignals.mockResolvedValue(signalsResponse(6));
  await act(async () => { await mockFallbackPoll(); });
  expect(toasts()).toHaveLength(1);

  act(() => mockRealtimeHandler(event(6)));

  expect(toasts()).toHaveLength(1);
});

test('farklı sinyaller ayrı ayrı gösterilir', async () => {
  render(<AlertMonitor />);
  await act(async () => {});

  act(() => mockRealtimeHandler(event(7)));
  act(() => mockRealtimeHandler(event(8)));

  expect(toasts()).toHaveLength(2);
});
