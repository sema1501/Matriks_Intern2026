import { useState } from 'react';
import { Link } from 'react-router-dom';
import { forgotPassword } from '../../services/apiService';

export default function ForgotPassword() {
    const [email, setEmail] = useState('');
    const [message, setMessage] = useState('');
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    const handleSubmit = async (e) => {
        e.preventDefault();
        setMessage('');
        setError('');
        setLoading(true);

        try {
            const res = await forgotPassword({ email });
            setMessage(res.data.message);
        } catch (err) {
            setError(err.response?.data?.error || 'İşlem başarısız oldu.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: '400px', margin: '80px auto', padding: '2rem' }}>
            <h2>Şifremi Unuttum</h2>

            <p>Şifre sıfırlama bağlantısı oluşturmak için e-posta adresinizi girin.</p>

            {message && (
                <p style={{ color: 'green', marginBottom: '1rem' }}>{message}</p>
            )}

            {error && (
                <p style={{ color: 'red', marginBottom: '1rem' }}>{error}</p>
            )}

            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <div>
                    <label htmlFor="email">Email</label>
                    <br />
                    <input
                        id="email"
                        name="email"
                        type="email"
                        value={email}
                        onChange={(e) => setEmail(e.target.value)}
                        required
                        style={{ width: '100%', padding: '0.5rem', marginTop: '0.25rem' }}
                    />
                </div>

                <button type="submit" disabled={loading} style={{ padding: '0.6rem', cursor: 'pointer' }}>
                    {loading ? 'Gönderiliyor...' : 'Sıfırlama Bağlantısı Oluştur'}
                </button>
            </form>

            <p style={{ marginTop: '1rem' }}>
                <Link to="/signin">Giriş sayfasına dön</Link>
            </p>
        </div>
    );
}