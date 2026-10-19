import { createContext, useContext, useState, useEffect } from 'react';
import { getMe, logout, saveSession, clearSession } from '../services/apiService';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user,    setUser]    = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const token = localStorage.getItem('token');
    if (token) {
      getMe()
        .then(res => setUser(res.data))
        // Süresi dolmuş token'ı apiService yenilemeyi dener; o da olmazsa oturumu kendisi kapatır.
        .catch(() => setUser(null))
        .finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, []);

  const loginUser = (token, refreshToken, userData) => {
    saveSession(token, refreshToken);
    setUser(userData);
  };

  const logoutUser = () => {
    const refreshToken = localStorage.getItem('refreshToken');
    // Backend'deki refresh token'ı iptal et; istek başarısız olsa da yerelde çıkış yapılır.
    if (refreshToken) logout(refreshToken).catch(() => {});
    clearSession();
    setUser(null);
  };

  return (
    <AuthContext.Provider value={{ user, loading, loginUser, logoutUser, setUser }}>
      {children}
    </AuthContext.Provider>
  );
}

export const useAuth = () => useContext(AuthContext);
