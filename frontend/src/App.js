import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { AuthProvider }      from './context/AuthContext';
import { PriceProvider }     from './context/PriceContext';
import { ThemeProvider }     from './context/ThemeContext';
import { WatchlistProvider } from './context/WatchlistContext';
import { useAuth }           from './context/AuthContext';
import Navbar      from './components/Navbar/Navbar';
import Home        from './pages/Home/Home';
import SignIn      from './pages/SignIn/SignIn';
import SignUp      from './pages/SignUp/SignUp';
import Profile     from './pages/Profile/Profile';
import Dashboard   from './pages/Dashboard/Dashboard';
import CoinDetail  from './pages/CoinDetail/CoinDetail';
import Watchlist   from './pages/Watchlist/Watchlist';

// PrivateRoute: giriş yapılmamışsa /signin'e yönlendirir
function PrivateRoute({ children }) {
  const { user, loading } = useAuth();
  if (loading) return null;
  return user ? children : <Navigate to="/signin" replace />;
}

function App() {
  return (
    <AuthProvider>
      <PriceProvider>
        <ThemeProvider>
          <WatchlistProvider>
            <BrowserRouter>
              <div className="app-shell">
                <Navbar />
                <main className="app-main">
                  <Routes>
                    <Route path="/"          element={<Home />} />
                    <Route path="/signin"    element={<SignIn />} />
                    <Route path="/signup"    element={<SignUp />} />
                    <Route path="/profile"   element={<Profile />} />
                    <Route path="/dashboard" element={<Dashboard />} />
                    <Route path="/coin/:symbol" element={<CoinDetail />} />
                    <Route
                      path="/watchlist"
                      element={
                        <PrivateRoute>
                          <Watchlist />
                        </PrivateRoute>
                      }
                    />
                  </Routes>
                </main>
              </div>
            </BrowserRouter>
          </WatchlistProvider>
        </ThemeProvider>
      </PriceProvider>
    </AuthProvider>
  );
}

export default App;
