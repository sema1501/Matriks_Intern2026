import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { login } from '../../services/apiService';

export default function SignIn() {
    const { loginUser } = useAuth();
    const { t } = useLanguage();
    const navigate = useNavigate();

    const [form, setForm] = useState({ usernameOrEmail: '', password: '' });
    const [errors, setErrors] = useState({ usernameOrEmail: '', password: '', global: '' });
    const [loading, setLoading] = useState(false);
    const [showPassword, setShowPassword] = useState(false);

    const handleChange = (e) => {
        setForm({ ...form, [e.target.name]: e.target.value });
        setErrors({ ...errors, [e.target.name]: '', global: '' });
    };

    const validateForm = () => {
        let isValid = true;
        let localErrors = { usernameOrEmail: '', password: '', global: '' };

        if (!form.usernameOrEmail.trim()) {
            localErrors.usernameOrEmail = t('auth.usernameOrEmailRequired');
            isValid = false;
        }
        if (!form.password) {
            localErrors.password = t('auth.passwordRequired');
            isValid = false;
        } else if (form.password.length < 6) {
            localErrors.password = t('auth.passwordMinLength');
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
            const res = await login(form);
            loginUser(res.data.token, res.data);
            navigate('/');
        } catch (err) {
            setErrors({
                ...errors,
                global: err.response?.data?.message || t('auth.loginFailed')
            });
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="auth-container">
            <div className="auth-card">
                <h2 className="auth-title">{t('auth.signinTitle')}</h2>

                {errors.global && (
                    <div className="auth-error-alert">{errors.global}</div>
                )}

                <form onSubmit={handleSubmit} className="auth-form">
                    <div className="form-group">
                        <label htmlFor="usernameOrEmail">{t('auth.usernameOrEmail')}</label>
                        <input
                            id="usernameOrEmail"
                            name="usernameOrEmail"
                            type="text"
                            className={`form-input ${errors.usernameOrEmail ? 'input-error' : ''}`}
                            value={form.usernameOrEmail}
                            onChange={handleChange}
                            placeholder={t('auth.usernameOrEmailPlaceholder')}
                        />
                        {errors.usernameOrEmail && <span className="error-text">{errors.usernameOrEmail}</span>}
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
                                placeholder={t('auth.passwordPlaceholder')}
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

                    <button type="submit" disabled={loading} className="auth-submit-btn">
                        {loading ? <span className="spinner"></span> : t('auth.signinTitle')}
                    </button>
                </form>

                <div className="auth-footer-links">
                    <span>
                        {t('auth.noAccount')} <Link to="/signup" className="auth-link">{t('auth.signupTitle')}</Link>
                    </span>
                    <span className="divider">|</span>
                    <Link to="/forgot-password" className="auth-link">
                        {t('auth.forgotPassword')}
                    </Link>
                </div>
            </div>
        </div>
    );
}