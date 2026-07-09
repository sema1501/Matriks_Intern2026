import React, { useEffect, useState } from 'react';
import { getFeedbacks } from '../../services/apiService';

export default function Dashboard() {
  const [feedbacks, setFeedbacks] = useState([]);

  useEffect(() => {
    loadFeedbacks();
  }, []);

  const loadFeedbacks = async () => {
    try {
      const response = await getFeedbacks();
      setFeedbacks(response.data);
    } catch (error) {
      console.error(error);
    }
  };

  return (
    <div
      style={{
        display: 'flex',
        minHeight: 'calc(100vh - 60px)',
        fontFamily: 'sans-serif',
      }}
    >
      {/* Sidebar */}
      <div
        style={{
          width: '240px',
          backgroundColor: '#f4f4f4',
          padding: '20px',
          borderRight: '1px solid #ddd',
        }}
      >
        <h3>Admin Paneli</h3>

        <ul style={{ listStyle: 'none', padding: 0 }}>
          <li>📊 Özet</li>
          <li>🪙 Kripto Varlıklar</li>
          <li>👤 Kullanıcı Yönetimi</li>
          <li>💬 Geri Bildirimler</li>
        </ul>
      </div>

      {/* İçerik */}
      <div style={{ flex: 1, padding: '30px' }}>
        <h2>Geri Bildirimler</h2>

        <table
          style={{
            width: '100%',
            borderCollapse: 'collapse',
            marginTop: '20px',
          }}
        >
          <thead>
            <tr>
              <th>ID</th>
              <th>Mesaj</th>
              <th>Puan</th>
              <th>Kullanıcı</th>
              <th>Tarih</th>
            </tr>
          </thead>

          <tbody>
            {feedbacks.map((item) => (
              <tr key={item.id}>
                <td>{item.id}</td>
                <td>{item.message}</td>
                <td>{item.rating ?? '-'}</td>
                <td>{item.userId ?? 'Misafir'}</td>
                <td>{new Date(item.createdAt).toLocaleString()}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {feedbacks.length === 0 && (
          <p style={{ marginTop: '20px' }}>
            Henüz geri bildirim bulunmuyor.
          </p>
        )}
      </div>
    </div>
  );
}