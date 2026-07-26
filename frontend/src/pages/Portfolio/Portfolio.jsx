import React, { useState, useEffect } from 'react';
import { getBalance, getHoldings, getTransactions } from '../../services/apiService';
import { useBinancePrices } from '../../hooks/useBinancePrices';
import './Portfolio.css'; 

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
      </div>
    </div>
  );
};

export default Portfolio;