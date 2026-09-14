import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { resetPassword } from '../../services/apiService';
import { useLanguage } from '../../context/LanguageContext';

export default function ResetPassword() {
    const { token } = useParams();
    const { t } = useLanguage();
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
            setError(t('reset.mismatch'));
            return;
        }

        setLoading(true);

        try {
            await resetPassword({ token, newPassword: form.newPassword });
            setMessage(t('reset.success'));

            setTimeout(() => {
                navigate('/signin', { replace: true });
            }, 1500);
        } catch (err) {
            setError(err.response?.data?.error || t('forgot.error'));
        } finally {
            setLoading(false);
        }
    };

    return (
        <div style={{ maxWidth: '400px', margin: '80px auto', padding: '2rem' }}>
            <h2>{t('reset.title')}</h2>

            {message && (
                <p style={{ color: 'green', marginBottom: '1rem' }}>{message}</p>
            )}

            {error && (
                <p style={{ color: 'red', marginBottom: '1rem' }}>{error}</p>
            )}

            <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <div>
                    <label htmlFor="newPassword">{t('reset.newPassword')}</label>
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
                    <label htmlFor="confirmPassword">{t('reset.confirmPassword')}</label>
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
                    {loading ? t('common.loading') : t('reset.submit')}
                </button>
            </form>

            <p style={{ marginTop: '1rem' }}>
                <Link to="/signin">{t('signup.backToSignin')}</Link>
            </p>
        </div>
    );
}