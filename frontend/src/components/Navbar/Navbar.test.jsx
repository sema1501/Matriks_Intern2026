import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LanguageProvider, useLanguage } from '../../context/LanguageContext';
import Navbar from './Navbar';

let mockUser = null;
jest.mock('../../context/AuthContext', () => ({
  useAuth: () => ({ user: mockUser, logoutUser: jest.fn() }),
}));
// Sağdaki grup bu testin konusu değil.
jest.mock('../ConnectionStatus/ConnectionStatus', () => () => null);
jest.mock('../RealtimeStatus/RealtimeStatus', () => () => null);
jest.mock('../ThemeToggle/ThemeToggle', () => () => null);
jest.mock('../CurrencyToggle/CurrencyToggle', () => () => null);
jest.mock('../NotificationBell/NotificationBell', () => () => null);
jest.mock('../UserMenu/UserMenu', () => () => null);

let switchLang;
function LangSwitch() {
  switchLang = useLanguage().setLang;
  return null;
}

function renderNavbar(path = '/') {
  return render(
    <LanguageProvider>
      <MemoryRouter initialEntries={[path]} future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <LangSwitch />
        <Navbar />
      </MemoryRouter>
    </LanguageProvider>
  );
}

const menu = () => screen.getByRole('complementary', { hidden: true });
const openMenu = () => fireEvent.click(screen.getByRole('button', { name: 'Menüyü aç' }));

beforeEach(() => {
  localStorage.clear();
  mockUser = null;
});

test('hamburger menüyü açar, Escape kapatır ve odak butona döner', () => {
  renderNavbar();
  const button = screen.getByRole('button', { name: 'Menüyü aç' });
  expect(button).toHaveAttribute('aria-expanded', 'false');

  openMenu();
  expect(button).toHaveAttribute('aria-expanded', 'true');
  expect(menu()).toHaveClass('is-open');

  fireEvent.keyDown(document, { key: 'Escape' });
  expect(button).toHaveAttribute('aria-expanded', 'false');
  expect(button).toHaveFocus();
});

test('dışarı tıklayınca menü kapanır', () => {
  const { container } = renderNavbar();
  openMenu();
  fireEvent.click(container.querySelector('.side-menu__overlay'));
  expect(menu()).not.toHaveClass('is-open');
});

test('misafir yalnızca Piyasa grubunu ve giriş linklerini görür', () => {
  renderNavbar();
  openMenu();
  const panel = within(menu());
  expect(panel.getByText('Piyasa')).toBeInTheDocument();
  expect(panel.queryByText('Hesabım')).not.toBeInTheDocument();
  expect(panel.queryByText('Yönetim')).not.toBeInTheDocument();
  expect(panel.getByText('Giriş Yap')).toBeInTheDocument();
});

test('admin üç grubu görür, açık sayfa vurgulanır', () => {
  mockUser = { username: 'admin', roles: ['Admin', 'User'] };
  renderNavbar('/admin/bots');
  openMenu();
  const panel = within(menu());
  expect(panel.getByText('Hesabım')).toBeInTheDocument();
  expect(panel.getByText('Yönetim')).toBeInTheDocument();
  expect(panel.getByRole('link', { name: 'Bot ve Portföy İzleme' })).toHaveClass('is-active');
  expect(panel.getByRole('link', { name: 'Genel Bakış' })).not.toHaveClass('is-active');
  expect(panel.getByText('Çıkış Yap')).toBeInTheDocument();
});

test('admin olmayan kullanıcı Yönetim grubunu görmez', () => {
  mockUser = { username: 'sefa', roles: ['User'] };
  renderNavbar();
  openMenu();
  expect(within(menu()).queryByText('Yönetim')).not.toBeInTheDocument();
});

test('menü adları İngilizcede de çevrilir', () => {
  mockUser = { username: 'admin', roles: ['Admin'] };
  renderNavbar();
  act(() => switchLang('en'));
  fireEvent.click(screen.getByRole('button', { name: 'Open menu' }));
  const panel = within(menu());
  ['Market', 'My Account', 'Admin', 'Overview', 'Bot & Portfolio Monitoring', 'My Profile', 'Log Out'].forEach((label) => {
    expect(panel.getByText(label)).toBeInTheDocument();
  });
  expect(document.documentElement.lang).toBe('en');
});

test('arama çubuğu coin listesini filtreler', () => {
  renderNavbar();
  const input = screen.getByRole('combobox', { name: 'Coin ara' });
  fireEvent.change(input, { target: { value: 'eth' } });
  const options = screen.getAllByRole('option');
  expect(options.length).toBeGreaterThan(0);
  expect(options[0]).toHaveTextContent('ETH');
});
