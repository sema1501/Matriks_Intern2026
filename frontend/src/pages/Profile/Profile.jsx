import { useAuth } from '../../context/AuthContext';
// TODO Gorev 2: kullanici bilgileri, profil duzenleme formu, sifre degistirme formu
export default function Profile() {
  const { user } = useAuth();
  return (
    <div>
      <h2>Profil</h2>
      <p style={{ color: 'orange' }}>Gorev 2 — tamamlanmadi</p>
      <pre>{JSON.stringify(user, null, 2)}</pre>
    </div>
  );
}
