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
                {/* Herkese açık bağlantılar */}
                <Link to="/">Ana Sayfa</Link>
                <Link to="/leaderboard">Liderlik Tablosu</Link>
                <Link to="/converter">Dönüştürücü</Link>
                <Link to="/feedback">Geri Bildirim</Link>
                <Link to="/bot">🤖 Botlarım</Link> {/* Artık giriş yapmayanlara da görünür */}

                {user ? (
                    <>
                        <Link to="/watchlist"> 🌟 Favorilerim 🌟</Link>
                        <Link to="/portfolio">Portföyüm</Link>
                        <Link to="/profile">{user.username}</Link>
                        <Link to="/dashboard">Dashboard</Link>

                        <button type="button" onClick={logoutUser}>
                            Çıkış
                        </button>
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