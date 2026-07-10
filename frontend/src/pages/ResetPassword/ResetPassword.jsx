import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { resetPassword } from '../../services/apiService';

export default function ResetPassword() {
    const { token } = useParams();
    const navigate = useNavigate();

    const [form, setForm] = useState({ newPassword: '', confirmPassword: '' });
    const [error, setError] = useState('');
    const [message, setMessage] = useState('');
    const [loading, setLoading] = useState(false);

    const handleChange = (e) => {
        setForm({ ...form, [e.target.name]: e.target.value });
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setMessage('');

        if (form.newPassword !== form.confirmPassword) {
            setError('Şifreler eşleşmiyor.');
            return;
        }

        setLoading(true);

        try {
            await resetPassword({ token, newPassword: form.newPassword });
            setMessage('Şifreniz başarıyla değiştirildi. Giriş sayfasına yönlendiriliyorsunuz.');

            setTimeout(() => {
                navigate('/signin', { replace: true });
            }, 1500);
        } catch (err) {
            setError(err.response?.data?.error || 'Şifre değiştirme işlemi başarısız oldu.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: '400px', margin: '80px auto', padding: '2rem' }}>
            <h2>Yeni Şifre Belirle</h2>

            {message && (
                <p style={{ color: 'green', marginBottom: '1rem' }}>{message}</p>
            )}

            {error && (
                <p style={{ color: 'red', marginBottom: '1rem' }}>{error}</p>
            )}

            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <div>
                    <label htmlFor="newPassword">Yeni Şifre</label>
                    <br />
                    <input
                        id="newPassword"
                        name="newPassword"
                        type="password"
                        value={form.newPassword}
                        onChange={handleChange}
                        required
                        style={{ width: '100%', padding: '0.5rem', marginTop: '0.25rem' }}
                    />
                </div>

                <div>
                    <label htmlFor="confirmPassword">Yeni Şifre Tekrar</label>
                    <br />
                    <input
                        id="confirmPassword"
                        name="confirmPassword"
                        type="password"
                        value={form.confirmPassword}
                        onChange={handleChange}
                        required
                        style={{ width: '100%', padding: '0.5rem', marginTop: '0.25rem' }}
                    />
                </div>

                <button type="submit" disabled={loading} style={{ padding: '0.6rem', cursor: 'pointer' }}>
                    {loading ? 'Değiştiriliyor...' : 'Şifreyi Değiştir'}
                </button>
            </form>

            <p style={{ marginTop: '1rem' }}>
                <Link to="/signin">Giriş sayfasına dön</Link>
            </p>
        </div>
    );
}