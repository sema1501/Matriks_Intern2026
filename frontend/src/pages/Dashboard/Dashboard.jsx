import React, { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { useLanguage } from '../../context/LanguageContext';
import { getAllUsers, assignRole, removeRole, getRoles, getFeedbacks, getAuditLog } from '../../services/apiService';
import './Dashboard.css';

function parseApiUtcDate(value) {
  if (!value) return null;
  const text = String(value);
  const hasTimezone = /Z|[+-]\d{2}:?\d{2}$/.test(text);
  return new Date(hasTimezone ? text : `${text}Z`);
}

function formatAuditDate(value, locale) {
  const date = parseApiUtcDate(value);
  if (!date || Number.isNaN(date.getTime())) return '-';
  return date.toLocaleString(locale);
}

function localDateStartUtcIso(dateStr) {
  if (!dateStr) return undefined;
  return new Date(`${dateStr}T00:00:00`).toISOString();
}

function localDateEndUtcIso(dateStr) {
  if (!dateStr) return undefined;
  return new Date(`${dateStr}T23:59:59.999`).toISOString();
}

const AUDIT_ACTION_KEYS = {
  BotForceStopped: 'dashboard.audit.botForceStopped',
  BotFlagged: 'dashboard.audit.botFlagged'
};

export default function Dashboard() {
  const { user, loading } = useAuth();
  const { t, locale } = useLanguage();
  const [users, setUsers] = useState([]);
  const [searchTerm, setSearchTerm] = useState('');
  const [rolesList, setRolesList] = useState([]);
  const [activeTab, setActiveTab] = useState('summary');
  const [selectedRole, setSelectedRole] = useState('');
  const [feedbacks, setFeedbacks] = useState([]);
  const [auditLogs, setAuditLogs] = useState([]);
  const [auditLoading, setAuditLoading] = useState(false);
  const [auditError, setAuditError] = useState(null);
  const [auditFrom, setAuditFrom] = useState('');
  const [auditTo, setAuditTo] = useState('');

  const fetchUsers = () => {
    getAllUsers()
      .then(res => setUsers(res.data))
      .catch(err => console.error("Kullanıcılar çekilemedi:", err));
  };

  const fetchRoles = () => {
    getRoles()
      .then(res => setRolesList(res.data))
      .catch(() => {});
  };

  const fetchFeedbacks = () => {
    getFeedbacks()
      .then(res => setFeedbacks(res.data))
      .catch(err => console.error("Geri bildirimler çekilemedi:", err));
  };

  const fetchAuditLogs = () => {
    if (auditFrom && auditTo && auditFrom > auditTo) {
      setAuditError(t('dashboard.audit.rangeInvalid'));
      setAuditLogs([]);
      return;
    }

    setAuditLoading(true);
    setAuditError(null);
    getAuditLog({
      from: localDateStartUtcIso(auditFrom),
      to: localDateEndUtcIso(auditTo)
    })
      .then(res => setAuditLogs(res.data || []))
      .catch(err => {
        console.error("Denetim günlüğü çekilemedi:", err);
        setAuditError(err.response?.data?.error || t('dashboard.audit.loadError'));
        setAuditLogs([]);
      })
      .finally(() => setAuditLoading(false));
  };

  useEffect(() => {
    if (user && (user.roles.includes('Admin') || user.roles.includes('SuperAdmin'))) {
      fetchUsers();
      fetchRoles();
      fetchFeedbacks();
    }
  }, [user]);

  useEffect(() => {
    if (user && (user.roles.includes('Admin') || user.roles.includes('SuperAdmin')) && activeTab === 'audit') {
      fetchAuditLogs();
    }
  }, [user, activeTab]);

  const filteredUsers = users.filter(u => {
    const matchesSearch = u.username?.toLowerCase().includes(searchTerm.toLowerCase()) ||
    u.email?.toLowerCase().includes(searchTerm.toLowerCase());
    const matchesRole = selectedRole ? u.roles?.includes(selectedRole) : true;

    return matchesSearch && matchesRole;
  });

  const handleRoleAction = async (userId, roleName, action) => {
    const roleObj = rolesList.find(r => r.name === roleName || r.roleName === roleName);
    if (!roleObj) return;

    const roleId = roleObj.id || roleObj.roleId;

    if (action === 'remove') {
      await removeRole(roleId, userId).catch(() => {});
    } else {
      await assignRole(roleId, userId).catch(() => {});
    }
    fetchUsers();
  };

  if (loading) {
    return <div className="admin-dashboard__center">{t('common.loading')}</div>;
  }

  if (!user || !user.roles || (!user.roles.includes('Admin') && !user.roles.includes('SuperAdmin'))) {
    return (
      <div className="admin-dashboard__center">
        <div className="admin-dashboard__unauthorized">
          <h2>{t('dashboard.unauthorized')}</h2>
          <p>{t('dashboard.unauthorizedHint')}</p>
        </div>
      </div>
    );
  }

  const tabs = [
    { key: 'summary', label: `📊 ${t('dashboard.tab.summary')}` },
    { key: 'crypto', label: `🪙 ${t('dashboard.tab.crypto')}`, disabled: true },
    { key: 'users', label: `👤 ${t('dashboard.tab.users')}` },
    { key: 'feedback', label: `💬 ${t('dashboard.tab.feedback')}` },
    { key: 'audit', label: `📋 ${t('dashboard.tab.audit')}` },
    { key: 'settings', label: `⚙️ ${t('dashboard.tab.settings')}`, disabled: true },
  ];

  return (
    <div className="admin-dashboard">
      {/* Sol Menü (Sidebar) */}
      <aside className="admin-dashboard__sidebar">
        <h3>{t('dashboard.adminPanel')}</h3>
        <ul>
          {tabs.map((tab) => (
            <li key={tab.key}>
              <button
                type="button"
                className={`admin-dashboard__tab${activeTab === tab.key ? ' is-active' : ''}`}
                onClick={() => setActiveTab(tab.key)}
                disabled={tab.disabled}
                aria-current={activeTab === tab.key ? 'page' : undefined}
                title={tab.disabled ? t('dashboard.tab.soon') : undefined}
              >
                {tab.label}
              </button>
            </li>
          ))}
        </ul>
      </aside>

      {/* Sağ İçerik Alanı (Main Content) */}
      <div className="admin-dashboard__content">
      {activeTab === 'summary' && (
      <div>
      <h2>{t('dashboard.reports')}</h2>
      <p className="admin-dashboard__muted">{t('dashboard.reportsSubtitle')}</p>

      {/* Örnek Bilgi Kartları */}
      <div className="admin-dashboard__cards">
        <div className="admin-dashboard__card admin-dashboard__card--blue">
          <h4>{t('dashboard.totalUsers')}</h4>
          <span>1,240</span>
        </div>
        <div className="admin-dashboard__card admin-dashboard__card--green">
          <h4>{t('dashboard.activeCoins')}</h4>
          <span>85</span>
        </div>
        <div className="admin-dashboard__card admin-dashboard__card--amber">
          <h4>{t('dashboard.dailyVolume')}</h4>
          <span>$45,210</span>
        </div>
      </div>
    </div>
    )}

 {activeTab === 'users' && (
          <div>
            <h2>{t('dashboard.userMgmt')}</h2>
            <p className="admin-dashboard__muted">{t('dashboard.usersSubtitle')}</p>

            <div className="admin-dashboard__filters">
              <input
                type="text"
                placeholder={t('dashboard.searchUser')}
                aria-label={t('dashboard.searchUser')}
                onChange={(e) => setSearchTerm(e.target.value)}
            />

            <select
                aria-label={t('dashboard.allRoles')}
                onChange={(e) => setSelectedRole(e.target.value)}
              >
                <option value="">{t('dashboard.allRoles')}</option>
                {rolesList.map(r => (
                  <option key={r.id || r.roleId} value={r.name || r.roleName}>
                    {r.name || r.roleName}
                  </option>
                ))}
              </select>
            </div>

            <table className="admin-dashboard__table">
              <thead>
                <tr>
                  <th>{t('profile.username')}</th>
                  <th>{t('dashboard.email')}</th>
                  <th>{t('dashboard.roleAssign')}</th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.map(u => (
                  <tr key={u.id}>
                    <td>{u.username}</td>
                    <td>{u.email}</td>
                    <td>

                      {u.roles?.map(role => (
                        <span key={role} className="admin-dashboard__role">
                          {role}
                          <button
                            type="button"
                            onClick={() => handleRoleAction(u.id, role, 'remove')}
                            aria-label={t('dashboard.removeRole', { role })}
                          >
                            ×
                          </button>
                        </span>
                      ))}

                      <select
                        className="admin-dashboard__role-add"
                        aria-label={t('dashboard.addRole')}
                        onChange={(e) => { if(e.target.value) handleRoleAction(u.id, e.target.value, 'add'); e.target.value = ''; }}
                      >
                        <option value="">+ {t('dashboard.addRole')}</option>
                        {rolesList.map(r => (
                          <option key={r.id || r.roleId} value={r.name || r.roleName}>
                            {r.name || r.roleName}
                          </option>
                        ))}
                      </select>

                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {activeTab === 'feedback' && (
          <div>
            <h2>{t('dashboard.feedbacks')}</h2>
            <p className="admin-dashboard__muted">{t('dashboard.feedbackSubtitle')}</p>

            <table className="admin-dashboard__table">
              <thead>
                <tr>
                  <th>ID</th>
                  <th>{t('dashboard.message')}</th>
                  <th>{t('dashboard.score')}</th>
                  <th>{t('profile.username')}</th>
                  <th>{t('dashboard.date')}</th>
                </tr>
              </thead>
              <tbody>
                {feedbacks.map((item) => (
                  <tr key={item.id}>
                    <td>{item.id}</td>
                    <td>{item.message}</td>
                    <td>{item.rating ?? '-'}</td>
                    <td>{item.userId ?? t('dashboard.guest')}</td>
                    <td>{new Date(item.createdAt).toLocaleString(locale)}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            {feedbacks.length === 0 && (
              <p className="admin-dashboard__muted">{t('dashboard.noFeedback')}</p>
            )}
          </div>
        )}

        {activeTab === 'audit' && (
          <div>
            <h2>{t('dashboard.auditLog')}</h2>
            <p className="admin-dashboard__muted">{t('dashboard.auditSubtitle')}</p>

            <div className="admin-dashboard__filters">
              <label>
                {t('common.startDate')}
                <input
                  type="date"
                  value={auditFrom}
                  onChange={(e) => setAuditFrom(e.target.value)}
                />
              </label>
              <label>
                {t('common.endDate')}
                <input
                  type="date"
                  value={auditTo}
                  onChange={(e) => setAuditTo(e.target.value)}
                />
              </label>
              <button
                type="button"
                className="admin-dashboard__button admin-dashboard__button--primary"
                onClick={fetchAuditLogs}
              >
                {t('common.filter')}
              </button>
              <button
                type="button"
                className="admin-dashboard__button"
                onClick={() => {
                  setAuditFrom('');
                  setAuditTo('');
                  setAuditLoading(true);
                  setAuditError(null);
                  getAuditLog()
                    .then(res => setAuditLogs(res.data || []))
                    .catch(err => {
                      setAuditError(err.response?.data?.error || t('dashboard.audit.loadError'));
                      setAuditLogs([]);
                    })
                    .finally(() => setAuditLoading(false));
                }}
              >
                {t('common.clear')}
              </button>
            </div>

            {auditLoading && (
              <p className="admin-dashboard__muted">{t('dashboard.audit.loading')}</p>
            )}

            {auditError && (
              <p className="admin-dashboard__error">{auditError}</p>
            )}

            {!auditLoading && !auditError && (
              <table className="admin-dashboard__table">
                <thead>
                  <tr>
                    <th>{t('dashboard.admin')}</th>
                    <th>{t('dashboard.action')}</th>
                    <th>{t('dashboard.target')}</th>
                    <th>{t('dashboard.detail')}</th>
                    <th>{t('dashboard.dateTime')}</th>
                  </tr>
                </thead>
                <tbody>
                  {auditLogs.map((item) => (
                    <tr key={item.id}>
                      <td>{item.actorUsername || t('dashboard.audit.userFallback', { id: item.actorUserId })}</td>
                      <td>
                        {AUDIT_ACTION_KEYS[item.action] ? t(AUDIT_ACTION_KEYS[item.action]) : item.action}
                        <div className="admin-dashboard__code">{item.action}</div>
                      </td>
                      <td>Bot #{item.targetId}</td>
                      <td className="admin-dashboard__muted">{item.details || '-'}</td>
                      <td>{formatAuditDate(item.createdAt, locale)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {!auditLoading && !auditError && auditLogs.length === 0 && (
              <p className="admin-dashboard__muted">{t('dashboard.audit.empty')}</p>
            )}
          </div>
        )}

      </div>
    </div>
  );
}
