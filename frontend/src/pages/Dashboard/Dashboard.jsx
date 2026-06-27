import React from 'react';

export default function Dashboard() {
  return (
    <div style={{ display: 'flex', minHeight: 'calc(100vh - 60px)', fontFamily: 'sans-serif' }}>
      {/* Sol Menü (Sidebar) */}
      <div style={{ width: '240px', backgroundColor: '#f4f4f4', padding: '20px', borderRight: '1px solid #ddd' }}>
        <h3 style={{ margin: '0 0 20px 0', color: '#333' }}>Admin Paneli</h3>
        <ul style={{ listStyleType: 'none', padding: 0, margin: 0 }}>
          <li style={{ padding: '10px 0', fontWeight: 'bold', color: '#0066cc' }}>📊 Özet Raporlar</li>
          <li style={{ padding: '10px 0', color: '#555', cursor: 'pointer' }}>🪙 Kripto Varlıklar</li>
          <li style={{ padding: '10px 0', color: '#555', cursor: 'pointer' }}>👤 Kullanıcı Yönetimi</li>
          <li style={{ padding: '10px 0', color: '#555', cursor: 'pointer' }}>⚙️ Ayarlar</li>
        </ul>
      </div>

      {/* Sağ İçerik Alanı (Main Content) */}
      <div style={{ flex: 1, padding: '30px', backgroundColor: '#fff' }}>
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
    </div>
  );
}
