import { act, render, screen } from '@testing-library/react';
import { useAuth } from './AuthContext';
import { getFreshAccessToken } from '../services/apiService';
import {
  RESTART_DELAY_MS,
  RealtimeProvider,
  usePollingFallback,
  useRealtime,
  useRealtimeEvent,
} from './RealtimeContext';

jest.mock('./AuthContext', () => ({ useAuth: jest.fn() }));
jest.mock('../services/apiService', () => ({
  API_BASE_URL: 'http://api.test',
  getFreshAccessToken: jest.fn(),
}));

// Gerçek SignalR yerine olayları elle tetikleyebildiğimiz sahte bağlantı.
let mockConnections = [];
let mockStartResults = [];

jest.mock('@microsoft/signalr', () => {
  class FakeConnection {
    constructor(url, options) {
      this.url = url;
      this.options = options;
      this.handlers = {};
      this.start = jest.fn(() => (mockStartResults.length ? mockStartResults.shift() : Promise.resolve()));
      this.stop = jest.fn(() => Promise.resolve());
      mockConnections.push(this);
    }
    on(name, handler) { this.handlers[name] = handler; }
    onreconnecting(handler) { this.reconnecting = handler; }
    onreconnected(handler) { this.reconnected = handler; }
    onclose(handler) { this.closed = handler; }
  }

  class HubConnectionBuilder {
    withUrl(url, options) { this.url = url; this.options = options; return this; }
    withAutomaticReconnect() { this.autoReconnect = true; return this; }
    configureLogging() { return this; }
    build() { return new FakeConnection(this.url, this.options); }
  }

  return { HubConnectionBuilder, LogLevel: { Warning: 3 } };
});

const POLL_MS = 1000;

function Probe({ poll, onAlert }) {
  const { status } = useRealtime();
  usePollingFallback(poll, POLL_MS);
  useRealtimeEvent('alertTriggered', onAlert);
  return <span data-testid="status">{status}</span>;
}

const renderProvider = (props) =>
  render(
    <RealtimeProvider>
      <Probe {...props} />
    </RealtimeProvider>
  );

const status = () => screen.getByTestId('status').textContent;
const lastConnection = () => mockConnections[mockConnections.length - 1];

// Bekleyen promise'lerin (connection.start) çözülmesine izin verir.
const flush = () => act(async () => {});

beforeEach(() => {
  jest.useFakeTimers();
  mockConnections = [];
  mockStartResults = [];
  useAuth.mockReturnValue({ user: { id: 1, username: 'sefa' } });
});

afterEach(() => {
  jest.useRealTimers();
});

test('giriş yapılınca hub\'a güncel token fonksiyonuyla bağlanır ve bağlıyken polling yapılmaz', async () => {
  const poll = jest.fn();
  renderProvider({ poll, onAlert: jest.fn() });
  await flush();

  expect(lastConnection().url).toBe('http://api.test/hubs/notifications');
  expect(lastConnection().options.accessTokenFactory).toBe(getFreshAccessToken);
  expect(status()).toBe('connected');
  // İlk yükleme ile bağlantının kurulması arasında kaçan olay olmasın diye bir kez çekilir.
  expect(poll).toHaveBeenCalledTimes(1);

  act(() => { jest.advanceTimersByTime(POLL_MS * 5); });
  expect(poll).toHaveBeenCalledTimes(1);
});

test('bağlantı koparsa polling başlar, geri gelince bir kez yakalama yapılıp polling durur', async () => {
  const poll = jest.fn();
  renderProvider({ poll, onAlert: jest.fn() });
  await flush();
  poll.mockClear();

  act(() => lastConnection().reconnecting());
  expect(status()).toBe('reconnecting');
  act(() => { jest.advanceTimersByTime(POLL_MS * 3); });
  expect(poll).toHaveBeenCalledTimes(3);

  act(() => lastConnection().reconnected());
  expect(status()).toBe('connected');
  // Kopukken kaçan olaylar için bir kez daha çekilir.
  expect(poll).toHaveBeenCalledTimes(4);

  act(() => { jest.advanceTimersByTime(POLL_MS * 5); });
  expect(poll).toHaveBeenCalledTimes(4);
});

test('backend kapalıysa çevrimdışı görünür, polling çalışır ve bağlantı tekrar denenir', async () => {
  mockStartResults = [Promise.reject(new Error('down'))];
  const poll = jest.fn();
  renderProvider({ poll, onAlert: jest.fn() });
  await flush();

  expect(status()).toBe('offline');
  act(() => { jest.advanceTimersByTime(POLL_MS * 2); });
  expect(poll).toHaveBeenCalledTimes(2);

  // Backend geri geldi: bir sonraki denemede bağlanır, polling durur.
  act(() => { jest.advanceTimersByTime(RESTART_DELAY_MS); });
  await flush();
  expect(lastConnection().start).toHaveBeenCalledTimes(2);
  expect(status()).toBe('connected');

  const callsWhenConnected = poll.mock.calls.length;
  act(() => { jest.advanceTimersByTime(POLL_MS * 5); });
  expect(poll).toHaveBeenCalledTimes(callsWhenConnected);
});

test('otomatik yeniden bağlanma pes ederse çevrimdışı olur ve bağlantı yeniden başlatılır', async () => {
  renderProvider({ poll: jest.fn(), onAlert: jest.fn() });
  await flush();

  act(() => lastConnection().closed(new Error('gone')));
  expect(status()).toBe('offline');

  act(() => { jest.advanceTimersByTime(RESTART_DELAY_MS); });
  await flush();
  expect(lastConnection().start).toHaveBeenCalledTimes(2);
  expect(status()).toBe('connected');
});

test('hub\'dan gelen olay abone olan bileşene iletilir', async () => {
  const onAlert = jest.fn();
  renderProvider({ poll: jest.fn(), onAlert });
  await flush();

  const event = { id: 5, alertId: 1, symbol: 'BTCUSDT', priceAtTrigger: 150 };
  act(() => lastConnection().handlers.alertTriggered(event));

  expect(onAlert).toHaveBeenCalledWith(event);
});

test('çıkış yapılınca bağlantı kapatılır', async () => {
  const poll = jest.fn();
  const { rerender } = renderProvider({ poll, onAlert: jest.fn() });
  await flush();
  const connection = lastConnection();

  useAuth.mockReturnValue({ user: null });
  rerender(
    <RealtimeProvider>
      <Probe poll={poll} onAlert={jest.fn()} />
    </RealtimeProvider>
  );
  await flush();

  expect(connection.stop).toHaveBeenCalled();
  expect(status()).toBe('idle');
  expect(mockConnections).toHaveLength(1);
});
