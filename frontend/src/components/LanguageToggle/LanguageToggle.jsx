import { useLanguage } from '../../context/LanguageContext';

// Görev 41: Her sayfada sağ alt köşede sabit duran dil değiştirme butonu.
export default function LanguageToggle() {
  const { lang, toggleLang } = useLanguage();

  return (
    <button
      type="button"
      onClick={toggleLang}
      title="Dil / Language"
      aria-label={lang === 'tr' ? 'Switch to English' : "Türkçe'ye geç"}
      style={{
        position: 'fixed',
        bottom: '1rem',
        right: '1rem',
        zIndex: 1000,
        padding: '0.5rem 0.85rem',
        borderRadius: '999px',
        border: '1px solid var(--border-color, #94a3b8)',
        background: 'var(--surface-strong, #1e293b)',
        color: 'var(--text-primary, inherit)',
        cursor: 'pointer',
        fontWeight: 600,
        boxShadow: '0 2px 8px rgba(0,0,0,0.2)',
      }}
    >
      🌐 {lang === 'tr' ? 'EN' : 'TR'}
    </button>
  );
}
