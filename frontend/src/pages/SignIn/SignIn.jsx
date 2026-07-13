import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { login } from '../../services/apiService';

export default function SignIn() {
    const { loginUser } = useAuth();
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
            localErrors.usernameOrEmail = 'Kullanıcı adı veya e-posta alanı boş bırakılamaz.';
            isValid = false;
        }
        if (!form.password) {
            localErrors.password = 'Şifre alanı boş bırakılamaz.';
            isValid = false;
        } else if (form.password.length < 6) {
            localErrors.password = 'Şifre en az 6 karakter olmalıdır.';
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
                global: err.response?.data?.message || 'Giriş başarısız. Bilgilerinizi kontrol edin.'
            });
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="auth-container">
            <div className="auth-card">
                <h2 className="auth-title">Giriş Yap</h2>

                {errors.global && (
                    <div className="auth-error-alert">{errors.global}</div>
                )}

                <form onSubmit={handleSubmit} className="auth-form">
                    <div className="form-group">
                        <label htmlFor="usernameOrEmail">Kullanıcı Adı veya Email</label>
                        <input
                            id="usernameOrEmail"
                            name="usernameOrEmail"
                            type="text"
                            className={`form-input ${errors.usernameOrEmail ? 'input-error' : ''}`}
                            value={form.usernameOrEmail}
                            onChange={handleChange}
                            placeholder="Kullanıcı adınızı veya e-postanızı girin"
                        />
                        {errors.usernameOrEmail && <span className="error-text">{errors.usernameOrEmail}</span>}
                    </div>

                    <div className="form-group">
                        <label htmlFor="password">Şifre</label>
                        <div className="password-input-wrapper">
                            <input
                                id="password"
                                name="password"
                                type={showPassword ? "text" : "password"}
                                className={`form-input ${errors.password ? 'input-error' : ''}`}
                                value={form.password}
                                onChange={handleChange}
                                placeholder="Şifrenizi girin"
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
                        {loading ? <span className="spinner"></span> : 'Giriş Yap'}
                    </button>
                </form>

                <div className="auth-footer-links">
                    <span>
                        Hesabın yok mu? <Link to="/signup" className="auth-link">Kayıt Ol</Link>
                    </span>
                    <span className="divider">|</span>
                    <Link to="/forgot-password" className="auth-link">
                        Şifreni mi unuttun?
                    </Link>
                </div>
            </div>
        </div>
    );
}