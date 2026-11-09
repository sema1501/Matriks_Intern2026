import axios from 'axios';

export const API_BASE_URL = process.env.REACT_APP_API_URL || 'http://localhost:5002';
const baseURL = API_BASE_URL;

// Refresh token HttpOnly cookie'de durur, kod onu göremez. withCredentials olmadan API başka porttayken
// tarayıcı cookie'yi ne kaydeder ne de gönderir.
const api = axios.create({ baseURL, withCredentials: true });

// refresh/logout çağrıları için ayrı istemci: bunlar aşağıdaki 401 yakalayıcısına
// girmemeli, yoksa refresh'in kendisi 401 alınca sonsuz döngü olur.
// Özel başlığı backend CSRF koruması için ister, başka bir site CORS izni olmadan gönderemez.
const authApi = axios.create({
    baseURL,
    withCredentials: true,
    timeout: 15_000,
    headers: { 'X-Requested-With': 'XMLHttpRequest' },
});

export const SESSION_MESSAGE_KEY = 'authMessage';

// Cookie'nin varlığı koddan görülemez. Bu işaret "bu tarayıcıda oturum açılmıştı" der; böylece access token
// kaybolunca refresh denenir ama hiç giriş yapmamış ziyaretçi her sayfa açılışında boşuna istek atmaz.
const SESSION_MARKER_KEY = 'hasSession';

// Eski sürümden localStorage'da kalan refresh token artık işe yaramıyor, orada durmasın.
localStorage.removeItem('refreshToken');

export const saveSession = (token) => {
    localStorage.setItem('token', token);
    localStorage.setItem(SESSION_MARKER_KEY, '1');
};

export const clearSession = () => {
    localStorage.removeItem('token');
    localStorage.removeItem(SESSION_MARKER_KEY);
};

export const hasSession = () =>
    Boolean(localStorage.getItem('token') || localStorage.getItem(SESSION_MARKER_KEY));

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

// Token'ın exp alanını okur (imza doğrulaması backend'in işi). Okunamazsa süresi dolmuş sayılır.
const expiresSoon = (token, marginMs) => {
    try {
        const part = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
        const { exp } = JSON.parse(atob(part.padEnd(Math.ceil(part.length / 4) * 4, '=')));
        return !exp || exp * 1000 - marginMs <= Date.now();
    } catch {
        return true;
    }
};

const FRESH_TOKEN_MARGIN_MS = 30_000;

// Cookie tüm sekmelerde ortak olduğu için iki sekme aynı anda yenilerse ikincisi az önce iptal edilen
// token'ı gönderir. Web Locks ile tek seferde tek sekme yeniler, diğeri kilit açılınca yeni token'ı okur.
// Tarayıcı Web Locks desteklemiyorsa (ya da sayfa HTTPS değilse) kilit yoktur, aşağıdaki yedek devreye girer.
const REFRESH_LOCK_NAME = 'crypto-tracker-token-refresh';

const withRefreshLock = (task) =>
    navigator.locks ? navigator.locks.request(REFRESH_LOCK_NAME, task) : task();

// Backend bu kodu, token birkaç saniye önce başka bir sekmede yenilendiyse döner (#100).
const TOKEN_ROTATED_CODE = 'refresh_token_rotated';
const OTHER_TAB_WAIT_MS = 3000;
const OTHER_TAB_POLL_MS = 100;

// Diğer sekme yeni access token'ı localStorage'a yazana kadar kısa bir süre bekler.
const waitForOtherTabToken = async (previousToken) => {
    for (let waited = 0; waited <= OTHER_TAB_WAIT_MS; waited += OTHER_TAB_POLL_MS) {
        const token = localStorage.getItem('token');
        if (token && token !== previousToken) return token;
        await new Promise((resolve) => setTimeout(resolve, OTHER_TAB_POLL_MS));
    }
    return null;
};

// Aynı sekmede aynı anda birden çok istek 401 alırsa hepsi bu tek refresh isteğini bekler.
let refreshPromise = null;

const refreshAccessToken = () => {
    if (!refreshPromise) {
        const previousToken = localStorage.getItem('token');
        refreshPromise = withRefreshLock(async () => {
            // Kilidi beklerken başka bir sekme yenilediyse ikinci kez yenilemeye gerek yok.
            const current = localStorage.getItem('token');
            if (current && current !== previousToken && !expiresSoon(current, FRESH_TOKEN_MARGIN_MS)) {
                return current;
            }

            try {
                const res = await authApi.post('/api/Auth/refresh');
                saveSession(res.data.token);
                return res.data.token;
            } catch (refreshError) {
                // Kilit yoksa iki sekme yine aynı anda yenileyebilir: diğeri kazandı, onun token'ını kullan.
                if (refreshError.response?.data?.code === TOKEN_ROTATED_CODE) {
                    const token = await waitForOtherTabToken(previousToken);
                    if (token) return token;
                }
                throw refreshError;
            }
        }).finally(() => { refreshPromise = null; });
    }
    return refreshPromise;
};

// Oturumu yalnızca refresh token gerçekten reddedildiyse kapat. Backend kapalıyken ya da rate limit'e
// takılınca kullanıcı atılmaz, istek hata döner.
const endSessionIfRejected = (refreshError) => {
    if (refreshError.response?.status === 401) endSession('auth.sessionExpired');
};

// SignalR her bağlanışta ve yeniden bağlanışta bunu çağırır (#102). Token dolmak üzereyse
// önce yenilenir, böylece süresi dolmuş token ile bağlanılmaz. Yenileme aynı kilitten geçer.
export const getFreshAccessToken = async () => {
    const token = localStorage.getItem('token');
    if (token && !expiresSoon(token, FRESH_TOKEN_MARGIN_MS)) return token;
    if (!hasSession()) return token;
    try {
        return await refreshAccessToken();
    } catch (refreshError) {
        endSessionIfRejected(refreshError);
        throw refreshError;
    }
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

        try {
            const newToken = await refreshAccessToken();
            original.headers.Authorization = `Bearer ${newToken}`;
            return api(original);
        } catch (refreshError) {
            endSessionIfRejected(refreshError);
            return Promise.reject(refreshError);
        }
    }
);

// Auth
export const register = (data) => api.post('/api/Auth/register', data);
export const login = (data) => api.post('/api/Auth/login', data);
export const logout = () => authApi.post('/api/Auth/logout');
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