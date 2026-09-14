import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useLanguage } from '../../context/LanguageContext';
import { register } from '../../services/apiService';

export default function SignUp() {
    const { t } = useLanguage();
    const [form, setForm] = useState({
        username: '',
        email: '',
        password: '',
        confirmPassword: '',
    });

    const [errors, setErrors] = useState({
        username: '',
        email: '',
        password: '',
        confirmPassword: '',
        global: ''
    });

    const [loading, setLoading] = useState(false);
    const [showPassword, setShowPassword] = useState(false);
    const [showConfirmPassword, setShowConfirmPassword] = useState(false);
    const [successMessage, setSuccessMessage] = useState(''); // Görev 48: e-posta doğrulama bildirimi

    const handleChange = (e) => {
        setForm({ ...form, [e.target.name]: e.target.value });
        setErrors({ ...errors, [e.target.name]: '', global: '' });
    };

    const validateForm = () => {
        let isValid = true;
        let localErrors = { username: '', email: '', password: '', confirmPassword: '', global: '' };

        if (!form.username.trim()) {
            localErrors.username = t('signup.usernameRequired');
            isValid = false;
        }

        if (!form.email.trim()) {
            localErrors.email = t('signup.emailRequired');
            isValid = false;
        } else if (!/\S+@\S+\.\S+/.test(form.email)) {
            localErrors.email = t('signup.emailInvalid');
            isValid = false;
        }

        if (!form.password) {
            localErrors.password = t('auth.passwordRequired');
            isValid = false;
        } else if (form.password.length < 6) {
            localErrors.password = t('auth.passwordMinLength');
            isValid = false;
        }

        if (!form.confirmPassword) {
            localErrors.confirmPassword = t('signup.confirmPasswordRequired');
            isValid = false;
        }

        if (form.password && form.confirmPassword && form.password !== form.confirmPassword) {
            localErrors.confirmPassword = t('signup.passwordsMismatch');
            isValid = false;
        }

        setErrors(localErrors);
        return isValid;
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (!validateForm()) return;

        setLoading(true);
        try {
            const res = await register({
                username: form.username,
                email: form.email,
                password: form.password,
            });
            // Kayıt sonrası otomatik giriş yok; kullanıcı e-postasını doğrulamalı (Görev 48).
            setSuccessMessage(res.data?.message || t('signup.successMessage'));
        } catch (err) {
            setErrors({
                ...errors,
                global: err.response?.data?.message || t('signup.registerFailed')
            });
        } finally {
            setLoading(false);
        }
    };

    // Kayıt başarılıysa formu gizle, e-posta doğrulama bildirimini göster (Görev 48).
    if (successMessage) {
        return (
            <div className="auth-container">
                <div className="auth-card">
                    <h2 className="auth-title">{t('auth.signupTitle')}</h2>
                    <div className="auth-success-alert" style={{ color: 'green', marginBottom: '1rem' }}>
                        {successMessage}
                    </div>
                    <Link to="/signin" className="auth-link">{t('signup.backToSignin')}</Link>
                </div>
            </div>
        );
    }

    return (
        <div className="auth-container">
            <div className="auth-card">
                <h2 className="auth-title">Kayıt Ol</h2>

                {errors.global && (
                    <div className="auth-error-alert">{errors.global}</div>
                )}

                <form onSubmit={handleSubmit} className="auth-form">
                    <div className="form-group">
                        <label htmlFor="username">{t('signup.username')}</label>
                        <input
                            id="username"
                            name="username"
                            type="text"
                            className={`form-input ${errors.username ? 'input-error' : ''}`}
                            value={form.username}
                            onChange={handleChange}
                            placeholder={t('signup.usernamePlaceholder')}
                        />
                        {errors.username && <span className="error-text">{errors.username}</span>}
                    </div>

                    <div className="form-group">
                        <label htmlFor="email">{t('signup.email')}</label>
                        <input
                            id="email"
                            name="email"
                            type="email"
                            className={`form-input ${errors.email ? 'input-error' : ''}`}
                            value={form.email}
                            onChange={handleChange}
                            placeholder={t('signup.emailPlaceholder')}
                        />
                        {errors.email && <span className="error-text">{errors.email}</span>}
                    </div>

                    <div className="form-group">
                        <label htmlFor="password">{t('auth.password')}</label>
                        <div className="password-input-wrapper">
                            <input
                                id="password"
                                name="password"
                                type={showPassword ? "text" : "password"}
                                className={`form-input ${errors.password ? 'input-error' : ''}`}
                                value={form.password}
                                onChange={handleChange}
                                placeholder={t('signup.passwordPlaceholder')}
                            />
                            <button
                                type="button"
                                className="password-toggle-btn"
                                onClick={() => setShowPassword(!showPassword)}
                                tabIndex="-1"
                            >
                                {showPassword ? '👁️' : '👁️‍🗨️'}
                            </button>
                        </div>
                        {errors.password && <span className="error-text">{errors.password}</span>}
                    </div>

                    <div className="form-group">
                        <label htmlFor="confirmPassword">{t('signup.confirmPassword')}</label>
                        <div className="password-input-wrapper">
                            <input
                                id="confirmPassword"
                                name="confirmPassword"
                                type={showConfirmPassword ? "text" : "password"}
                                className={`form-input ${errors.confirmPassword ? 'input-error' : ''}`}
                                value={form.confirmPassword}
                                onChange={handleChange}
                                placeholder={t('signup.confirmPasswordPlaceholder')}
                            />
                            <button
                                type="button"
                                className="password-toggle-btn"
                                onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                                tabIndex="-1"
                            >
                                {showConfirmPassword ? '👁️' : '👁️‍🗨️'}
                            </button>
                        </div>
                        {errors.confirmPassword && <span className="error-text">{errors.confirmPassword}</span>}
                    </div>

                    <button type="submit" disabled={loading} className="auth-submit-btn">
                        {loading ? <span className="spinner"></span> : t('auth.signupTitle')}
                    </button>
                </form>

                <div className="auth-footer-links">
                    <span>
                        Zaten hesabın var mı? <Link to="/signin" className="auth-link">Giriş Yap</Link>
                    </span>
                </div>
            </div>
        </div>
    );
}