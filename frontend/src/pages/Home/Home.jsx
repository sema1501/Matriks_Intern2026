import { useAuth } from '../../context/AuthContext';
import CryptoGrid from '../../components/CryptoGrid/CryptoGrid';

export default function Home() {
  const { user } = useAuth();

  return (
    <div style={{ padding: '32px' }}>
      <h2>Hos geldin{user ? `, ${user.username}` : ''}!</h2>
      <p>CryptoTracker — kripto para takip uygulamasi.</p>
      
      {/* Senin yazdığın canlı veri ızgarasını sayfaya yerleştiriyoruz */}
      <div style={{ marginTop: '32px' }}>
        <CryptoGrid />
      </div>
    </div>
  );
}