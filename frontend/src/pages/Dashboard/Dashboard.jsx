import { useEffect, useState } from 'react';
import { getDailyNewUsers } from '../../services/apiService';
// TODO Gorev 5: istatistikleri goster, sadece Admin/SuperAdmin erisebilsin
export default function Dashboard() {
  const [stats, setStats] = useState(null);
  useEffect(() => {
    getDailyNewUsers().then(res => setStats(res.data)).catch(console.error);
  }, []);
  return (
    <div>
      <h2>Admin Dashboard</h2>
      <p style={{ color: 'orange' }}>Gorev 5 — tamamlanmadi</p>
      {stats && <pre>{JSON.stringify(stats, null, 2)}</pre>}
    </div>
  );
}
