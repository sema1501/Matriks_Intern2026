import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { login } from '../../services/apiService';

export default function SignIn() {
  const { loginUser } = useAuth();
  const navigate = useNavigate();
  const [form,    setForm]    = useState({ usernameOrEmail: '', password: '' });
  const [error,   setError]   = useState('');
  const [loading, setLoading] = useState(false);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const res = await login(form);
      loginUser(res.data.token, res.data);
      navigate('/');
    } catch (err) {
      setError(err.response?.data?.message || 'Giriş başarısız. Bilgilerinizi kontrol edin.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ maxWidth: '400px', margin: '80px auto', padding: '2rem' }}>
      <h2>Giriş Yap</h2>

      {error && (
        <p style={{ color: 'red', marginBottom: '1rem' }}>{error}</p>
      )}

      <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
        <div>
          <label htmlFor="usernameOrEmail">Kullanıcı Adı veya Email</label>
          <br />
          <input
            id="usernameOrEmail"
            name="usernameOrEmail"
            type="text"
            value={form.usernameOrEmail}
            onChange={handleChange}
            required
            style={{ width: '100%', padding: '0.5rem', marginTop: '0.25rem' }}
          />
        </div>

        <div>
          <label htmlFor="password">Şifre</label>
          <br />
          <input
            id="password"
            name="password"
            type="password"
            value={form.password}
            onChange={handleChange}
            required
            style={{ width: '100%', padding: '0.5rem', marginTop: '0.25rem' }}
          />
        </div>
        <button type="submit" disabled={loading} style={{ padding: '0.6rem', cursor: 'pointer' }}>
          {loading ? 'Giriş yapılıyor...' : 'Giriş Yap'}
        </button>
      </form>

          <div
              style={{
                  display: 'grid',
                  gridTemplateColumns: '1fr auto 1fr',
                  alignItems: 'center',
                  columnGap: '0.75rem',
                  width: '100%',
                  marginTop: '1rem'
              }}
          >
              <span style={{ whiteSpace: 'nowrap' }}>
                  Hesabın yok mu? <Link to="/signup">Kayıt Ol</Link>
              </span>

              <span>|</span>

              <Link
                  to="/forgot-password"
                  style={{ justifySelf: 'end', whiteSpace: 'nowrap' }}
              >
                  Şifreni mi unuttun?
              </Link>
          </div>
    </div>
  );
}
