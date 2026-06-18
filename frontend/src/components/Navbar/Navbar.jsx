import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';

// TODO Gorev 4: rol rozeti, Admin icin Dashboard linki, dropdown
export default function Navbar() {
  const { user, logoutUser } = useAuth();
  return (
    <nav style={{ padding: '1rem', borderBottom: '1px solid #ccc', display: 'flex', gap: '1rem' }}>
      <Link to="/">Ana Sayfa</Link>
      {user ? (
        <>
          <Link to="/profile">{user.username}</Link>
          <button onClick={logoutUser}>Cikis</button>
        </>
      ) : (
        <>
          <Link to="/signin">Giris Yap</Link>
          <Link to="/signup">Kayit Ol</Link>
        </>
      )}
    </nav>
  );
}
