import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useTheme } from '../../context/ThemeContext';
import ConnectionStatus from '../ConnectionStatus/ConnectionStatus';

export default function Navbar() {
    const { user, logoutUser } = useAuth();
    const { theme, toggleTheme } = useTheme();

    return (
        <nav style={{
            padding: '1rem',
            borderBottom: '1px solid var(--border-color)',
            backgroundColor: 'var(--bg-card)',
            color: 'var(--text-primary)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            gap: '1rem',
            transition: 'background-color 0.25s ease, border-color 0.25s ease'
        }}>
            <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
                <Link to="/" style={{ color: 'var(--text-primary)', fontWeight: 'bold', textDecoration: 'none' }}>Ana Sayfa</Link>
            </div>

            <div style={{ display: 'flex', gap: '1rem', alignItems: 'center' }}>
                <ConnectionStatus />

                <button
                    onClick={toggleTheme}
                    style={{
                        background: 'none',
                        border: '1px solid var(--border-color)',
                        cursor: 'pointer',
                        padding: '4px 8px',
                        borderRadius: '6px',
                        fontSize: '1rem'
                    }}
                >
                    {theme === 'light' ? '🌙' : '☀️'}
                </button>

                {user ? (
                    <>
                        <Link to="/profile" style={{ color: 'var(--text-primary)', textDecoration: 'none' }}>{user.username}</Link>
                        <button
                            onClick={logoutUser}
                            style={{
                                background: 'none',
                                border: '1px solid var(--border-color)',
                                color: 'var(--text-primary)',
                                cursor: 'pointer',
                                padding: '4px 8px',
                                borderRadius: '6px'
                            }}
                        >
                            Cikis
                        </button>
                    </>
                ) : (
                    <>
                        <Link to="/signin" style={{ color: 'var(--text-primary)', textDecoration: 'none' }}>Giris Yap</Link>
                        <Link to="/signup" style={{ color: 'var(--text-primary)', textDecoration: 'none' }}>Kayit Ol</Link>
                    </>
                )}
            </div>
        </nav>
    );
}