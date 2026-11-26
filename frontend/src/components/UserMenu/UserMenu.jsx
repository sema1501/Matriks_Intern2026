import { Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import useDropdown from '../NavDropdown/useDropdown';
import '../NavDropdown/NavDropdown.css';

// Sağ üstteki küçük kullanıcı menüsü: Profilim ve Çıkış Yap.
export default function UserMenu() {
  const { user, logoutUser } = useAuth();
  const { t } = useLanguage();
  const { open, toggle, close, wrapperRef, buttonRef } = useDropdown();

  if (!user) return null;

  const initial = user.username?.charAt(0).toUpperCase() || '?';

  return (
    <div className="nav-dropdown" ref={wrapperRef}>
      <button
        type="button"
        ref={buttonRef}
        className="nav-dropdown__button"
        onClick={toggle}
        aria-haspopup="menu"
        aria-expanded={open}
        aria-label={t('userMenu.label', { username: user.username })}
        title={user.username}
      >
        <span className="nav-dropdown__avatar" aria-hidden="true">
          {user.avatarUrl ? <img src={user.avatarUrl} alt="" /> : initial}
        </span>
      </button>

      {open && (
        <div className="nav-dropdown__panel nav-dropdown__panel--narrow" role="menu" aria-label={t('userMenu.title')}>
          <div className="nav-dropdown__user">
            <strong>{user.username}</strong>
            {user.email && <small>{user.email}</small>}
          </div>
          <Link to="/profile" role="menuitem" className="nav-dropdown__item" onClick={close}>
            {t('nav.profile')}
          </Link>
          <button
            type="button"
            role="menuitem"
            className="nav-dropdown__item nav-dropdown__item--danger"
            onClick={() => { close(); logoutUser(); }}
          >
            {t('nav.logout')}
          </button>
        </div>
      )}
    </div>
  );
}
