import { useState } from 'react';
import { useBinancePrices } from '../../hooks/useBinancePrices';
import { useCurrency } from '../../context/CurrencyContext';
import { COIN_META } from '../../data/coinMeta';

// Görev 42: İki coini yan yana karşılaştırma sayfası.
export default function Compare() {
  const { prices } = useBinancePrices();
  const { formatPrice } = useCurrency();
  const symbols = Object.keys(COIN_META);

  const [left, setLeft] = useState('BTCUSDT');
  const [right, setRight] = useState('ETHUSDT');

  const renderChange = (value) => {
    if (value === undefined || value === null || isNaN(value)) return '—';
    const positive = value >= 0;
    return (
      <span style={{ color: positive ? '#22c55e' : '#ef4444', fontWeight: 600 }}>
        {positive ? '+' : ''}{Number(value).toFixed(2)}%
      </span>
    );
  };

  const cellPrice = (symbol, field) => {
    const d = prices[symbol];
    if (!d || d[field] === undefined || d[field] === null) return '—';
    return formatPrice(d[field]);
  };

  const selectStyle = { padding: '0.5rem', width: '100%' };
  const options = symbols.map((s) => (
    <option key={s} value={s}>
      {COIN_META[s].name} ({COIN_META[s].symbol})
    </option>
  ));

  const th = { textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid var(--border-color, #e2e8f0)' };
  const td = { padding: '0.75rem', borderBottom: '1px solid var(--border-color, #e2e8f0)' };

  return (
    <div style={{ width: '100%', padding: '0 1rem', boxSizing: 'border-box' }}>
      <h2>Coin Karşılaştırma</h2>
      <p style={{ color: 'var(--text-muted, #64748b)', marginBottom: '1rem' }}>
        İki coini seçip güncel piyasa verilerini yan yana karşılaştırın.
      </p>

      <table style={{ width: '100%', borderCollapse: 'collapse' }}>
        <thead>
          <tr>
            <th style={th}>Metrik</th>
            <th style={th}>
              <select value={left} onChange={(e) => setLeft(e.target.value)} style={selectStyle}>
                {options}
              </select>
            </th>
            <th style={th}>
              <select value={right} onChange={(e) => setRight(e.target.value)} style={selectStyle}>
                {options}
              </select>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td style={td}>Güncel Fiyat</td>
            <td style={td}>{cellPrice(left, 'currentPrice')}</td>
            <td style={td}>{cellPrice(right, 'currentPrice')}</td>
          </tr>
          <tr>
            <td style={td}>24s Değişim</td>
            <td style={td}>{renderChange(prices[left]?.priceChangePercentage24h)}</td>
            <td style={td}>{renderChange(prices[right]?.priceChangePercentage24h)}</td>
          </tr>
          <tr>
            <td style={td}>24s En Yüksek</td>
            <td style={td}>{cellPrice(left, 'high24h')}</td>
            <td style={td}>{cellPrice(right, 'high24h')}</td>
          </tr>
          <tr>
            <td style={td}>24s En Düşük</td>
            <td style={td}>{cellPrice(left, 'low24h')}</td>
            <td style={td}>{cellPrice(right, 'low24h')}</td>
          </tr>
        </tbody>
      </table>

      {(!prices[left] || !prices[right]) && (
        <p style={{ color: 'var(--text-muted, #64748b)', marginTop: '1rem' }}>
          Fiyatlar yükleniyor... (canlı veri birkaç saniye içinde gelir)
        </p>
      )}
    </div>
  );
}
