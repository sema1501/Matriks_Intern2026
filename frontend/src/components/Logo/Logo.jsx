import { useId } from 'react';
import { Link } from 'react-router-dom';
import './Logo.css';

// Geçici logo: Matriks logosu gelene kadar kullanılır.
// Renkler theme.css'teki --logo-start ve --logo-end değişkenlerinden gelir.
export default function Logo({ onClick }) {
  const gradientId = `logo-gradient-${useId().replace(/:/g, '')}`;

  return (
    <Link to="/" className="logo" onClick={onClick} aria-label="CryptoTracker">
      <svg className="logo__icon" viewBox="0 0 32 32" width="32" height="32" aria-hidden="true">
        <defs>
          <linearGradient id={gradientId} x1="0" y1="0" x2="1" y2="1">
            <stop offset="0%" style={{ stopColor: 'var(--logo-start)' }} />
            <stop offset="100%" style={{ stopColor: 'var(--logo-end)' }} />
          </linearGradient>
        </defs>
        <rect x="1" y="1" width="30" height="30" rx="9" fill={`url(#${gradientId})`} />
        <rect x="8" y="16" width="3.5" height="8" rx="1.2" fill="#fff" opacity="0.55" />
        <rect x="14.25" y="12" width="3.5" height="12" rx="1.2" fill="#fff" opacity="0.75" />
        <rect x="20.5" y="8" width="3.5" height="16" rx="1.2" fill="#fff" />
        <path d="M7 13.5 L13 9.5 L17 11.5 L25 6" fill="none" stroke="#fff" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" />
      </svg>
      <span className="logo__text">
        Crypto<span className="logo__text-accent">Tracker</span>
      </span>
    </Link>
  );
}
