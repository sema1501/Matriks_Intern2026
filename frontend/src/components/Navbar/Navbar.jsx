import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import ConnectionStatus from '../ConnectionStatus/ConnectionStatus';
import ThemeToggle from '../ThemeToggle/ThemeToggle';
import './Navbar.css';

export default function Navbar() {
  const { user, logoutUser } = useAuth();

  return (
    <nav className="navbar">
      <div className="navbar__links">
        <Link to="/">Ana Sayfa</Link>
        {user ? (
          <><Link to="/watchlist"> 🌟 Favorilerim 🌟</Link>
            <Link to="/profile">{user.username}</Link>
            <button type="button" onClick={logoutUser}>Çıkış</button>
          </>
        ) : (
          <>
            <Link to="/signin">Giriş Yap</Link>
            <Link to="/signup">Kayıt Ol</Link>
          </>
        )}
      </div>

      <div className="navbar__actions">
        <ConnectionStatus />
        <ThemeToggle />
      </div>
    </nav>
  );
}
