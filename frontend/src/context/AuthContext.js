import { createContext, useContext, useState, useEffect } from 'react';
import { getFreshAccessToken, getMe, hasSession, logout, saveSession, clearSession } from '../services/apiService';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [user,    setUser]    = useState(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (hasSession()) {
      // Access token kaybolmuş ya da dolmuş olabilir: önce cookie ile sessizce yenilenir.
      // Refresh reddedilirse apiService oturumu kendisi kapatır.
      getFreshAccessToken()
        .then(() => getMe())
        .then(res => setUser(res.data))
        .catch(() => setUser(null))
        .finally(() => setLoading(false));
    } else {
      setLoading(false);
    }
  }, []);

  const loginUser = (token, userData) => {
    saveSession(token);
    setUser(userData);
  };

  const logoutUser = () => {
    // Backend token'ı iptal edip cookie'yi siler; istek başarısız olsa da yerelde çıkış yapılır.
    logout().catch(() => {});
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
