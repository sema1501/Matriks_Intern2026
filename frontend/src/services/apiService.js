import axios from 'axios';

const api = axios.create({
  baseURL: process.env.REACT_APP_API_URL || 'http://localhost:5002',
});

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) config.headers.Authorization = `Bearer ${token}`;
  return config;
});

export const register         = (data)           => api.post('/api/Auth/register', data);
export const login            = (data)           => api.post('/api/Auth/login', data);
export const getMe            = ()               => api.get('/api/Auth/me');
export const updateProfile    = (data)           => api.put('/api/Auth/me', data);
export const changePassword   = (data)           => api.put('/api/Auth/me/password', data);
export const getAllUsers       = ()               => api.get('/api/Auth/users');
export const getRoles         = ()               => api.get('/api/Role');
export const createRole       = (data)           => api.post('/api/Role', data);
export const assignRole       = (roleId, userId) => api.post(`/api/Role/${roleId}/assign/${userId}`);
export const removeRole       = (roleId, userId) => api.delete(`/api/Role/${roleId}/remove/${userId}`);
export const getUserRoles     = (userId)         => api.get(`/api/Role/user/${userId}`);

export const getDailyNewUsers    = ()             => api.get('/api/Dashboard/daily-new-users');

// Watchlist
export const getWatchlist        = ()             => api.get('/api/Watchlist');
export const addToWatchlist      = (symbol)       => api.post(`/api/Watchlist/${symbol}`);
export const removeFromWatchlist = (symbol)       => api.delete(`/api/Watchlist/${symbol}`);

// Alert
export const getAlerts        = ()               => api.get('/api/Alert');
export const createAlert      = (data)           => api.post('/api/Alert', data);
export const deleteAlert      = (id)             => api.delete(`/api/Alert/${id}`);

