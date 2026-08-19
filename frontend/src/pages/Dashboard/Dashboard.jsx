import React, { useState, useEffect } from 'react';
import { useAuth } from '../../context/AuthContext';
import { getAllUsers, assignRole, removeRole, getRoles, getFeedbacks, getAuditLog } from '../../services/apiService';

function parseApiUtcDate(value) {
  if (!value) return null;
  const text = String(value);
  const hasTimezone = /Z|[+-]\d{2}:?\d{2}$/.test(text);
  return new Date(hasTimezone ? text : `${text}Z`);
}

function formatAuditDate(value) {
  const date = parseApiUtcDate(value);
  if (!date || Number.isNaN(date.getTime())) return '-';
  return date.toLocaleString('tr-TR');
}

function localDateStartUtcIso(dateStr) {
  if (!dateStr) return undefined;
  return new Date(`${dateStr}T00:00:00`).toISOString();
}

function localDateEndUtcIso(dateStr) {
  if (!dateStr) return undefined;
  return new Date(`${dateStr}T23:59:59.999`).toISOString();
}

const AUDIT_ACTION_LABELS = {
  BotForceStopped: 'Bot zorla durduruldu',
  BotFlagged: 'Bot işaretlendi'
};

export default function Dashboard() {
  const { user, loading } = useAuth();
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
      setAuditError('Başlangıç tarihi bitiş tarihinden sonra olamaz.');
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
        setAuditError(err.response?.data?.error || "Denetim günlüğü yüklenemedi.");
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
    return <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh' }}>Yükleniyor...</div>;
  }

  if (!user || !user.roles || (!user.roles.includes('Admin') && !user.roles.includes('SuperAdmin'))) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', backgroundColor: '#f8f9fa' }}>
        <div style={{ textAlign: 'center', padding: '40px', backgroundColor: 'white', borderRadius: '8px', boxShadow: '0 4px 6px rgba(0,0,0,0.1)' }}>
          <h2 style={{ color: '#dc3545', margin: '0 0 10px 0' }}>Yetkisiz Erişim!</h2>
          <p style={{ color: '#6c757d', margin: 0 }}>Bu sayfayı görüntülemek için Admin veya SuperAdmin yetkisine sahip olmalısınız.</p>
        </div>
      </div>
    );
  }

  const getTabStyle = (tabName) => ({
    padding: '10px 0',
    cursor: 'pointer',
    fontWeight: activeTab === tabName ? 'bold' : 'normal',
    color: activeTab === tabName ? '#0066cc' : '#555'
  });

  return (
    <div style={{ display: 'flex', minHeight: 'calc(100vh - 60px)', fontFamily: 'sans-serif' }}>
      {/* Sol Menü (Sidebar) */}
      <div style={{ width: '240px', backgroundColor: '#f4f4f4', padding: '20px', borderRight: '1px solid #ddd' }}>
        <h3 style={{ margin: '0 0 20px 0', color: '#333' }}>Admin Paneli</h3>
        <ul style={{ listStyleType: 'none', padding: 0, margin: 0 }}>
          <li style={getTabStyle('summary')} onClick={() => setActiveTab('summary')}>📊 Özet Raporlar</li>
          <li style={{ padding: '10px 0', color: '#555', cursor: 'not-allowed', opacity: 0.5 }}>🪙 Kripto Varlıklar</li>
          <li style={getTabStyle('users')} onClick={() => setActiveTab('users')}>👤 Kullanıcı Yönetimi</li>
          <li style={getTabStyle('feedback')} onClick={() => setActiveTab('feedback')}>💬 Geri Bildirimler</li>
          <li style={getTabStyle('audit')} onClick={() => setActiveTab('audit')}>📋 Denetim Günlüğü</li>
          <li style={{ padding: '10px 0', color: '#555', cursor: 'not-allowed', opacity: 0.5 }}>⚙️ Ayarlar</li>
        </ul>
      </div>

      {/* Sağ İçerik Alanı (Main Content) */}
      <div style={{ flex: 1, padding: '30px', backgroundColor: '#fff' }}>
      {activeTab === 'summary' && (
      <div>
      <h2 style={{ marginTop: 0 }}>📊 Sistem Özet Raporları</h2>
      <p style={{ color: '#666' }}>CryptoTracker sistemindeki anlık veriler ve yönetim araçları.</p>

      {/* Örnek Bilgi Kartları */}
      <div style={{ display: 'flex', gap: '20px', marginTop: '30px' }}>
        <div style={{ flex: 1, padding: '20px', backgroundColor: '#e6f2ff', borderRadius: '8px', border: '1px solid #b3d7ff' }}>
          <h4 style={{ margin: '0 0 10px 0', color: '#004085' }}>Toplam Kullanıcı</h4>
          <span style={{ fontSize: '24px', fontWeight: 'bold' }}>1,240</span>
        </div>
        <div style={{ flex: 1, padding: '20px', backgroundColor: '#d4edda', borderRadius: '8px', border: '1px solid #c3e6cb' }}>
          <h4 style={{ margin: '0 0 10px 0', color: '#155724' }}>Aktif Kripto Paralar</h4>
          <span style={{ fontSize: '24px', fontWeight: 'bold' }}>85</span>
        </div>
        <div style={{ flex: 1, padding: '20px', backgroundColor: '#fff3cd', borderRadius: '8px', border: '1px solid #ffeeba' }}>
          <h4 style={{ margin: '0 0 10px 0', color: '#856404' }}>Günlük İşlem Hacmi</h4>
          <span style={{ fontSize: '24px', fontWeight: 'bold' }}>$45,210</span>
        </div>
      </div>
    </div>
    )}

 {activeTab === 'users' && (
          <div>
            <h2 style={{ marginTop: 0 }}> Kullanıcı Yönetimi Tablosu</h2>
            <p style={{ color: '#666' }}>Sistemdeki kullanıcıları arayın, filtreleyin ve rollerini yönetin.</p>

            <div style={{ display: 'flex', gap: '10px', marginBottom: '15px' }}>
              <input
                type="text"
                placeholder="Kullanıcı veya E-posta ara..."
                onChange={(e) => setSearchTerm(e.target.value)}
                style={{ padding: '8px', width: '250px', border: '1px solid #ccc', borderRadius: '4px' }}
            />

            <select
                onChange={(e) => setSelectedRole(e.target.value)}
                style={{ padding: '8px', width: '150px', border: '1px solid #ccc', borderRadius: '4px' }}
              >
                <option value="">Tüm Roller</option>
                {rolesList.map(r => (
                  <option key={r.id || r.roleId} value={r.name || r.roleName}>
                    {r.name || r.roleName}
                  </option>
                ))}
              </select>
            </div>

            <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '20px' }}>
              <thead>
                <tr style={{ backgroundColor: '#f8f9fa', textAlign: 'left', borderBottom: '2px solid #ddd' }}>
                  <th style={{ padding: '12px', color: '#495057' }}>Kullanıcı Adı</th>
                  <th style={{ padding: '12px', color: '#495057' }}>E-Posta</th>
                  <th style={{ padding: '12px', color: '#495057' }}>Rol Atama</th>
                </tr>
              </thead>
              <tbody>
                {filteredUsers.map(u => (
                  <tr key={u.id} style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '12px' }}>{u.username}</td>
                    <td style={{ padding: '12px' }}>{u.email}</td>
                    <td style={{ padding: '12px' }}>

                      {u.roles?.map(role => (
                        <span key={role} style={{ marginRight: '5px', background: '#eee', padding: '2px 6px', borderRadius: '4px' }}>
                          {role}
                          <button onClick={() => handleRoleAction(u.id, role, 'remove')} style={{ marginLeft: '5px', cursor: 'pointer', border: 'none', background: 'transparent' }}>x</button>
                        </span>
                      ))}

                      <select onChange={(e) => { if(e.target.value) handleRoleAction(u.id, e.target.value, 'add'); e.target.value = ''; }} style={{ marginLeft: '10px', padding: '2px' }}>
                        <option value="">+ Rol Ekle</option>
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
            <h2 style={{ marginTop: 0 }}>💬 Geri Bildirimler</h2>
            <p style={{ color: '#666' }}>Kullanıcılardan gelen geri bildirim, şikayet ve öneriler.</p>

            <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '20px' }}>
              <thead>
                <tr style={{ backgroundColor: '#f8f9fa', textAlign: 'left', borderBottom: '2px solid #ddd' }}>
                  <th style={{ padding: '12px', color: '#495057' }}>ID</th>
                  <th style={{ padding: '12px', color: '#495057' }}>Mesaj</th>
                  <th style={{ padding: '12px', color: '#495057' }}>Puan</th>
                  <th style={{ padding: '12px', color: '#495057' }}>Kullanıcı</th>
                  <th style={{ padding: '12px', color: '#495057' }}>Tarih</th>
                </tr>
              </thead>
              <tbody>
                {feedbacks.map((item) => (
                  <tr key={item.id} style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '12px' }}>{item.id}</td>
                    <td style={{ padding: '12px' }}>{item.message}</td>
                    <td style={{ padding: '12px' }}>{item.rating ?? '-'}</td>
                    <td style={{ padding: '12px' }}>{item.userId ?? 'Misafir'}</td>
                    <td style={{ padding: '12px' }}>{new Date(item.createdAt).toLocaleString('tr-TR')}</td>
                  </tr>
                ))}
              </tbody>
            </table>

            {feedbacks.length === 0 && (
              <p style={{ marginTop: '20px', color: '#666' }}>Henüz geri bildirim bulunmuyor.</p>
            )}
          </div>
        )}

        {activeTab === 'audit' && (
          <div>
            <h2 style={{ marginTop: 0 }}>📋 Denetim Günlüğü</h2>
            <p style={{ color: '#666' }}>Hangi yöneticinin hangi bot üzerinde ne zaman işlem yaptığını görüntüleyin. Kayıtlar varsayılan olarak en yeniden eskiye sıralanır.</p>

            <div style={{ display: 'flex', gap: '10px', marginBottom: '15px', alignItems: 'center', flexWrap: 'wrap' }}>
              <label style={{ color: '#555' }}>
                Başlangıç
                <input
                  type="date"
                  value={auditFrom}
                  onChange={(e) => setAuditFrom(e.target.value)}
                  style={{ marginLeft: '8px', padding: '8px', border: '1px solid #ccc', borderRadius: '4px' }}
                />
              </label>
              <label style={{ color: '#555' }}>
                Bitiş
                <input
                  type="date"
                  value={auditTo}
                  onChange={(e) => setAuditTo(e.target.value)}
                  style={{ marginLeft: '8px', padding: '8px', border: '1px solid #ccc', borderRadius: '4px' }}
                />
              </label>
              <button
                type="button"
                onClick={fetchAuditLogs}
                style={{ padding: '8px 14px', cursor: 'pointer', border: '1px solid #0066cc', background: '#0066cc', color: '#fff', borderRadius: '4px' }}
              >
                Filtrele
              </button>
              <button
                type="button"
                onClick={() => {
                  setAuditFrom('');
                  setAuditTo('');
                  setAuditLoading(true);
                  setAuditError(null);
                  getAuditLog()
                    .then(res => setAuditLogs(res.data || []))
                    .catch(err => {
                      setAuditError(err.response?.data?.error || "Denetim günlüğü yüklenemedi.");
                      setAuditLogs([]);
                    })
                    .finally(() => setAuditLoading(false));
                }}
                style={{ padding: '8px 14px', cursor: 'pointer', border: '1px solid #ccc', background: '#fff', borderRadius: '4px' }}
              >
                Temizle
              </button>
            </div>

            {auditLoading && (
              <p style={{ color: '#666' }}>Denetim kayıtları yükleniyor...</p>
            )}

            {auditError && (
              <p style={{ color: '#dc3545' }}>{auditError}</p>
            )}

            {!auditLoading && !auditError && (
              <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '20px' }}>
                <thead>
                  <tr style={{ backgroundColor: '#f8f9fa', textAlign: 'left', borderBottom: '2px solid #ddd' }}>
                    <th style={{ padding: '12px', color: '#495057' }}>Yönetici</th>
                    <th style={{ padding: '12px', color: '#495057' }}>İşlem</th>
                    <th style={{ padding: '12px', color: '#495057' }}>Hedef</th>
                    <th style={{ padding: '12px', color: '#495057' }}>Detay</th>
                    <th style={{ padding: '12px', color: '#495057' }}>Tarih / Saat</th>
                  </tr>
                </thead>
                <tbody>
                  {auditLogs.map((item) => (
                    <tr key={item.id} style={{ borderBottom: '1px solid #eee' }}>
                      <td style={{ padding: '12px' }}>{item.actorUsername || `Kullanıcı #${item.actorUserId}`}</td>
                      <td style={{ padding: '12px' }}>
                        {AUDIT_ACTION_LABELS[item.action] || item.action}
                        <div style={{ fontSize: '12px', color: '#888' }}>{item.action}</div>
                      </td>
                      <td style={{ padding: '12px' }}>Bot #{item.targetId}</td>
                      <td style={{ padding: '12px', color: '#555' }}>{item.details || '-'}</td>
                      <td style={{ padding: '12px' }}>{formatAuditDate(item.createdAt)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}

            {!auditLoading && !auditError && auditLogs.length === 0 && (
              <p style={{ marginTop: '20px', color: '#666' }}>Seçilen aralıkta denetim kaydı bulunmuyor.</p>
            )}
          </div>
        )}

      </div>
    </div>
  );
}
