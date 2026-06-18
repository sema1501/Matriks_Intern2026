import { useState } from 'react';
import { useAuth } from '../../context/AuthContext';
import { login } from '../../services/apiService';

// TODO Gorev 1: form alanları, API cagrisi, yonlendirme, hata gosterimi
export default function SignIn() {
  const { loginUser } = useAuth();
  const [form,  setForm]  = useState({ usernameOrEmail: '', password: '' });
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    // TODO: login(form) -> loginUser(res.data.token, res.data) -> navigate('/home')
  };

  return (
    <div>
      <h2>Giris Yap</h2>
      <p style={{ color: 'orange' }}>Gorev 1 — tamamlanmadi</p>
      <form onSubmit={handleSubmit}>{/* TODO */}</form>
    </div>
  );
}
