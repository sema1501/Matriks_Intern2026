import React, { useState, useEffect } from 'react';
import { getBalance, getHoldings, getTransactions, getBotPerformance } from '../../services/apiService';
import { useBinancePrices } from '../../hooks/useBinancePrices';
import './Portfolio.css'; 

const BotPerformanceSummary = ({ totalPortfolioProfit, prices }) => { // YENİ: prices prop'u eklendi
  const [performance, setPerformance] = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchPerformance = async () => {
      try {
        const res = await getBotPerformance();
        setPerformance(res.data);
      } catch (error) {
        console.error("Bot performansı çekilirken hata:", error);
      } finally {
        setLoading(false);
      }
    };
    fetchPerformance();
  }, []);

  if (loading) return <div className="portfolio-loading">Bot performans verileri yükleniyor...</div>;
  
  if (!performance) return null;

  const total = performance.totalSignals;
  const approvedRate = total > 0 ? ((performance.approvedSignals / total) * 100).toFixed(2) : "0.00";
  const rejectedRate = total > 0 ? ((performance.rejectedSignals / total) * 100).toFixed(2) : "0.00";
  const expiredRate = total > 0 ? ((performance.expiredSignals / total) * 100).toFixed(2) : "0.00";
  const failedCount = performance.failedSignals ?? 0;
  const failedRate = total > 0 ? ((failedCount / total) * 100).toFixed(2) : "0.00"; 
  
  let unrealizedPnL = 0;
  
  if (performance.activePositions && performance.activePositions.length > 0) {
    performance.activePositions.forEach(pos => {
      const livePrice = prices[pos.symbol]?.currentPrice || 0;
      if (livePrice > 0) {
        const currentValue = pos.quantity * livePrice;
        unrealizedPnL += (currentValue - pos.totalCost); 
      }
    });
  }

  
  const realTimeBotPnL = performance.botProfitLoss + unrealizedPnL;
  
  const impactPercentage = totalPortfolioProfit !== 0 
    ? ((realTimeBotPnL / Math.abs(totalPortfolioProfit)) * 100).toFixed(2) 
    : "0.00";

  return (
    <div className="portfolio-section" style={{ borderLeft: '4px solid #3b82f6' }}>
      <h2 style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>Bot Performansı</h2>
      <div style={{ display: 'flex', gap: '20px', flexWrap: 'wrap', fontSize: '0.95rem' }}>
        
        <div><strong>Toplam Sinyal:</strong> {total}</div>
        
        <div className="text-green">
          <strong>Başarı Oranı:</strong> %{approvedRate} <span style={{fontSize: '0.8rem'}}>({performance.approvedSignals})</span>
        </div>
        
        <div className="text-red">
          <strong>Reddedilme Oranı:</strong> %{rejectedRate} <span style={{fontSize: '0.8rem'}}>({performance.rejectedSignals})</span>
        </div>
        
        <div style={{ color: '#9ca3af' }}>
          <strong>Süresi Geçme Oranı:</strong> %{expiredRate} <span style={{fontSize: '0.8rem'}}>({performance.expiredSignals})</span>
        </div>

        <div className="text-red">
          <strong>Başarısız:</strong> %{failedRate} <span style={{fontSize: '0.8rem'}}>({failedCount})</span>
        </div>
        
        <div className={realTimeBotPnL > 0 ? 'text-green' : realTimeBotPnL < 0 ? 'text-red' : ''} style={{ fontWeight: 'bold', borderLeft: '2px solid #4b5563', paddingLeft: '10px' }}>
          <strong>Genel Portföye Etkisi:</strong> ${realTimeBotPnL.toFixed(2)} 
          <span style={{ fontSize: '0.85rem', marginLeft: '5px' }}>
            ({realTimeBotPnL > 0 ? '+' : ''}%{impactPercentage})
          </span>
        </div>

      </div>
    </div>
  );
};
const Portfolio = () => {
  const [balance, setBalance] = useState(0);
  const [initialBalance, setInitialBalance] = useState(0); 
  const [holdings, setHoldings] = useState([]);
  const [transactions, setTransactions] = useState([]);
  const [loading, setLoading] = useState(true);
  const { prices } = useBinancePrices();

  // İşlem geçmişi: sayfalama + filtreleme durumu (Görev 51)
  const [txPage, setTxPage] = useState(1);
  const [txTotalPages, setTxTotalPages] = useState(1);
  const [txSymbol, setTxSymbol] = useState('');
  const [txType, setTxType] = useState(''); // '' = hepsi, '0' = Alış, '1' = Satış
  const [txLoading, setTxLoading] = useState(false);

  useEffect(() => {
    const fetchPortfolio = async () => {
      try {
        setLoading(true);
        const [balanceRes, holdingsRes] = await Promise.all([
          getBalance(),
          getHoldings()
        ]);

        setBalance(balanceRes.data.balance);
        setInitialBalance(balanceRes.data.initialBalance || 10000);
        setHoldings(holdingsRes.data || []);
      } catch (error) {
        console.error("Portföy verileri çekilirken hata oluştu:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchPortfolio();
  }, []);

  // İşlem geçmişini sayfa/filtre değiştikçe ayrı çek (Görev 51).
  useEffect(() => {
    const fetchTransactions = async () => {
      try {
        setTxLoading(true);
        const params = { pageNumber: txPage, pageSize: 10 };
        if (txSymbol.trim()) params.symbol = txSymbol.trim();
        if (txType !== '') params.type = Number(txType);

        const res = await getTransactions(params);
        setTransactions(res.data.items || []);
        setTxTotalPages(res.data.totalPages || 1);
      } catch (error) {
        console.error("İşlem geçmişi çekilirken hata oluştu:", error);
      } finally {
        setTxLoading(false);
      }
    };

    fetchTransactions();
  }, [txPage, txSymbol, txType]);

  const enrichedHoldings = holdings.map((holding) => {
    const priceObj = prices[holding.symbol];
    const currentPrice = priceObj?.currentPrice;
    const hasPrice = currentPrice !== undefined && currentPrice !== null && !isNaN(currentPrice);

    const currentValue = hasPrice ? holding.quantity * currentPrice : null;
    const pnlPercentage = (hasPrice && holding.avgBuyPrice > 0)
      ? ((currentPrice - holding.avgBuyPrice) / holding.avgBuyPrice) * 100
      : null;

    return {
      ...holding,
      currentPrice,
      currentValue,
      pnlPercentage,
      hasPrice
    };
  });

  const totalHoldingsValue = enrichedHoldings.reduce((acc, h) => {
    return acc + (h.hasPrice ? h.currentValue : 0);
  }, 0);

  const totalPortfolioValue = balance + totalHoldingsValue;
  
  const totalPnL = initialBalance > 0 
    ? ((totalPortfolioValue - initialBalance) / initialBalance) * 100 
    : 0;

  if (loading) {
    return <div className="portfolio-loading">Portföy verileri yükleniyor...</div>;
  }

  return (
    <div className="portfolio-container">
      {/* 1. ÜST ÖZET KARTI */}
      <div className="portfolio-summary-card">
        <div>
          <h1 className="portfolio-title">Portföyüm</h1>
          <p className="portfolio-balance-text">
            Kullanılabilir Bakiye: <span>${balance.toLocaleString()}</span>
          </p>
        </div>
        <div className="portfolio-total-wrapper">
          <p className="portfolio-total-label">Toplam Portföy Değeri</p>
          <p className="portfolio-total-value">
            ${totalPortfolioValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}
          </p>
          <p className={`portfolio-pnl ${totalPnL >= 0 ? 'positive' : 'negative'}`}>
            {totalPnL >= 0 ? '▲' : '▼'} {totalPnL.toFixed(2)}% Genel Kâr/Zarar
          </p>
        </div>
      </div>

      <BotPerformanceSummary 
        totalPortfolioProfit={totalPortfolioValue - initialBalance} 
        prices={prices} 
      />

      <div className="portfolio-section">
        <h2>Varlıklarım</h2>
        <div className="table-responsive">
          <table className="portfolio-table">
            <thead>
              <tr>
                <th>Coin</th>
                <th>Miktar</th>
                <th>Ortalama Alış Fiyatı</th>
                <th>Güncel Fiyat</th>
                <th>Güncel Değer</th>
                <th>Kâr / Zarar %</th>
              </tr>
            </thead>
            <tbody>
              {enrichedHoldings.length === 0 ? (
                <tr>
                  <td colSpan="6" className="empty-row">Henüz sahip olduğunuz bir coin bulunmuyor.</td>
                </tr>
              ) : (
                enrichedHoldings.map((h, i) => (
                  <tr key={i}>
                    <td className="font-bold">{h.symbol}</td>
                    <td>{h.quantity}</td>
                    <td>${h.avgBuyPrice.toLocaleString()}</td>
                    <td>
                      {h.hasPrice ? `$${h.currentPrice.toLocaleString()}` : <span className="loading-text">Yükleniyor...</span>}
                    </td>
                    <td>
                      {h.hasPrice ? `$${h.currentValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 })}` : '-'}
                    </td>
                    <td>
                      {h.hasPrice ? (
                        <span className={h.pnlPercentage >= 0 ? 'text-green' : 'text-red'}>
                          {h.pnlPercentage >= 0 ? '+' : ''}{h.pnlPercentage.toFixed(2)}%
                        </span>
                      ) : (
                        <span className="na-text">N/A</span>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      
      <div className="portfolio-section">
        <h2>İşlem Geçmişi</h2>

        {/* Filtreleme (Görev 51): sembol + işlem türü */}
        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
          <input
            type="text"
            placeholder="Sembol (örn. BTCUSDT)"
            value={txSymbol}
            onChange={(e) => { setTxPage(1); setTxSymbol(e.target.value); }}
            style={{ padding: '0.4rem', flex: '1 1 200px' }}
          />
          <select
            value={txType}
            onChange={(e) => { setTxPage(1); setTxType(e.target.value); }}
            style={{ padding: '0.4rem' }}
          >
            <option value="">Tüm işlemler</option>
            <option value="0">Sadece Alım</option>
            <option value="1">Sadece Satım</option>
          </select>
        </div>

        <div className="table-responsive">
          <table className="portfolio-table">
            <thead>
              <tr>
                <th>Tarih</th>
                <th>İşlem Türü</th>
                <th>Coin</th>
                <th>Miktar</th>
                <th>İşlem Fiyatı</th>
              </tr>
            </thead>
            <tbody>
              {txLoading ? (
                <tr>
                  <td colSpan="5" className="empty-row">Yükleniyor...</td>
                </tr>
              ) : transactions.length === 0 ? (
                <tr>
                  <td colSpan="5" className="empty-row">Bu kritere uygun işlem bulunmuyor.</td>
                </tr>
              ) : (
                transactions.map((t, i) => (
                  <tr key={i}>
                    <td className="date-text">{new Date(t.createdAt).toLocaleString()}</td>
                    <td>
                      <span className={`badge ${t.type === 0 ? 'buy' : 'sell'}`}>
                        {t.type === 0 ? 'ALIM' : 'SATIM'}
                      </span>
                    </td>
                    <td className="font-semibold">{t.symbol}</td>
                    <td>{t.quantity}</td>
                    <td>${t.price.toLocaleString()}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Sayfalama (Görev 51) */}
        {txTotalPages > 1 && (
          <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', gap: '1rem', marginTop: '1rem' }}>
            <button
              type="button"
              onClick={() => setTxPage((p) => Math.max(1, p - 1))}
              disabled={txPage <= 1 || txLoading}
              style={{ padding: '0.4rem 0.9rem', cursor: 'pointer' }}
            >
              ← Önceki
            </button>
            <span>Sayfa {txPage} / {txTotalPages}</span>
            <button
              type="button"
              onClick={() => setTxPage((p) => Math.min(txTotalPages, p + 1))}
              disabled={txPage >= txTotalPages || txLoading}
              style={{ padding: '0.4rem 0.9rem', cursor: 'pointer' }}
            >
              Sonraki →
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

export default Portfolio;