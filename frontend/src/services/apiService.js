import axios from 'axios';

const api = axios.create({
    baseURL: process.env.REACT_APP_API_URL || 'http://localhost:5002',
});

api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});

// Auth
export const register = (data) => api.post('/api/Auth/register', data);
export const login = (data) => api.post('/api/Auth/login', data);
export const getMe = () => api.get('/api/Auth/me');
export const updateProfile = (data) => api.put('/api/Auth/me', data);
export const changePassword = (data) => api.put('/api/Auth/me/password', data);
export const getAllUsers = () => api.get('/api/Auth/users');
export const forgotPassword = (data) => api.post('/api/Auth/forgot-password', data);
export const resetPassword = (data) => api.post('/api/Auth/reset-password', data);

// Role
export const getRoles = () => api.get('/api/Role');
export const createRole = (data) => api.post('/api/Role', data);
export const assignRole = (roleId, userId) => api.post(`/api/Role/${roleId}/assign/${userId}`);
export const removeRole = (roleId, userId) => api.delete(`/api/Role/${roleId}/remove/${userId}`);
export const getUserRoles = (userId) => api.get(`/api/Role/user/${userId}`);

// Dashboard
export const getDailyNewUsers = () => api.get('/api/Dashboard/daily-new-users');

// Watchlist
export const getWatchlist = () => api.get('/api/Watchlist');
export const addToWatchlist = (symbol) => api.post(`/api/Watchlist/${symbol}`);
export const removeFromWatchlist = (symbol) => api.delete(`/api/Watchlist/${symbol}`);

// Alert
export const getAlerts = () => api.get('/api/Alert');
export const createAlert = (data) => api.post('/api/Alert', data);
export const deleteAlert = (id) => api.delete(`/api/Alert/${id}`);
export const toggleAlert = (id, isActive) => api.patch(`/api/Alert/${id}/toggle`, { isActive });
export const getAlertSignals = (id) => api.get(`/api/Alert/${id}/signals`);

// Feedback
export const getFeedbacks = () => api.get('/api/Feedback');
export const createFeedback = (data) => api.post('/api/Feedback', data);

// Portfolio & Leaderboard
export const getBalance = () => api.get('/api/Portfolio/balance');
export const getHoldings = () => api.get('/api/Portfolio/holdings');
export const getTransactions = () => api.get('/api/Portfolio/transactions');
export const getLeaderboard = () => api.get('/api/Portfolio/leaderboard');

export const buyCoin = async (data) => {
    const response = await api.post('/api/Portfolio/buy', data);
    return response.data;
};

export const sellCoin = async (data) => {
    const response = await api.post('/api/Portfolio/sell', data);
    return response.data;
};

export const getBotPerformance = () => api.get('/api/Bot/performance');

// --- BOT ENDPOINTS ---
export const getBots = async () => {
    return await api.get('/api/Bot');
};

export const createBot = async (botData) => {
    return await api.post('/api/Bot', botData);
};

export const toggleBot = async (botId) => {
    return await api.patch(`/api/Bot/${botId}/toggle`);
};

export const getBotSignals = async (botId) => {
    return await api.get(`/api/Bot/${botId}/signals`);
};

export const approveBotSignal = async (signalId) => {
    return await api.post(`/api/Bot/signals/${signalId}/approve`);
};

export const rejectBotSignal = async (signalId) => {
    return await api.post(`/api/Bot/signals/${signalId}/reject`);
};

// DÜZELTİLEN KISIM: Başına /api eklendi
export const deleteBot = async (botId) => {
    return await api.delete(`/api/Bot/${botId}`);
};