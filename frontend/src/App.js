import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import { BinanceProvider } from './context/BinanceContext';
import { ThemeProvider } from './context/ThemeContext';
import Navbar from './components/Navbar/Navbar';
import Home from './pages/Home/Home';
import SignIn from './pages/SignIn/SignIn';
import SignUp from './pages/SignUp/SignUp';
import Profile from './pages/Profile/Profile';
import Dashboard from './pages/Dashboard/Dashboard';
import CoinDetail from './pages/CoinDetail/CoinDetail';

function App() {
    return (
        <AuthProvider>
            <ThemeProvider>
                <BrowserRouter>
                    <BinanceProvider>
                        <Navbar />
                        <main style={{ padding: '2rem' }}>
                            <Routes>
                                <Route path="/" element={<Home />} />
                                <Route path="/signin" element={<SignIn />} />
                                <Route path="/signup" element={<SignUp />} />
                                <Route path="/profile" element={<Profile />} />
                                <Route path="/dashboard" element={<Dashboard />} />
                                <Route path="/coin/:symbol" element={<CoinDetail />} />
                            </Routes>
                        </main>
                    </BinanceProvider>
                </BrowserRouter>
            </ThemeProvider>
        </AuthProvider>
    );
}

export default App;