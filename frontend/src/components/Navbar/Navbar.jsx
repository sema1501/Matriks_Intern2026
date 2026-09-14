import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import ConnectionStatus from '../ConnectionStatus/ConnectionStatus';
import ThemeToggle from '../ThemeToggle/ThemeToggle';
import CurrencyToggle from '../CurrencyToggle/CurrencyToggle';
import './Navbar.css';

export default function Navbar() {
    const { user, logoutUser } = useAuth();
    const { t } = useLanguage();

    const roles = user?.roles || [];
    const isAdmin =
        roles.includes('Admin') || roles.includes('SuperAdmin');

    return (
        <nav className="navbar">
            <div className="navbar__links">
                {/* Herkese açık bağlantılar */}
                <Link to="/">{t('nav.home')}</Link>
                <Link to="/leaderboard">{t('nav.leaderboard')}</Link>
                <Link to="/compare">{t('nav.compare')}</Link>
                <Link to="/converter">{t('nav.converter')}</Link>
                <Link to="/feedback">{t('nav.feedback')}</Link>
                <Link to="/bot">{t('nav.bots')}</Link>

                {user ? (
                    <>
                        <Link to="/watchlist">{t('nav.watchlist')}</Link>
                        <Link to="/portfolio">{t('nav.portfolio')}</Link>
                        <Link to="/profile">{user.username}</Link>
                        <Link to="/dashboard">{t('nav.dashboard')}</Link>

                        {isAdmin && (
                            <Link to="/admin/bots">{t('nav.admin')}</Link>
                        )}

                        <button type="button" onClick={logoutUser}>
                            {t('nav.logout')}
                        </button>
                    </>
                ) : (
                    <>
                        <Link to="/signin">{t('nav.signin')}</Link>
                        <Link to="/signup">{t('nav.signup')}</Link>
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