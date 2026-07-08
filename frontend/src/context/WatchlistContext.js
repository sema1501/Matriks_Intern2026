import { createContext, useContext, useState, useEffect, useCallback } from 'react';
import { getWatchlist, addToWatchlist, removeFromWatchlist } from '../services/apiService';
import { useAuth } from './AuthContext';

const WatchlistContext = createContext(null);

export function WatchlistProvider({ children }) {
  const { user } = useAuth();
  const [watchlist, setWatchlist]   = useState([]); // [{ id, symbol, createdAt }]
  const [loading,   setLoading]     = useState(false);

  // Kullanıcı giriş yaptığında listeyi çek
  useEffect(() => {
    if (!user) { setWatchlist([]); return; }
    setLoading(true);
    getWatchlist()
      .then(res => setWatchlist(res.data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, [user]);

  // Symbol izleme listesinde var mı?
  const isFavorite = useCallback(
    (symbol) => watchlist.some(w => w.symbol === symbol?.toUpperCase()),
    [watchlist]
  );

  // Ekle
  const addFavorite = useCallback(async (symbol) => {
    const res = await addToWatchlist(symbol);
    setWatchlist(prev => [res.data, ...prev]);
  }, []);

  // Çıkar
  const removeFavorite = useCallback(async (symbol) => {
    await removeFromWatchlist(symbol);
    setWatchlist(prev => prev.filter(w => w.symbol !== symbol?.toUpperCase()));
  }, []);

  // Toggle — karttan tek çağrı ile ekle/çıkar
  const toggleFavorite = useCallback(async (symbol) => {
    if (isFavorite(symbol)) {
      await removeFavorite(symbol);
    } else {
      await addFavorite(symbol);
    }
  }, [isFavorite, addFavorite, removeFavorite]);

  return (
    <WatchlistContext.Provider value={{ watchlist, loading, isFavorite, toggleFavorite }}>
      {children}
    </WatchlistContext.Provider>
  );
}

export const useWatchlist = () => useContext(WatchlistContext);
