import React, { useState, useEffect } from 'react';
import { getBalance, getHoldings, getTransactions, getBotPerformance } from '../../services/apiService';
import { useBinancePrices } from '../../hooks/useBinancePrices';
import './Portfolio.css'; 

const BotPerformanceSummary = ({ totalPortfolioProfit }) => {
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
  const botPnl = performance.botProfitLoss;
  
  
  const impactPercentage = totalPortfolioProfit !== 0 
    ? ((botPnl / totalPortfolioProfit) * 100).toFixed(2) 
    : "0.00";

  return (
    <div className="portfolio-section" style={{ borderLeft: '4px solid #3b82f6' }}>
      <h2 style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>Bot Performansı</h2>
      <div style={{ display: 'flex', gap: '20px', flexWrap: 'wrap', fontSize: '0.95rem' }}>
        
        <div><strong>Toplam Sinyal:</strong> {total}</div>
        
        <div className="text-green">
          <strong>Onaylanma Oranı:</strong> %{approvedRate} <span style={{fontSize: '0.8rem'}}>({performance.approvedSignals})</span>
        </div>
        
        <div className="text-red">
          <strong>Reddedilme Oranı:</strong> %{rejectedRate} <span style={{fontSize: '0.8rem'}}>({performance.rejectedSignals})</span>
        </div>
        
        <div style={{ color: '#9ca3af' }}>
          <strong>Süresi Geçme Oranı:</strong> %{expiredRate} <span style={{fontSize: '0.8rem'}}>({performance.expiredSignals})</span>
        </div>
        
        <div className={botPnl >= 0 ? 'text-green' : 'text-red'} style={{ fontWeight: 'bold', borderLeft: '2px solid #4b5563', paddingLeft: '10px' }}>
          <strong>Genel Portföye Etkisi:</strong> ${botPnl.toFixed(2)} 
          <span style={{ fontSize: '0.85rem', marginLeft: '5px' }}>
            ({botPnl >= 0 ? '+' : ''}%{impactPercentage})
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

  useEffect(() => {
    const fetchPortfolio = async () => {
      try {
        setLoading(true);
        const [balanceRes, holdingsRes, transactionsRes] = await Promise.all([
          getBalance(),
          getHoldings(),
          getTransactions()
        ]);

        setBalance(balanceRes.data.balance);
        setInitialBalance(balanceRes.data.initialBalance || 10000); 
        setHoldings(holdingsRes.data || []);
        setTransactions(transactionsRes.data || []);
      } catch (error) {
        console.error("Portföy verileri çekilirken hata oluştu:", error);
      } finally {
        setLoading(false);
      }
    };

    fetchPortfolio();
  }, []);

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

      <BotPerformanceSummary totalPortfolioProfit={totalPortfolioValue - initialBalance} />

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
              {transactions.length === 0 ? (
                <tr>
                  <td colSpan="5" className="empty-row">Henüz işlem geçmişiniz bulunmuyor.</td>
                </tr>
              ) : (
                transactions.map((t, i) => (
                  <tr key={i}>
                    <td className="date-text">{new Date(t.createdAt).toLocaleString()}</td>
                    <td>
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <span className={`badge ${t.type === 0 ? 'buy' : 'sell'}`}>
                          {t.type === 0 ? 'ALIM' : 'SATIM'}
                        </span>
                        <span style={{ 
                          fontSize: '0.75rem', 
                          padding: '3px 8px', 
                          borderRadius: '4px', 
                          backgroundColor: t.description && t.description.includes("Bot") ? '#2563eb' : '#374151',
                          color: '#fff',
                          fontWeight: '500',
                          letterSpacing: '0.3px'
                        }}>
                          {t.description && t.description.includes("Bot") ? 'Bot' : 'Manuel'}
                        </span>
                      </div>
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
      </div>
    </div>
  );
};

export default Portfolio;