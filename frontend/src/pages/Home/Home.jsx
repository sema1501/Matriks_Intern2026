import { useAuth } from '../../context/AuthContext';
export default function Home() {
  const { user } = useAuth();
  return (
    <div>
      <h2>Hos geldin{user ? `, ${user.username}` : ''}!</h2>
      <p>CryptoTracker — kripto para takip uygulamasi.</p>
    </div>
  );
}
