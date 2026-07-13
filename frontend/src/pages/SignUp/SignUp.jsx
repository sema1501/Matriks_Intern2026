import { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { useAuth } from '../../context/AuthContext';
import { register } from '../../services/apiService';

export default function SignUp() {
    const { loginUser } = useAuth();
    const navigate = useNavigate();

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

    const handleChange = (e) => {
        setForm({ ...form, [e.target.name]: e.target.value });
        setErrors({ ...errors, [e.target.name]: '', global: '' });
    };

    const validateForm = () => {
        let isValid = true;
        let localErrors = { username: '', email: '', password: '', confirmPassword: '', global: '' };

        if (!form.username.trim()) {
            localErrors.username = 'Kullanıcı adı alanı boş bırakılamaz.';
            isValid = false;
        }

        if (!form.email.trim()) {
            localErrors.email = 'Email alanı boş bırakılamaz.';
            isValid = false;
        } else if (!/\S+@\S+\.\S+/.test(form.email)) {
            localErrors.email = 'Geçerli bir e-posta adresi giriniz.';
            isValid = false;
        }

        if (!form.password) {
            localErrors.password = 'Şifre alanı boş bırakılamaz.';
            isValid = false;
        } else if (form.password.length < 6) {
            localErrors.password = 'Şifre en az 6 karakter olmalıdır.';
            isValid = false;
        }

        if (!form.confirmPassword) {
            localErrors.confirmPassword = 'Şifre tekrar alanı boş bırakılamaz.';
            isValid = false;
        }

        if (form.password && form.confirmPassword && form.password !== form.confirmPassword) {
            localErrors.confirmPassword = 'Şifreler eşleşmiyor.';
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
            loginUser(res.data.token, res.data);
            navigate('/');
        } catch (err) {
            setErrors({
                ...errors,
                global: err.response?.data?.message || 'Kayıt başarısız. Lütfen tekrar deneyin.'
            });
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="auth-container">
            <div className="auth-card">
                <h2 className="auth-title">Kayıt Ol</h2>

                {errors.global && (
                    <div className="auth-error-alert">{errors.global}</div>
                )}

                <form onSubmit={handleSubmit} className="auth-form">
                    <div className="form-group">
                        <label htmlFor="username">Kullanıcı Adı</label>
                        <input
                            id="username"
                            name="username"
                            type="text"
                            className={`form-input ${errors.username ? 'input-error' : ''}`}
                            value={form.username}
                            onChange={handleChange}
                            placeholder="Kullanıcı adınızı belirleyin"
                        />
                        {errors.username && <span className="error-text">{errors.username}</span>}
                    </div>

                    <div className="form-group">
                        <label htmlFor="email">Email</label>
                        <input
                            id="email"
                            name="email"
                            type="email"
                            className={`form-input ${errors.email ? 'input-error' : ''}`}
                            value={form.email}
                            onChange={handleChange}
                            placeholder="E-posta adresinizi girin"
                        />
                        {errors.email && <span className="error-text">{errors.email}</span>}
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
                                placeholder="Güçlü bir şifre girin"
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
                        <label htmlFor="confirmPassword">Şifre Tekrar</label>
                        <div className="password-input-wrapper">
                            <input
                                id="confirmPassword"
                                name="confirmPassword"
                                type={showConfirmPassword ? "text" : "password"}
                                className={`form-input ${errors.confirmPassword ? 'input-error' : ''}`}
                                value={form.confirmPassword}
                                onChange={handleChange}
                                placeholder="Şifrenizi tekrar girin"
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
                        {loading ? <span className="spinner"></span> : 'Kayıt Ol'}
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