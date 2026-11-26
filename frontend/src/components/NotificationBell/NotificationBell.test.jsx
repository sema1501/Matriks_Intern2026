import { act, fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LanguageProvider } from '../../context/LanguageContext';
import NotificationBell from './NotificationBell';
import UserMenu from '../UserMenu/UserMenu';
import { getBots } from '../../services/apiService';

const mockLogout = jest.fn();
jest.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ user: { id: 7, username: 'sefa', roles: ['User'] }, logoutUser: mockLogout }),
}));
jest.mock('../../services/apiService', () => ({
  getBots: jest.fn(),
}));
const mockHandlers = {};
jest.mock('../../context/RealtimeContext', () => ({
  useRealtimeEvent: (eventName, handler) => {
    mockHandlers[eventName] = handler;
  },
}));

function renderWith(ui) {
  return render(
    <LanguageProvider>
      <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>{ui}</MemoryRouter>
    </LanguageProvider>
  );
}

beforeEach(() => {
  localStorage.clear();
  // CRA her testten önce mock'ları sıfırlar, dönüş değeri burada verilir.
  getBots.mockResolvedValue({ data: [{ id: 3, symbol: 'ETHUSDT' }] });
});

test('gelen alarm ve bot sinyali zilde okunmamış olarak görünür', async () => {
  renderWith(<NotificationBell />);
  await act(async () => {});

  act(() => mockHandlers.alertTriggered({ id: 1, symbol: 'BTCUSDT', priceAtTrigger: 100, triggeredAt: '2026-11-23T10:00:00Z' }));
  act(() => mockHandlers.botSignal({ id: 9, botId: 3, signalType: 1, priceAtSignal: 2000, createdAt: '2026-11-23T10:01:00Z' }));
  // Aynı olay ikinci kez gelirse tekrar eklenmez.
  act(() => mockHandlers.botSignal({ id: 9, botId: 3, signalType: 1, priceAtSignal: 2000, createdAt: '2026-11-23T10:01:00Z' }));

  const button = screen.getByRole('button', { name: 'Bildirimler, 2 okunmamış' });
  fireEvent.click(button);
  expect(screen.getByText(/BTCUSDT fiyat alarmı tetiklendi/)).toBeInTheDocument();
  expect(screen.getByText(/ETHUSDT botu SAT sinyali verdi/)).toBeInTheDocument();

  fireEvent.click(screen.getByRole('button', { name: 'Tümünü okundu say' }));
  expect(screen.getByRole('button', { name: 'Bildirimler' })).toBeInTheDocument();
  expect(JSON.parse(localStorage.getItem('crypto-tracker-notifications-7'))).toHaveLength(2);
});

test('kullanıcı menüsü Profilim ve Çıkış Yap içerir, Escape ile kapanır', () => {
  renderWith(<UserMenu />);
  const button = screen.getByRole('button', { name: 'Kullanıcı menüsü: sefa' });
  fireEvent.click(button);
  expect(screen.getByRole('menuitem', { name: 'Profilim' })).toHaveAttribute('href', '/profile');

  fireEvent.keyDown(document, { key: 'Escape' });
  expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  expect(button).toHaveFocus();

  fireEvent.click(button);
  fireEvent.click(screen.getByRole('menuitem', { name: 'Çıkış Yap' }));
  expect(mockLogout).toHaveBeenCalled();
});
