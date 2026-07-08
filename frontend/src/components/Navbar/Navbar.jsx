import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import ConnectionStatus from '../ConnectionStatus/ConnectionStatus';
import ThemeToggle from '../ThemeToggle/ThemeToggle';
import CurrencyToggle from '../CurrencyToggle/CurrencyToggle';
import './Navbar.css';

export default function Navbar() {
  const { user, logoutUser } = useAuth();

  return (
    <nav className="navbar">
      <div className="navbar__links">
        <Link to="/">Ana Sayfa</Link>
        <Link to="/converter">Dönüştürücü</Link>
        
        {user ? (
          <>
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
        <CurrencyToggle />
        <ThemeToggle />
      </div>
    </nav>
  );
}
