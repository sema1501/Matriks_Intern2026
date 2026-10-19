import axios from 'axios';

const baseURL = process.env.REACT_APP_API_URL || 'http://localhost:5002';

const api = axios.create({ baseURL });

// refresh/logout çağrıları için ayrı istemci: bunlar aşağıdaki 401 yakalayıcısına
// girmemeli, yoksa refresh'in kendisi 401 alınca sonsuz döngü olur.
const authApi = axios.create({ baseURL });

export const SESSION_MESSAGE_KEY = 'authMessage';

export const saveSession = (token, refreshToken) => {
    localStorage.setItem('token', token);
    localStorage.setItem('refreshToken', refreshToken);
};

export const clearSession = () => {
    localStorage.removeItem('token');
    localStorage.removeItem('refreshToken');
};

// Oturumu kapatıp giriş sayfasına gönderir. Mesaj anahtarı giriş sayfasında çevrilip gösterilir.
export const endSession = (messageKey) => {
    clearSession();
    if (messageKey) sessionStorage.setItem(SESSION_MESSAGE_KEY, messageKey);
    if (window.location.pathname !== '/signin') window.location.assign('/signin');
};

api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});

// Aynı anda birden çok istek 401 alırsa hepsi bu tek refresh isteğini bekler.
let refreshPromise = null;

const refreshAccessToken = () => {
    if (!refreshPromise) {
        const refreshToken = localStorage.getItem('refreshToken');
        refreshPromise = authApi
            .post('/api/Auth/refresh', { refreshToken })
            .then((res) => {
                saveSession(res.data.token, res.data.refreshToken);
                return res.data.token;
            })
            .finally(() => { refreshPromise = null; });
    }
    return refreshPromise;
};

api.interceptors.response.use(
    (response) => response,
    async (error) => {
        const original = error.config;
        const sentToken = original?.headers?.Authorization?.replace('Bearer ', '');

        // Yalnızca giriş yapmış kullanıcının isteği 401 aldıysa ve bu istek daha önce
        // tekrar denenmediyse devreye gir. Girişsiz isteklerin 401'i olduğu gibi döner.
        if (error.response?.status !== 401 || !sentToken || original._retry) {
            return Promise.reject(error);
        }
        original._retry = true;

        // Başka bir sekme token'ı bu arada yenilediyse refresh atmadan yeni token'la dene.
        const currentToken = localStorage.getItem('token');
        if (currentToken && currentToken !== sentToken) {
            original.headers.Authorization = `Bearer ${currentToken}`;
            return api(original);
        }

        if (!localStorage.getItem('refreshToken')) {
            endSession('auth.sessionExpired');
            return Promise.reject(error);
        }

        try {
            const newToken = await refreshAccessToken();
            original.headers.Authorization = `Bearer ${newToken}`;
            return api(original);
        } catch (refreshError) {
            endSession('auth.sessionExpired');
            return Promise.reject(refreshError);
        }
    }
);

// Auth
export const register = (data) => api.post('/api/Auth/register', data);
export const login = (data) => api.post('/api/Auth/login', data);
export const logout = (refreshToken) => authApi.post('/api/Auth/logout', { refreshToken });
export const getMe = () => api.get('/api/Auth/me');
export const updateProfile = (data) => api.put('/api/Auth/me', data);
export const changePassword = (data) => api.put('/api/Auth/me/password', data);
export const setEmailNotifications = (enabled) => api.put('/api/Auth/me/notifications', { enabled });
export const setAvatar = (avatarUrl) => api.put('/api/Auth/me/avatar', { avatarUrl });
export const getAllUsers = () => api.get('/api/Auth/users');
export const forgotPassword = (data) => api.post('/api/Auth/forgot-password', data);
export const resetPassword = (data) => api.post('/api/Auth/reset-password', data);
export const confirmEmail = (token) => api.post('/api/Auth/confirm-email', { token });

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
export const getTransactions = (params) => api.get('/api/Portfolio/transactions', { params });
// Görev 89: işlem geçmişini CSV ya da Excel olarak indir (dosya içeriği blob olarak gelir).
// params: symbol, type, lang ('tr' | 'en'), format ('csv' | 'xlsx')
export const exportTransactions = (params) =>
    api.get('/api/Portfolio/transactions/export', { params, responseType: 'blob' });
export const getLeaderboard = () => api.get('/api/Portfolio/leaderboard');
// Portföy toplam değer geçmişi. range: '24h' | '7d' | '30d' | 'all'
export const getPortfolioHistory = (range) => api.get('/api/Portfolio/history', { params: { range } });

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
// Admin
export const getAdminBots = () => api.get('/api/Admin/bots');
export const getAdminPortfolios = () => api.get('/api/Admin/portfolios');
export const killAdminBot = (botId) => api.post(`/api/Admin/bots/${botId}/kill`);
export const getOvertradingBots = () => api.get('/api/Admin/overtrading');
export const getAuditLog = (params = {}) => {
    const query = {};
    if (params.from) query.from = params.from;
    if (params.to) query.to = params.to;
    if (params.ascending === true || params.ascending === false) query.ascending = params.ascending;
    return api.get('/api/Admin/audit-log', { params: query });
};

// Backtest: geçmiş veri üzerinde saf simülasyon, gerçek/Testnet emri göndermez
export const runBotBacktest = async (botId, dateRange) => {
    return await api.post(`/api/Bot/${botId}/backtest`, dateRange);
};