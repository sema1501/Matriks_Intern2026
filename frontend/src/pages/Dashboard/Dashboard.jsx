import React, { useEffect, useState } from 'react';
import { getFeedbacks } from '../../services/apiService';
import './Dashboard.css';

export default function Dashboard() {
  const [feedbacks, setFeedbacks] = useState([]);
  const [activeMenu, setActiveMenu] = useState("feedback");

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
    <div className="dashboard">
      {/* Sidebar */}
      <div className="sidebar">
        <h2>👨‍💼 Admin Paneli</h2>

        <ul>
          <li
            className={activeMenu === "summary" ? "active" : ""}
            onClick={() => setActiveMenu("summary")}
          >
            📊 Özet
          </li>

          <li
            className={activeMenu === "crypto" ? "active" : ""}
            onClick={() => setActiveMenu("crypto")}
          >
            🪙 Kripto Varlıklar
          </li>

          <li
            className={activeMenu === "users" ? "active" : ""}
            onClick={() => setActiveMenu("users")}
          >
            👤 Kullanıcı Yönetimi
          </li>

          <li
            className={activeMenu === "feedback" ? "active" : ""}
            onClick={() => setActiveMenu("feedback")}
          >
            💬 Geri Bildirimler
          </li>
        </ul>
      </div>

      {/* İçerik */}
      <div className="content">
        <h2>💬 Geri Bildirimler</h2>

        <table>
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
                <td>{item.rating ?? "-"}</td>
                <td>{item.userId ?? "Misafir"}</td>
                <td>{new Date(item.createdAt).toLocaleString("tr-TR")}</td>
              </tr>
            ))}
          </tbody>
        </table>

        {feedbacks.length === 0 && (
          <p style={{ marginTop: "20px" }}>
            Henüz geri bildirim bulunmuyor.
          </p>
        )}
      </div>
    </div>
  );
}