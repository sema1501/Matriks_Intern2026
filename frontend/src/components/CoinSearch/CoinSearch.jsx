import { useEffect, useId, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { TRACKED_SYMBOLS } from '../../data/trackedSymbols';
import { COIN_META } from '../../data/coinMeta';
import { useLanguage } from '../../context/LanguageContext';
import './CoinSearch.css';

const MAX_RESULTS = 8;
const MAX_RECENT = 5;
const RECENT_KEY = 'crypto-tracker-recent-coins';

const COINS = TRACKED_SYMBOLS.map((pair) => ({
  pair,
  symbol: COIN_META[pair]?.symbol || pair.replace(/USDT$/, ''),
  name: COIN_META[pair]?.name || pair,
  image: COIN_META[pair]?.image,
}));
const COIN_BY_PAIR = new Map(COINS.map((coin) => [coin.pair, coin]));

function readRecent() {
  try {
    const saved = JSON.parse(localStorage.getItem(RECENT_KEY) || '[]');
    return Array.isArray(saved) ? saved.filter((pair) => COIN_BY_PAIR.has(pair)).slice(0, MAX_RECENT) : [];
  } catch {
    return [];
  }
}

// Navbar'daki coin arama: yazdıkça liste açılır, seçilen coinin detay sayfasına gider.
// Arama boşken son aramalar gösterilir.
export default function CoinSearch() {
  const { t } = useLanguage();
  const navigate = useNavigate();
  const listId = useId();
  const wrapperRef = useRef(null);
  const inputRef = useRef(null);
  const [query, setQuery] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(0);
  const [recent, setRecent] = useState(readRecent);

  const trimmed = query.trim().toLowerCase();
  const showingRecent = !trimmed && recent.length > 0;

  const results = useMemo(() => {
    if (!trimmed) {
      return recent.length > 0
        ? recent.map((pair) => COIN_BY_PAIR.get(pair))
        : COINS.slice(0, MAX_RESULTS);
    }
    return COINS.filter(
      (coin) =>
        coin.symbol.toLowerCase().includes(trimmed) ||
        coin.name.toLowerCase().includes(trimmed) ||
        coin.pair.toLowerCase().includes(trimmed)
    ).slice(0, MAX_RESULTS);
  }, [trimmed, recent]);

  useEffect(() => {
    setActiveIndex(0);
  }, [query]);

  // Dışarı tıklanınca liste kapanır.
  useEffect(() => {
    if (!open) return undefined;
    const handleClick = (e) => {
      if (wrapperRef.current && !wrapperRef.current.contains(e.target)) setOpen(false);
    };
    document.addEventListener('mousedown', handleClick);
    return () => document.removeEventListener('mousedown', handleClick);
  }, [open]);

  const rememberCoin = (pair) => {
    const next = [pair, ...recent.filter((p) => p !== pair)].slice(0, MAX_RECENT);
    setRecent(next);
    localStorage.setItem(RECENT_KEY, JSON.stringify(next));
  };

  const clearRecent = () => {
    setRecent([]);
    localStorage.removeItem(RECENT_KEY);
    inputRef.current?.focus();
  };

  const selectCoin = (coin) => {
    rememberCoin(coin.pair);
    navigate(`/coin/${coin.pair}`);
    setQuery('');
    setOpen(false);
    inputRef.current?.blur();
  };

  const handleKeyDown = (e) => {
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      setOpen(true);
      setActiveIndex((i) => (results.length ? (i + 1) % results.length : 0));
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      setActiveIndex((i) => (results.length ? (i - 1 + results.length) % results.length : 0));
    } else if (e.key === 'Enter') {
      if (open && results[activeIndex]) {
        e.preventDefault();
        selectCoin(results[activeIndex]);
      }
    } else if (e.key === 'Escape') {
      setOpen(false);
      e.currentTarget.blur();
    }
  };

  const showList = open;
  const activeId = showList && results[activeIndex] ? `${listId}-${results[activeIndex].pair}` : undefined;

  return (
    <div className="coin-search" ref={wrapperRef}>
      <svg className="coin-search__icon" viewBox="0 0 24 24" width="18" height="18" aria-hidden="true">
        <circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" strokeWidth="2" />
        <path d="M20 20 L16 16" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
      </svg>
      <input
        ref={inputRef}
        type="search"
        className="coin-search__input"
        value={query}
        placeholder={t('search.placeholder')}
        aria-label={t('search.label')}
        role="combobox"
        aria-expanded={showList}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-activedescendant={activeId}
        autoComplete="off"
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => setOpen(true)}
        onKeyDown={handleKeyDown}
      />

      {showList && (
        <div className="coin-search__panel">
          {showingRecent && (
            <div className="coin-search__heading">
              <span>{t('search.recent')}</span>
              <button type="button" onMouseDown={(e) => e.preventDefault()} onClick={clearRecent}>
                {t('search.clearRecent')}
              </button>
            </div>
          )}
          <ul className="coin-search__list" id={listId} role="listbox" aria-label={showingRecent ? t('search.recent') : t('search.label')}>
            {results.length === 0 ? (
              <li className="coin-search__empty" role="presentation">
                {t('search.noResults')}
              </li>
            ) : (
              results.map((coin, index) => (
                <li
                  key={coin.pair}
                  id={`${listId}-${coin.pair}`}
                  role="option"
                  aria-selected={index === activeIndex}
                  className={`coin-search__option${index === activeIndex ? ' is-active' : ''}`}
                  onMouseDown={(e) => e.preventDefault()}
                  onMouseEnter={() => setActiveIndex(index)}
                  onClick={() => selectCoin(coin)}
                >
                  {coin.image ? (
                    <img src={coin.image} alt="" width="22" height="22" loading="lazy" />
                  ) : (
                    <span className="coin-search__placeholder" aria-hidden="true" />
                  )}
                  <span className="coin-search__symbol">{coin.symbol}</span>
                  <span className="coin-search__name">{coin.name}</span>
                  {showingRecent && (
                    <svg className="coin-search__recent-icon" viewBox="0 0 24 24" width="14" height="14" aria-hidden="true">
                      <circle cx="12" cy="12" r="9" fill="none" stroke="currentColor" strokeWidth="2" />
                      <path d="M12 7v5l3 2" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
                    </svg>
                  )}
                </li>
              ))
            )}
          </ul>
        </div>
      )}
    </div>
  );
}
