import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './context/AuthContext';
import Navbar    from './components/Navbar/Navbar';
import Home      from './pages/Home/Home';
import SignIn    from './pages/SignIn/SignIn';
import SignUp    from './pages/SignUp/SignUp';
import Profile   from './pages/Profile/Profile';
import Dashboard from './pages/Dashboard/Dashboard';

function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Navbar />
        <main style={{ padding: '2rem' }}>
          <Routes>
            <Route path="/"          element={<Home />} />
            <Route path="/signin"    element={<SignIn />} />
            <Route path="/signup"    element={<SignUp />} />
            <Route path="/profile"   element={<Profile />} />
            <Route path="/dashboard" element={<Dashboard />} />
          </Routes>
        </main>
      </BrowserRouter>
    </AuthProvider>
  );
}

export default App;
