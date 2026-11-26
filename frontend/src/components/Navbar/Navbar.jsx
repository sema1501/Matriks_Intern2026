import { useEffect, useRef, useState } from 'react';
import { Link, NavLink, useLocation } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import ConnectionStatus from '../ConnectionStatus/ConnectionStatus';
import RealtimeStatus from '../RealtimeStatus/RealtimeStatus';
import ThemeToggle from '../ThemeToggle/ThemeToggle';
import CurrencyToggle from '../CurrencyToggle/CurrencyToggle';
import Logo from '../Logo/Logo';
import CoinSearch from '../CoinSearch/CoinSearch';
import './Navbar.css';

// #109: Uzun link sırası soldan açılan menüye taşındı.
export default function Navbar() {
    const { user, logoutUser } = useAuth();
    const { t } = useLanguage();
    const location = useLocation();
    const [menuOpen, setMenuOpen] = useState(false);
    const menuButtonRef = useRef(null);
    const closeButtonRef = useRef(null);
    const wasOpenRef = useRef(false);

    const roles = user?.roles || [];
    const isAdmin = roles.includes('Admin') || roles.includes('SuperAdmin');

    const closeMenu = () => setMenuOpen(false);

    // Sayfa değişince menü kapanır.
    useEffect(() => {
        setMenuOpen(false);
    }, [location.pathname]);

    // Escape ile kapanır, açıkken sayfa kaymaz, odak menüye geçer ve kapanınca butona döner.
    useEffect(() => {
        if (menuOpen) {
            wasOpenRef.current = true;
            closeButtonRef.current?.focus();
            const previousOverflow = document.body.style.overflow;
            document.body.style.overflow = 'hidden';
            const handleKey = (e) => {
                if (e.key === 'Escape') setMenuOpen(false);
            };
            document.addEventListener('keydown', handleKey);
            return () => {
                document.body.style.overflow = previousOverflow;
                document.removeEventListener('keydown', handleKey);
            };
        }
        if (wasOpenRef.current) {
            wasOpenRef.current = false;
            menuButtonRef.current?.focus();
        }
        return undefined;
    }, [menuOpen]);

    const groups = [
        {
            key: 'market',
            title: t('nav.group.market'),
            links: [
                { to: '/', label: t('nav.home'), end: true },
                { to: '/leaderboard', label: t('nav.leaderboard') },
                { to: '/compare', label: t('nav.compare') },
                { to: '/converter', label: t('nav.converter') },
            ],
        },
        user && {
            key: 'account',
            title: t('nav.group.account'),
            links: [
                { to: '/profile', label: t('nav.profile') },
                { to: '/portfolio', label: t('nav.portfolio') },
                { to: '/watchlist', label: t('nav.watchlist') },
                { to: '/bot', label: t('nav.bots') },
            ],
        },
        user && isAdmin && {
            key: 'admin',
            title: t('nav.group.admin'),
            links: [
                { to: '/dashboard', label: t('nav.dashboard') },
                { to: '/admin/bots', label: t('nav.admin') },
            ],
        },
    ].filter(Boolean);

    const linkClass = ({ isActive }) => `side-menu__link${isActive ? ' is-active' : ''}`;

    return (
        <>
            <nav className="navbar" aria-label={t('nav.mainLabel')}>
                <div className="navbar__start">
                    <button
                        type="button"
                        ref={menuButtonRef}
                        className="navbar__menu-button"
                        onClick={() => setMenuOpen(true)}
                        aria-label={t('nav.openMenu')}
                        aria-expanded={menuOpen}
                        aria-controls="side-menu"
                    >
                        <span aria-hidden="true" />
                        <span aria-hidden="true" />
                        <span aria-hidden="true" />
                    </button>
                    <Logo />
                </div>

                <div className="navbar__center">
                    <CoinSearch />
                </div>

                <div className="navbar__actions">
                    {!user && (
                        <Link to="/signin" className="navbar__signin">{t('nav.signin')}</Link>
                    )}
                    <ConnectionStatus />
                    <RealtimeStatus />
                    <CurrencyToggle />
                    <ThemeToggle />
                </div>
            </nav>

            <div
                className={`side-menu__overlay${menuOpen ? ' is-open' : ''}`}
                onClick={closeMenu}
                aria-hidden="true"
            />

            <aside
                id="side-menu"
                className={`side-menu${menuOpen ? ' is-open' : ''}`}
                aria-label={t('nav.menuLabel')}
                aria-hidden={!menuOpen}
                inert={menuOpen ? undefined : ''}
            >
                <div className="side-menu__header">
                    <Logo onClick={closeMenu} />
                    <button
                        type="button"
                        ref={closeButtonRef}
                        className="side-menu__close"
                        onClick={closeMenu}
                        aria-label={t('nav.closeMenu')}
                    >
                        ×
                    </button>
                </div>

                {user && (
                    <div className="side-menu__user">
                        <span className="side-menu__avatar" aria-hidden="true">
                            {user.username?.charAt(0).toUpperCase()}
                        </span>
                        <span className="side-menu__username">{user.username}</span>
                    </div>
                )}

                <div className="side-menu__body">
                    {groups.map((group) => (
                        <section key={group.key} className="side-menu__group" aria-labelledby={`side-menu-${group.key}`}>
                            <h2 id={`side-menu-${group.key}`} className="side-menu__title">{group.title}</h2>
                            <ul>
                                {group.links.map((link) => (
                                    <li key={link.to}>
                                        <NavLink to={link.to} end={link.end} className={linkClass}>
                                            {link.label}
                                        </NavLink>
                                    </li>
                                ))}
                            </ul>
                        </section>
                    ))}
                </div>

                <div className="side-menu__footer">
                    <NavLink to="/feedback" className={linkClass}>
                        {t('nav.feedback')}
                    </NavLink>
                    {user ? (
                        <button type="button" className="side-menu__logout" onClick={() => { closeMenu(); logoutUser(); }}>
                            {t('nav.logout')}
                        </button>
                    ) : (
                        <div className="side-menu__auth">
                            <Link to="/signin" className="side-menu__auth-primary">{t('nav.signin')}</Link>
                            <Link to="/signup" className="side-menu__auth-secondary">{t('nav.signup')}</Link>
                        </div>
                    )}
                </div>
            </aside>
        </>
    );
}
