import { fireEvent, render, screen } from '@testing-library/react';
import { LanguageProvider } from '../../context/LanguageContext';
import { runBotBacktest } from '../../services/apiService';
import { getKlinesRange } from '../../services/binanceService';
import BacktestReport from './BacktestReport';

jest.mock('../../services/apiService', () => ({ runBotBacktest: jest.fn() }));
jest.mock('../../services/binanceService', () => ({ getKlinesRange: jest.fn() }));
jest.mock('klinecharts', () => ({ init: jest.fn(), dispose: jest.fn(), registerOverlay: jest.fn() }));

// jsdom'da ResponsiveContainer boyut ölçemediği için grafik çizilmez; kaç nokta geldiğini yazan sade bileşenler.
jest.mock('recharts', () => ({
  ResponsiveContainer: ({ children }) => <div>{children}</div>,
  AreaChart: ({ data, children }) => (
    <div data-testid="equity-chart" data-points={data.length}>
      {children}
    </div>
  ),
  Area: () => null,
  CartesianGrid: () => null,
  ReferenceLine: () => null,
  Tooltip: () => null,
  XAxis: () => null,
  YAxis: () => null,
}));

const summary = {
  totalSignals: 0,
  buySignals: 0,
  sellSignals: 0,
  completedTrades: 0,
  winningTrades: 0,
  losingTrades: 0,
  netProfitLoss: 0,
};

const nullMetrics = {
  maxDrawdownPercent: null,
  winRatePercent: null,
  profitFactor: null,
  averageTradeReturnPercent: null,
  stopLossTrades: 0,
  takeProfitTrades: 0,
  strategyReturnPercent: null,
  buyAndHoldReturnPercent: null,
  buyAndHoldDifferencePercent: null,
  equityCurve: [],
};

const filledMetrics = {
  maxDrawdownPercent: 6,
  winRatePercent: 50,
  profitFactor: 1.5,
  averageTradeReturnPercent: 2,
  stopLossTrades: 1,
  takeProfitTrades: 2,
  strategyReturnPercent: 12,
  buyAndHoldReturnPercent: 7,
  buyAndHoldDifferencePercent: 5,
  equityCurve: [
    { timestamp: '2024-06-01T00:00:00Z', value: 100 },
    { timestamp: '2024-06-01T00:01:00Z', value: 112 },
  ],
};

const runReport = async (metrics, extra = {}) => {
  window.localStorage.setItem('crypto-tracker-lang', 'tr');
  getKlinesRange.mockResolvedValue([]);
  runBotBacktest.mockResolvedValue({
    data: { symbol: 'BTCUSDT', interval: '1m', summary, signals: [], metrics, ...extra },
  });
  const view = render(
    <LanguageProvider>
      <BacktestReport bot={{ id: 1, symbol: 'BTCUSDT', strategy: 0, buyRsiThreshold: 30, sellRsiThreshold: 70, tradeQuantity: 1 }} onClose={() => {}} />
    </LanguageProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Backtest Çalıştır' }));
  await screen.findByText('Toplam Sinyal');
  return view;
};

const expectNoBrokenNumbers = (container) => {
  expect(container.textContent).not.toMatch(/NaN|Infinity/);
};

describe('BacktestReport metrikleri', () => {
  test('null metrikler çökmeden — olarak görünür', async () => {
    const { container } = await runReport(nullMetrics);

    const card = (label) => screen.getByText(label).closest('div');
    expect(card('Kazanma Oranı')).toHaveTextContent('—');
    expect(card('Maks. Düşüş')).toHaveTextContent('—');
    expect(card('Kâr Faktörü')).toHaveTextContent('—');
    expect(card('Al ve Tut Farkı')).toHaveTextContent('—');
    expect(screen.queryByTestId('equity-chart')).not.toBeInTheDocument();
    expectNoBrokenNumbers(container);
  });

  test('metrik alanı hiç gelmezse de çökmez', async () => {
    const { container } = await runReport(undefined);

    expect(screen.queryByText('Kazanma Oranı')).not.toBeInTheDocument();
    expectNoBrokenNumbers(container);
  });

  test('dolu metrikler, grafik ve al-ve-tutu geçme rozeti görünür', async () => {
    const { container } = await runReport(filledMetrics);

    expect(screen.getByText('%50')).toBeInTheDocument();
    expect(screen.getByText('%6')).toHaveClass('negative');
    expect(screen.getByText('1,5')).toBeInTheDocument();
    expect(screen.getByText('Al ve tutu geçti')).toBeInTheDocument();
    expect(screen.getByTestId('equity-chart')).toHaveAttribute('data-points', '2');
    expectNoBrokenNumbers(container);
  });

  test('al ve tutun gerisinde kalınca kullanıcı bunu görür', async () => {
    await runReport({ ...filledMetrics, buyAndHoldDifferencePercent: -5 });

    expect(screen.getByText('Al ve tutun gerisinde')).toBeInTheDocument();
    expect(screen.queryByText('Al ve tutu geçti')).not.toBeInTheDocument();
  });

  test('her metriğin yanında açıklaması var', async () => {
    await runReport(filledMetrics);

    expect(screen.getByLabelText(/en yüksek değerinden/i)).toBeInTheDocument();
  });

  test('risk limiti olan botta limitli ve limitsiz sonuçlar yan yana görünür', async () => {
    await runReport(filledMetrics, {
      metricsWithoutRiskLimits: { ...nullMetrics, strategyReturnPercent: 30, maxDrawdownPercent: 40 },
    });

    expect(screen.getByText('Stop-Loss / Take-Profit Karşılaştırması')).toBeInTheDocument();
    expect(screen.getByText('%30')).toBeInTheDocument();
    expect(screen.getByText('%40')).toBeInTheDocument();
  });

  test('risk limiti yoksa karşılaştırma tablosu çıkmaz', async () => {
    await runReport(filledMetrics, { metricsWithoutRiskLimits: null });

    expect(screen.queryByText('Stop-Loss / Take-Profit Karşılaştırması')).not.toBeInTheDocument();
  });
});
