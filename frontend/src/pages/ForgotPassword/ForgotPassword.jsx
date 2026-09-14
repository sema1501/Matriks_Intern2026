import { useState } from 'react';
import { Link } from 'react-router-dom';
import { forgotPassword } from '../../services/apiService';
import { useLanguage } from '../../context/LanguageContext';

export default function ForgotPassword() {
    const { t } = useLanguage();
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
            setError(err.response?.data?.error || t('forgot.error'));
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: '400px', margin: '80px auto', padding: '2rem' }}>
            <h2>{t('forgot.title')}</h2>

            <p>{t('forgot.subtitle')}</p>

            {message && (
                <p style={{ color: 'green', marginBottom: '1rem' }}>{message}</p>
            )}

            {error && (
                <p style={{ color: 'red', marginBottom: '1rem' }}>{error}</p>
            )}

            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <div>
                    <label htmlFor="email">{t('signup.email')}</label>
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
                    {loading ? t('forgot.sending') : t('forgot.submit')}
                </button>
            </form>

            <p style={{ marginTop: '1rem' }}>
                <Link to="/signin">{t('signup.backToSignin')}</Link>
            </p>
        </div>
    );
}