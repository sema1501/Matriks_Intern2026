import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { LanguageProvider } from '../../context/LanguageContext';
import CoinSearch from './CoinSearch';

function renderSearch() {
  return render(
    <LanguageProvider>
      <MemoryRouter future={{ v7_startTransition: true, v7_relativeSplatPath: true }}>
        <CoinSearch />
      </MemoryRouter>
    </LanguageProvider>
  );
}

beforeEach(() => localStorage.clear());

test('seçilen coin son aramalara eklenir ve temizlenebilir', () => {
  renderSearch();
  const search = screen.getByRole('combobox', { name: 'Coin ara' });

  fireEvent.change(search, { target: { value: 'sol' } });
  fireEvent.click(screen.getByRole('option', { name: /SOL/ }));
  expect(JSON.parse(localStorage.getItem('crypto-tracker-recent-coins'))).toEqual(['SOLUSDT']);

  fireEvent.focus(search);
  expect(screen.getByText('Son aramalar')).toBeInTheDocument();
  expect(screen.getAllByRole('option')).toHaveLength(1);

  fireEvent.click(screen.getByRole('button', { name: 'Temizle' }));
  expect(screen.queryByText('Son aramalar')).not.toBeInTheDocument();
  expect(localStorage.getItem('crypto-tracker-recent-coins')).toBeNull();
});
