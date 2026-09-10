import { useEffect, useState, useRef } from 'react';
import { useParams, Link } from 'react-router-dom';
import { confirmEmail } from '../../services/apiService';

// Görev 48: Kayıt e-postasındaki doğrulama bağlantısı bu sayfayı açar.
export default function ConfirmEmail() {
    const { token } = useParams();
    const [status, setStatus] = useState('loading'); // 'loading' | 'success' | 'error'
    const [message, setMessage] = useState('');
    const calledRef = useRef(false);

    useEffect(() => {
        if (calledRef.current) return; // Strict Mode'da iki kez çağrılmasını önle
        calledRef.current = true;

        confirmEmail(token)
            .then((res) => {
                setStatus('success');
                setMessage(res.data?.message || 'E-posta adresiniz doğrulandı. Artık giriş yapabilirsiniz.');
            })
            .catch((err) => {
                setStatus('error');
                setMessage(
                    err.response?.data?.error ||
                    err.response?.data?.message ||
                    'Doğrulama başarısız. Bağlantı geçersiz veya süresi dolmuş olabilir.'
                );
            });
    }, [token]);

    return (
        <div className="auth-container">
            <div className="auth-card">
                <h2 className="auth-title">E-posta Doğrulama</h2>

                {status === 'loading' && <p>Doğrulanıyor, lütfen bekleyin...</p>}

                {status === 'success' && (
                    <>
                        <div className="auth-success-alert" style={{ color: 'green', marginBottom: '1rem' }}>
                            {message}
                        </div>
                        <Link to="/signin" className="auth-link">Giriş yap</Link>
                    </>
                )}

                {status === 'error' && (
                    <>
                        <div className="auth-error-alert" style={{ marginBottom: '1rem' }}>
                            {message}
                        </div>
                        <Link to="/signup" className="auth-link">Yeniden kayıt ol</Link>
                    </>
                )}
            </div>
        </div>
    );
}
