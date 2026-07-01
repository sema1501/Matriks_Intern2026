import { useAuth } from '../../context/AuthContext';
import { useBinancePrices } from '../../hooks/useBinancePrices';
import CryptoCard from '../../components/CryptoCard/CryptoCard';

export default function Home() {
  const { user } = useAuth();
  const { prices } = useBinancePrices();

  const btcMeta = {
    name: 'Bitcoin',
    symbol: 'BTCUSDT',
    shortSymbol: 'BTC',
    logo: 'https://assets.coingecko.com/coins/images/1/large/bitcoin.png',
  };

  const ethMeta = {
    name: 'Ethereum',
    symbol: 'ETHUSDT',
    shortSymbol: 'ETH',
    logo: 'https://assets.coingecko.com/coins/images/279/large/ethereum.png',
  };

  return (
    <div style={{ padding: '32px' }}>
      <h2>Hos geldin{user ? `, ${user.username}` : ''}!</h2>
      <p>CryptoTracker — kripto para takip uygulamasi.</p>

      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(260px, 1fr))',
          gap: '20px',
          marginTop: '32px',
        }}
      >
        <CryptoCard meta={btcMeta} priceData={prices.BTCUSDT} />
        <CryptoCard meta={ethMeta} priceData={prices.ETHUSDT} />

        
        <CryptoCard
          meta={{
            name: 'Loading Coin',
            symbol: 'LOADUSDT',
            shortSymbol: 'LOAD',
          }}
          priceData={undefined}
        />
      </div>
    </div>
  );
}