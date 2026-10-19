import { fireEvent, render, screen } from '@testing-library/react';
import { LanguageProvider } from '../../context/LanguageContext';
import { getPortfolioHistory } from '../../services/apiService';
import PortfolioHistoryChart from './PortfolioHistoryChart';

jest.mock('../../services/apiService', () => ({ getPortfolioHistory: jest.fn() }));

// jsdom'da ResponsiveContainer boyut ölçemediği için grafik hiç çizilmez. Recharts yerine
// sadece kaç nokta geldiğini yazan sade bileşenler kullanılır.
jest.mock('recharts', () => ({
  ResponsiveContainer: ({ children }) => <div>{children}</div>,
  AreaChart: ({ data, children }) => (
    <div data-testid="area-chart" data-points={data.length}>
      {children}
    </div>
  ),
  Area: () => null,
  CartesianGrid: () => null,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

const point = (takenAt, totalValue) => ({ takenAt, totalValue });

const history = (points, changeUsd = null, changePercent = null) => ({
  data: { points, changeUsd, changePercent },
});

const renderChart = () =>
  render(
    <LanguageProvider>
      <PortfolioHistoryChart />
    </LanguageProvider>
  );

beforeEach(() => {
  localStorage.setItem('crypto-tracker-lang', 'tr');
  getPortfolioHistory.mockReset();
  jest.spyOn(console, 'error').mockImplementation(() => {});
});

afterEach(() => {
  console.error.mockRestore();
});

test('veri gelene kadar yükleniyor yazısı görünür', () => {
  getPortfolioHistory.mockReturnValue(new Promise(() => {}));

  renderChart();

  expect(screen.getByText('Geçmiş yükleniyor...')).toBeInTheDocument();
  expect(getPortfolioHistory).toHaveBeenCalledWith('24h');
});

test('istek hata verirse hata mesajı görünür', async () => {
  getPortfolioHistory.mockRejectedValue(new Error('500'));

  renderChart();

  expect(await screen.findByText('Portföy geçmişi yüklenemedi.')).toBeInTheDocument();
  expect(screen.queryByTestId('area-chart')).not.toBeInTheDocument();
});

test('hiç kayıt yoksa boş durum mesajı görünür', async () => {
  getPortfolioHistory.mockResolvedValue(history([]));

  renderChart();

  expect(await screen.findByText(/Geçmiş verisi birikiyor/)).toBeInTheDocument();
  expect(screen.queryByTestId('area-chart')).not.toBeInTheDocument();
});

test('tek kayıt varsa değer ve en az iki kayıt gerektiği yazılır, grafik çizilmez', async () => {
  getPortfolioHistory.mockResolvedValue(history([point('2026-10-19T10:00:00Z', 10250)]));

  renderChart();

  expect(await screen.findByText(/en az iki kayıt gerekiyor/)).toBeInTheDocument();
  expect(screen.getByText('$10,250.00')).toBeInTheDocument();
  expect(screen.queryByTestId('area-chart')).not.toBeInTheDocument();
});

test('birden çok kayıt varsa grafik ve aralıktaki değişim görünür', async () => {
  getPortfolioHistory.mockResolvedValue(
    history(
      [point('2026-10-19T10:00:00Z', 10000), point('2026-10-19T11:00:00Z', 10100), point('2026-10-19T12:00:00Z', 10250)],
      250,
      2.5
    )
  );

  renderChart();

  const chart = await screen.findByTestId('area-chart');
  expect(chart).toHaveAttribute('data-points', '3');
  expect(screen.getByRole('img', { name: 'Portföy toplam değerinin zaman içindeki değişimi' })).toBeInTheDocument();
  expect(screen.getByText(/Seçilen aralıktaki değişim/)).toBeInTheDocument();
  expect(screen.getByText(/\+\$250\.00/)).toBeInTheDocument();
  expect(screen.getByText(/\+2\.50%/)).toBeInTheDocument();
});

test('aralık değişince yeni aralık için istek atılır', async () => {
  getPortfolioHistory.mockResolvedValue(history([]));

  renderChart();
  await screen.findByText(/Geçmiş verisi birikiyor/);
  fireEvent.click(screen.getByRole('button', { name: '7g' }));

  expect(getPortfolioHistory).toHaveBeenLastCalledWith('7d');
  expect(await screen.findByRole('button', { name: '7g', pressed: true })).toBeInTheDocument();
});
