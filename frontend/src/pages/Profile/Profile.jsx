import { useEffect, useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { usePollingFallback, useRealtimeEvent } from '../../context/RealtimeContext';
import {
  getMe,
  updateProfile,
  changePassword,
  getAlerts,
  deleteAlert,
  toggleAlert,
  getAlertSignals,
  setEmailNotifications,
  setAvatar,
  endSession,
} from '../../services/apiService';

const inputStyle = { width: '100%', padding: '0.5rem', marginTop: '0.25rem' };
const sectionStyle = { maxWidth: '560px', marginBottom: '2rem' };
const ALERT_POLL_MS = 45_000;

function formatDate(dateStr, locale) {
  if (!dateStr) return '—';
  return new Date(dateStr).toLocaleDateString(locale, {
    year: 'numeric', month: 'long', day: 'numeric',
  });
}

function formatDateTime(dateStr, t, locale) {
  if (!dateStr) return t('profile.noSignalYet');
  return new Date(dateStr).toLocaleString(locale);
}

function getErrorMessage(err, t) {
  return err.response?.data?.error || err.response?.data?.message || t('common.error');
}

function formatDirection(direction, t) {
  if (direction === 0 || direction === '0') return t('profile.direction.above');
  if (direction === 1 || direction === '1') return t('profile.direction.below');
  const d = String(direction || '').toLowerCase();
  if (d === 'above') return t('profile.direction.above');
  if (d === 'below') return t('profile.direction.below');
  return direction || '—';
}

function formatInterval(interval, t) {
  if (interval === 0 || interval === '0') return t('coin.minutely');
  if (interval === 1 || interval === '1') return t('coin.hourly');
  if (interval === 2 || interval === '2') return t('coin.daily');
  return '—';
}

function getAlertField(alert, camel, pascal) {
  return alert[camel] ?? alert[pascal];
}

export default function Profile() {
  const { user: authUser, loading: authLoading, setUser } = useAuth();
  const { t, locale } = useLanguage();

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
  const [alerts, setAlerts]                   = useState([]);
  const [alertsLoading, setAlertsLoading]     = useState(false);
  const [alertsError, setAlertsError]         = useState('');
  const [alertsActionMessage, setAlertsActionMessage] = useState('');
  const [deletingId, setDeletingId]           = useState(null);
  const [togglingId, setTogglingId]           = useState(null);
  const [expandedSignalAlertId, setExpandedSignalAlertId] = useState(null);
  const [signalsByAlertId, setSignalsByAlertId] = useState({});
  const [signalsLoadingId, setSignalsLoadingId] = useState(null);
  const [signalsError, setSignalsError] = useState('');
  const [notifLoading, setNotifLoading] = useState(false);
  const [notifError, setNotifError]     = useState('');
  const [avatarLoading, setAvatarLoading] = useState(false);
  const [avatarError, setAvatarError]     = useState('');

  useEffect(() => {
    if (authLoading) return;
    if (!authUser) { setLoading(false); return; }

    getMe()
      .then(res => {
        setProfile(res.data);
        setProfileForm({ username: res.data.username, email: res.data.email });
      })
      .catch(err => setProfileError(getErrorMessage(err, t)))
      .finally(() => setLoading(false));
  }, [authUser, authLoading]);

  const loadAlerts = (silent = false) => {
    if (!authUser) return;
    if (!silent) {
      setAlertsLoading(true);
      setAlertsError('');
    }
    getAlerts()
      .then(res => setAlerts(Array.isArray(res.data) ? res.data : []))
      .catch(err => {
        if (err.response?.status === 401 || err.response?.status === 403) {
          setAlertsError(t('profile.alertsLoginRequired'));
        } else if (!silent) {
          setAlertsError(t('profile.alertsLoadError'));
        }
      })
      .finally(() => {
        if (!silent) setAlertsLoading(false);
      });
  };

  useEffect(() => {
    if (authLoading || !authUser) return;
    loadAlerts();
  }, [authUser, authLoading]);

  useEffect(() => {
    const handleAlertsChanged = () => loadAlerts(true);
    window.addEventListener('alerts-changed', handleAlertsChanged);
    return () => window.removeEventListener('alerts-changed', handleAlertsChanged);
  }, [authUser]);

  // Alarm tetiklenince sayaç ve son tetiklenme anında güncellenir; bağlantı yokken polling devam eder (#102).
  usePollingFallback(() => loadAlerts(true), ALERT_POLL_MS, !authLoading && Boolean(authUser));
  useRealtimeEvent('alertTriggered', () => loadAlerts(true));

  const handleDeleteAlert = async (id) => {
    setDeletingId(id);
    setAlertsError('');
    setAlertsActionMessage('');
    try {
      await deleteAlert(id);
      setAlerts(prev => prev.filter(a => getAlertField(a, 'id', 'Id') !== id));
      if (expandedSignalAlertId === id) {
        setExpandedSignalAlertId(null);
      }
      window.dispatchEvent(new Event('alerts-changed'));
      setAlertsActionMessage(t('profile.alertDeleted'));
    } catch (err) {
      setAlertsError(getErrorMessage(err, t));
    } finally {
      setDeletingId(null);
    }
  };

  const handleToggleAlert = async (alert) => {
    const id = getAlertField(alert, 'id', 'Id');
    const isActive = getAlertField(alert, 'isActive', 'IsActive');
    const nextActive = !(isActive === true || isActive === 'true');
    setTogglingId(id);
    setAlertsError('');
    setAlertsActionMessage('');
    try {
      const res = await toggleAlert(id, nextActive);
      setAlerts(prev => prev.map(a => (getAlertField(a, 'id', 'Id') === id ? res.data : a)));
      window.dispatchEvent(new Event('alerts-changed'));
      setAlertsActionMessage(nextActive ? t('profile.alertActivated') : t('profile.alertDeactivated'));
    } catch (err) {
      setAlertsError(getErrorMessage(err, t));
    } finally {
      setTogglingId(null);
    }
  };

  const handleToggleSignals = async (alertId) => {
    if (expandedSignalAlertId === alertId) {
      setExpandedSignalAlertId(null);
      setSignalsError('');
      return;
    }

    setExpandedSignalAlertId(alertId);
    setSignalsLoadingId(alertId);
    setSignalsError('');
    try {
      const res = await getAlertSignals(alertId);
      setSignalsByAlertId(prev => ({ ...prev, [alertId]: res.data }));
    } catch (err) {
      setSignalsError(getErrorMessage(err, t));
    } finally {
      setSignalsLoadingId(null);
    }
  };

  const handleToggleNotifications = async () => {
    const next = !profile.emailNotificationsEnabled;
    setNotifLoading(true);
    setNotifError('');
    try {
      const res = await setEmailNotifications(next);
      setProfile(res.data);
    } catch (err) {
      setNotifError(getErrorMessage(err, t));
    } finally {
      setNotifLoading(false);
    }
  };

  // Görev 43: Seçilen fotoğrafı base64'e çevirip yükler.
  const handleAvatarChange = (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      setAvatarError(t('profile.avatarNotImage'));
      return;
    }
    if (file.size > 3 * 1024 * 1024) {
      setAvatarError(t('profile.avatarTooLarge'));
      return;
    }

    setAvatarError('');
    setAvatarLoading(true);

    const reader = new FileReader();
    reader.onload = async () => {
      try {
        const res = await setAvatar(reader.result);
        setProfile(res.data);
        setUser(res.data);
      } catch (err) {
        setAvatarError(getErrorMessage(err, t));
      } finally {
        setAvatarLoading(false);
      }
    };
    reader.onerror = () => {
      setAvatarError(t('profile.avatarReadError'));
      setAvatarLoading(false);
    };
    reader.readAsDataURL(file);
  };

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
      setProfileSuccess(t('profile.updated'));
    } catch (err) {
      setProfileError(getErrorMessage(err, t));
    } finally {
      setProfileLoading(false);
    }
  };

  const handlePasswordSubmit = async (e) => {
    e.preventDefault();
    setPasswordError('');
    setPasswordSuccess('');

    if (passwordForm.newPassword !== passwordForm.confirmPassword) {
      setPasswordError(t('profile.passwordMismatch'));
      return;
    }

    setPasswordLoading(true);
    try {
      await changePassword({
        currentPassword: passwordForm.currentPassword,
        newPassword: passwordForm.newPassword,
      });
      // Backend tüm oturumları kapattı; kullanıcı yeni şifresiyle tekrar giriş yapmalı.
      endSession('auth.passwordChangedRelogin');
      return;
    } catch (err) {
      setPasswordError(getErrorMessage(err, t));
    } finally {
      setPasswordLoading(false);
    }
  };

  if (authLoading || loading) {
    return <p>{t('common.loading')}</p>;
  }

  if (!authUser) {
    return <p>{t('profile.loginRequired')}</p>;
  }

  if (!profile) {
    return <p style={{ color: 'red' }}>{profileError || t('profile.loadError')}</p>;
  }

  return (
    <div style={{ maxWidth: '900px' , margin: '0 auto'}}>
      <h2>{t('profile.title')}</h2>

      <section style={sectionStyle}>
        <h3>{t('profile.info')}</h3>

        {/* Profil kartı: bilgiler solda, avatar sağda (Görev 43) */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', gap: '1.5rem', flexWrap: 'wrap', marginBottom: '1rem' }}>
          {/* Sol: bilgiler */}
          <div>
            <p><strong>{t('profile.username')}:</strong> {profile.username}</p>
            <p><strong>{t('profile.email')}:</strong> {profile.email}</p>
            <p><strong>{t('profile.roles')}:</strong> {profile.roles?.join(', ') || '—'}</p>
            <p><strong>{t('profile.memberSince')}:</strong> {formatDate(profile.createdAt, locale)}</p>
          </div>

          {/* Sağ: avatar + fotoğraf yükle */}
          <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.5rem', flexShrink: 0, marginTop: '-48px' }}>
            {profile.avatarUrl ? (
              <img
                src={profile.avatarUrl}
                alt={t('profile.photoAlt')}
                style={{ width: '120px', height: '120px', borderRadius: '50%', objectFit: 'cover', border: '1px solid var(--border-color, #e2e8f0)' }}
              />
            ) : (
              <div
                style={{
                  width: '120px', height: '120px', borderRadius: '50%',
                  background: '#cbd5e1', color: '#fff', display: 'flex',
                  alignItems: 'center', justifyContent: 'center', fontSize: '3rem', fontWeight: 600,
                }}
                aria-label={t('profile.defaultPhotoAlt')}
              >
                {profile.username?.[0]?.toUpperCase() || '?'}
              </div>
            )}
            <label style={{ display: 'inline-block', padding: '0.35rem 0.7rem', border: '1px solid var(--border-color, #94a3b8)', borderRadius: '6px', cursor: avatarLoading ? 'default' : 'pointer', fontSize: '0.85rem' }}>
              {avatarLoading ? t('common.loading') : t('profile.uploadPhoto')}
              <input
                type="file"
                accept="image/*"
                onChange={handleAvatarChange}
                disabled={avatarLoading}
                style={{ display: 'none' }}
              />
            </label>
            {avatarError && <p style={{ color: 'red', margin: 0, fontSize: '0.8rem' }}>{avatarError}</p>}
          </div>
        </div>

        <div style={{ marginTop: '0.75rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
          <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', cursor: 'pointer' }}>
            <input
              type="checkbox"
              checked={!!profile.emailNotificationsEnabled}
              onChange={handleToggleNotifications}
              disabled={notifLoading}
            />
            <span>{t('profile.emailNotifications')}</span>
          </label>
          {notifLoading && <span style={{ fontSize: '0.85rem', color: 'var(--text-muted, #64748b)' }}>{t('common.loading')}</span>}
        </div>
        <p style={{ fontSize: '0.85rem', color: 'var(--text-muted, #64748b)', marginTop: '0.25rem' }}>
          {t('profile.emailHint')}
        </p>
        {notifError && <p style={{ color: 'red' }}>{notifError}</p>}
      </section>

      <section style={sectionStyle}>
        <h3>{t('profile.edit')}</h3>
        {profileError && <p style={{ color: 'red' }}>{profileError}</p>}
        {profileSuccess && <p style={{ color: 'green' }}>{profileSuccess}</p>}
        <form onSubmit={handleProfileSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div>
            <label htmlFor="username">{t('profile.username')}</label>
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
            <label htmlFor="email">{t('profile.email')}</label>
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
            {profileLoading ? t('common.loading') : t('profile.save')}
          </button>
        </form>
      </section>

      <section style={sectionStyle}>
        <h3>{t('profile.myAlerts')}</h3>
        {alertsLoading && <p>{t('common.loading')}</p>}
        {alertsError && <p style={{ color: 'red' }}>{alertsError}</p>}
        {alertsActionMessage && <p style={{ color: 'green' }}>{alertsActionMessage}</p>}
        {!alertsLoading && !alertsError && alerts.length === 0 && (
          <p>{t('profile.noAlerts')}</p>
        )}
        {!alertsLoading && alerts.length > 0 && (
          <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
            {alerts.map(alert => {
              const id = getAlertField(alert, 'id', 'Id');
              const symbol = getAlertField(alert, 'symbol', 'Symbol');
              const targetPrice = getAlertField(alert, 'targetPrice', 'TargetPrice');
              const direction = getAlertField(alert, 'direction', 'Direction');
              const interval = getAlertField(alert, 'interval', 'Interval');
              const alertType = getAlertField(alert, 'type', 'Type');
              const percentThreshold = getAlertField(alert, 'percentChangeThreshold', 'PercentChangeThreshold');
              const referencePrice = getAlertField(alert, 'referencePrice', 'ReferencePrice');
              const isPercent = alertType === 1 || alertType === '1';
              const isActive = getAlertField(alert, 'isActive', 'IsActive');
              const signalCount = Number(getAlertField(alert, 'signalCount', 'SignalCount') ?? 0);
              const lastTriggeredAt = getAlertField(alert, 'lastTriggeredAt', 'LastTriggeredAt');
              const active = isActive === true || isActive === 'true';
              const signalsPayload = signalsByAlertId[id];
              const signals = Array.isArray(signalsPayload?.signals)
                ? signalsPayload.signals
                : Array.isArray(signalsPayload?.Signals)
                  ? signalsPayload.Signals
                  : [];

              return (
                <li
                  key={id}
                  style={{
                    padding: '0.75rem',
                    border: '1px solid var(--border-color, #e2e8f0)',
                    borderRadius: '8px',
                    opacity: active ? 1 : 0.75,
                  }}
                >
                  <div style={{ display: 'flex', justifyContent: 'space-between', gap: '1rem', alignItems: 'flex-start' }}>
                    <div>
                      <strong>{symbol}</strong>
                      {!active && (
                        <span style={{ marginLeft: '0.5rem', fontSize: '0.85rem', color: 'var(--text-muted, #64748b)' }}>
                          ({t('profile.passive')})
                        </span>
                      )}
                      <br />
                      <span style={{ fontSize: '0.9rem', color: 'var(--text-muted, #64748b)' }}>
                        {isPercent
                          ? `${t('profile.target')}: %${Number(percentThreshold)} ${direction === 0 || direction === '0' ? t('profile.rise') : t('profile.fall')}` +
                            (referencePrice != null ? ` (${t('profile.reference')}: $${Number(referencePrice).toLocaleString(locale)})` : '')
                          : `${t('profile.target')}: $${Number(targetPrice).toLocaleString(locale)} — ${formatDirection(direction, t)}`}
                      </span>
                      <br />
                      <span style={{ fontSize: '0.85rem', color: 'var(--text-muted, #64748b)' }}>
                        {t('profile.interval')}: {formatInterval(interval, t)} · {t('profile.status')}: {active ? t('profile.active') : t('profile.passive')}
                      </span>
                      <br />
                      <span style={{ fontSize: '0.85rem', color: 'var(--text-muted, #64748b)' }}>
                        {t('profile.signal')}: {signalCount} · {t('profile.last')}: {formatDateTime(lastTriggeredAt, t, locale)}
                      </span>
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.4rem', flexShrink: 0 }}>
                      <button
                        type="button"
                        onClick={() => handleToggleAlert(alert)}
                        disabled={togglingId === id}
                        style={{ padding: '0.4rem 0.8rem', cursor: 'pointer' }}
                      >
                        {togglingId === id ? t('common.loading') : active ? t('profile.deactivate') : t('profile.activate')}
                      </button>
                      <button
                        type="button"
                        onClick={() => handleToggleSignals(id)}
                        style={{ padding: '0.4rem 0.8rem', cursor: 'pointer' }}
                      >
                        {expandedSignalAlertId === id ? t('profile.hideSignals') : t('profile.signalHistory')}
                      </button>
                      <button
                        type="button"
                        onClick={() => handleDeleteAlert(id)}
                        disabled={deletingId === id}
                        style={{ padding: '0.4rem 0.8rem', cursor: 'pointer' }}
                      >
                        {deletingId === id ? t('common.deleting') : t('common.delete')}
                      </button>
                    </div>
                  </div>

                  {expandedSignalAlertId === id && (
                    <div style={{ marginTop: '0.75rem', borderTop: '1px solid var(--border-color, #e2e8f0)', paddingTop: '0.75rem' }}>
                      {signalsLoadingId === id && <p>{t('common.loading')}</p>}
                      {signalsError && expandedSignalAlertId === id && (
                        <p style={{ color: 'red' }}>{signalsError}</p>
                      )}
                      {!signalsLoadingId && !signalsError && signals.length === 0 && (
                        <p style={{ fontSize: '0.9rem', color: 'var(--text-muted, #64748b)' }}>
                          {active ? t('profile.noSignalsYet') : t('profile.alertPassiveHint')}
                        </p>
                      )}
                      {!signalsLoadingId && signals.length > 0 && (
                        <>
                          <p style={{ fontSize: '0.85rem', marginBottom: '0.5rem' }}>
                            {t('profile.total')}: {signalsPayload?.totalCount ?? signalsPayload?.TotalCount ?? signals.length}
                          </p>
                          <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.35rem' }}>
                            {signals.map((signal) => {
                              const signalId = signal.id ?? signal.Id;
                              const price = Number(signal.priceAtTrigger ?? signal.PriceAtTrigger);
                              const at = signal.triggeredAt ?? signal.TriggeredAt;
                              return (
                                <li key={signalId} style={{ fontSize: '0.85rem' }}>
                                  ${price.toLocaleString(locale)} — {formatDateTime(at, t, locale)}
                                </li>
                              );
                            })}
                          </ul>
                        </>
                      )}
                    </div>
                  )}
                </li>
              );
            })}
          </ul>
        )}
      </section>

      <section style={sectionStyle}>
        <h3>{t('profile.changePassword')}</h3>
        {passwordError && <p style={{ color: 'red' }}>{passwordError}</p>}
        {passwordSuccess && <p style={{ color: 'green' }}>{passwordSuccess}</p>}
        <form onSubmit={handlePasswordSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
          <div>
            <label htmlFor="currentPassword">{t('profile.currentPassword')}</label>
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
            <label htmlFor="newPassword">{t('profile.newPassword')}</label>
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
            <label htmlFor="confirmPassword">{t('profile.confirmPassword')}</label>
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
            {passwordLoading ? t('profile.changing') : t('profile.changePasswordButton')}
          </button>
        </form>
      </section>
    </div>
  );
}
