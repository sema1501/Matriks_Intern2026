import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getMe, updateProfile, changePassword } from '../../services/apiService';

const inputStyle = { width: '100%', padding: '0.5rem', marginTop: '0.25rem' };
const sectionStyle = { maxWidth: '500px', marginBottom: '2rem' };

function formatDate(dateStr) {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleDateString('tr-TR', {
    year: 'numeric', month: 'long', day: 'numeric',
  });
}

function getErrorMessage(err) {
  return err.response?.data?.error || err.response?.data?.message || 'Bir hata oluştu.';
}

export default function Profile() {
  const { user: authUser, loading: authLoading, setUser } = useAuth();

  const [profile, setProfile]       = useState(null);
  const [loading, setLoading]       = useState(true);
  const [profileForm, setProfileForm] = useState({ username: '', email: '' });
  const [passwordForm, setPasswordForm] = useState({ currentPassword: '', newPassword: '', confirmPassword: '' });
  const [profileLoading, setProfileLoading]   = useState(false);
  const [passwordLoading, setPasswordLoading] = useState(false);
  const [profileError, setProfileError]       = useState('');
  const [profileSuccess, setProfileSuccess]   = useState('');
  const [passwordError, setPasswordError]     = useState('');
  const [passwordSuccess, setPasswordSuccess] = useState('');

  useEffect(() => {
    if (authLoading) return;
    if (!authUser) { setLoading(false); return; }

    getMe()
      .then(res => {
        setProfile(res.data);
        setProfileForm({ username: res.data.username, email: res.data.email });
      })
      .catch(err => setProfileError(getErrorMessage(err)))
      .finally(() => setLoading(false));
  }, [authUser, authLoading]);

  const handleProfileChange = (e) => {
    setProfileForm({ ...profileForm, [e.target.name]: e.target.value });
  };

  const handlePasswordChange = (e) => {
    setPasswordForm({ ...passwordForm, [e.target.name]: e.target.value });
  };

  const handleProfileSubmit = async (e) => {
    e.preventDefault();
    setProfileError('');
    setProfileSuccess('');
    setProfileLoading(true);
    try {
      const res = await updateProfile({
        username: profileForm.username,
        email: profileForm.email,
      });
      setProfile(res.data);
      setUser(res.data);
      setProfileSuccess('Profil başarıyla güncellendi.');
    } catch (err) {
      setProfileError(getErrorMessage(err));
    } finally {
      setProfileLoading(false);
    }
  };

  const handlePasswordSubmit = async (e) => {
    e.preventDefault();
    setPasswordError('');
    setPasswordSuccess('');

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setPasswordError('Yeni şifreler eşleşmiyor.');
      return;
    }

    setPasswordLoading(true);
    try {
      await changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
      });
      setPasswordForm({ currentPassword: '', newPassword: '', confirmPassword: '' });
      setPasswordSuccess('Şifre başarıyla değiştirildi.');
    } catch (err) {
      setPasswordError(getErrorMessage(err));
    } finally {
      setPasswordLoading(false);
    }
  };

  if (authLoading || loading) {
    return <p>Profil yükleniyor...</p>;
  }

  if (!authUser) {
    return <p>Giriş yapmanız gerekiyor.</p>;
  }

  if (!profile) {
    return <p style={{ color: 'red' }}>{profileError || 'Profil bilgisi alınamadı.'}</p>;
  }

  return (
    <div style={{ maxWidth: '600px' }}>
      <h2>Profil</h2>

      <section style={sectionStyle}>
        <h3>Bilgilerim</h3>
        <p><strong>Kullanıcı Adı:</strong> {profile.username}</p>
        <p><strong>Email:</strong> {profile.email}</p>
        <p><strong>Roller:</strong> {profile.roles?.join(', ') || '—'}</p>
        <p><strong>Üyelik Tarihi:</strong> {formatDate(profile.createdAt)}</p>
      </section>

      <section style={sectionStyle}>
        <h3>Profili Düzenle</h3>
        {profileError && <p style={{ color: 'red' }}>{profileError}</p>}
        {profileSuccess && <p style={{ color: 'green' }}>{profileSuccess}</p>}
        <form onSubmit={handleProfileSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div>
            <label htmlFor="username">Kullanıcı Adı</label>
            <br />
            <input
              id="username"
              name="username"
              type="text"
              value={profileForm.username}
              onChange={handleProfileChange}
              required
              style={inputStyle}
            />
          </div>
          <div>
            <label htmlFor="email">Email</label>
            <br />
            <input
              id="email"
              name="email"
              type="email"
              value={profileForm.email}
              onChange={handleProfileChange}
              required
              style={inputStyle}
            />
          </div>
          <button type="submit" disabled={profileLoading} style={{ padding: '0.6rem', cursor: 'pointer' }}>
            {profileLoading ? 'Kaydediliyor...' : 'Kaydet'}
          </button>
        </form>
      </section>

      <section style={sectionStyle}>
        <h3>Şifre Değiştir</h3>
        {passwordError && <p style={{ color: 'red' }}>{passwordError}</p>}
        {passwordSuccess && <p style={{ color: 'green' }}>{passwordSuccess}</p>}
        <form onSubmit={handlePasswordSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div>
            <label htmlFor="currentPassword">Mevcut Şifre</label>
            <br />
            <input
              id="currentPassword"
              name="currentPassword"
              type="password"
              value={passwordForm.currentPassword}
              onChange={handlePasswordChange}
              required
              style={inputStyle}
            />
          </div>
          <div>
            <label htmlFor="newPassword">Yeni Şifre</label>
            <br />
            <input
              id="newPassword"
              name="newPassword"
              type="password"
              value={passwordForm.newPassword}
              onChange={handlePasswordChange}
              required
              style={inputStyle}
            />
          </div>
          <div>
            <label htmlFor="confirmPassword">Yeni Şifre Tekrar</label>
            <br />
            <input
              id="confirmPassword"
              name="confirmPassword"
              type="password"
              value={passwordForm.confirmPassword}
              onChange={handlePasswordChange}
              required
              style={inputStyle}
            />
          </div>
          <button type="submit" disabled={passwordLoading} style={{ padding: '0.6rem', cursor: 'pointer' }}>
            {passwordLoading ? 'Değiştiriliyor...' : 'Şifreyi Değiştir'}
          </button>
        </form>
      </section>
    </div>
  );
}
