import { render, screen } from '@testing-library/react';
import { LanguageProvider, useLanguage } from '../context/LanguageContext';
import { isRiskExit, normalizeReason, reasonLabelKey } from './botSignalReason';

test('sayı olarak gelen neden doğru anahtara çevrilir', () => {
  expect(normalizeReason(0)).toBe('strategy');
  expect(normalizeReason(1)).toBe('stopLoss');
  expect(normalizeReason(2)).toBe('takeProfit');
});

test('metin olarak gelen neden büyük küçük harfe bakmadan çevrilir', () => {
  expect(normalizeReason('Strategy')).toBe('strategy');
  expect(normalizeReason('StopLoss')).toBe('stopLoss');
  expect(normalizeReason('takeprofit')).toBe('takeProfit');
});

test('boş ya da bilinmeyen neden strateji sayılır', () => {
  expect(normalizeReason(null)).toBe('strategy');
  expect(normalizeReason(undefined)).toBe('strategy');
  expect(normalizeReason(7)).toBe('strategy');
  expect(normalizeReason('Manual')).toBe('strategy');
});

test('yalnızca stop-loss ve take-profit risk çıkışıdır', () => {
  expect(isRiskExit(0)).toBe(false);
  expect(isRiskExit('StopLoss')).toBe(true);
  expect(isRiskExit(2)).toBe(true);
});

function ReasonLabel({ reason }) {
  const { t } = useLanguage();
  return <span>{t(reasonLabelKey(reason))}</span>;
}

const renderLabel = (reason, lang) => {
  localStorage.setItem('crypto-tracker-lang', lang);
  return render(
    <LanguageProvider>
      <ReasonLabel reason={reason} />
    </LanguageProvider>
  );
};

test.each([
  [0, 'tr', 'Strateji'],
  [1, 'tr', 'Stop-loss'],
  ['TakeProfit', 'tr', 'Take-profit'],
  ['Strategy', 'en', 'Strategy'],
  [1, 'en', 'Stop-loss'],
  [2, 'en', 'Take-profit'],
])('neden %p, %s dilinde "%s" olarak görünür', (reason, lang, label) => {
  renderLabel(reason, lang);
  expect(screen.getByText(label)).toBeInTheDocument();
});
