// apiService'teki 401 yakalayıcısının testleri. Gerçek backend'e gidilmez: axios'un adapter'ı
// sahte bir fonksiyonla değiştirilir, her istek bu fonksiyona düşer.

import { waitFor } from '@testing-library/react';

let axios;
let apiService;
let refreshCalls;
let logoutCalls;
let handleRefresh;
let beforeProtectedRequest;

const ok = (config, data) => ({ data, status: 200, statusText: 'OK', headers: {}, config, request: {} });

const unauthorized = (config, data = {}) => {
  const response = { data, status: 401, statusText: 'Unauthorized', headers: {}, config, request: {} };
  return Promise.reject(new axios.AxiosError('401', 'ERR_BAD_REQUEST', config, {}, response));
};

const networkError = (config) => Promise.reject(new axios.AxiosError('Network Error', 'ERR_NETWORK', config, {}));

const jwtExpiringIn = (seconds) => {
  const payload = btoa(JSON.stringify({ exp: Math.floor(Date.now() / 1000) + seconds })).replace(/=+$/, '');
  return `header.${payload}.signature`;
};

// Korunan uçlar yalnızca "new-token" ile 200 döner; eski token 401 alır.
const fakeAdapter = (config) => {
  if (config.url === '/api/Auth/refresh') {
    refreshCalls.push(config);
    return handleRefresh(config);
  }
  if (config.url === '/api/Auth/logout') {
    logoutCalls.push(config);
    return Promise.resolve(ok(config, null));
  }
  beforeProtectedRequest(config);
  if (config.headers.Authorization === 'Bearer new-token') {
    return Promise.resolve(ok(config, { url: config.url }));
  }
  return unauthorized(config);
};

const refreshSucceeds = (config) => Promise.resolve(ok(config, { token: 'new-token' }));

// Her sekme kendi modül kopyasıyla çalışır; localStorage ve kilit sekmeler arasında ortaktır.
const openTab = () => {
  let tab;
  jest.isolateModules(() => {
    require('axios').defaults.adapter = fakeAdapter;
    tab = require('./apiService');
  });
  return tab;
};

// Tarayıcıdaki Web Locks gibi: aynı isimli kilidi isteyenler sırayla çalışır.
const installWebLocks = () => {
  let tail = Promise.resolve();
  Object.defineProperty(navigator, 'locks', {
    configurable: true,
    value: {
      request: (name, task) => {
        const run = tail.then(task);
        tail = run.catch(() => {});
        return run;
      },
    },
  });
};

const originalLocation = window.location;

beforeEach(() => {
  // Her testte modüller baştan yüklenir: interceptor'daki refreshPromise testler arasında taşınmaz.
  jest.resetModules();
  axios = require('axios');
  axios.defaults.adapter = fakeAdapter;
  apiService = require('./apiService');

  refreshCalls = [];
  logoutCalls = [];
  handleRefresh = refreshSucceeds;
  beforeProtectedRequest = () => {};
  localStorage.clear();
  sessionStorage.clear();
  localStorage.setItem('token', 'old-token');
  localStorage.setItem('hasSession', '1');

  // jsdom sayfa değiştirmeyi desteklemiyor; yönlendirme sahte assign ile yakalanır.
  delete window.location;
  window.location = { pathname: '/portfolio', assign: jest.fn() };
});

afterEach(() => {
  window.location = originalLocation;
  delete navigator.locks;
});

test('401 alan istek cookie ile refresh sonrası yeni token ile tekrarlanır', async () => {
  const response = await apiService.getMe();

  expect(response.data).toEqual({ url: '/api/Auth/me' });
  expect(refreshCalls).toHaveLength(1);
  expect(localStorage.getItem('token')).toBe('new-token');
});

test('refresh isteği cookie ile ve özel başlıkla gider, gövdede token yoktur', async () => {
  await apiService.getMe();

  const [call] = refreshCalls;
  expect(call.withCredentials).toBe(true);
  expect(call.headers['X-Requested-With']).toBe('XMLHttpRequest');
  expect(call.data).toBeUndefined();
});

test('refresh token hiçbir yerde saklanmaz, eski sürümden kalan değer silinir', async () => {
  localStorage.setItem('refreshToken', 'eski-surumden-kalan');
  openTab();
  expect(localStorage.getItem('refreshToken')).toBeNull();

  handleRefresh = (config) => Promise.resolve(ok(config, { token: 'new-token', refreshToken: 'sizdi' }));
  await apiService.getMe();

  expect(JSON.stringify({ ...localStorage })).not.toContain('sizdi');
});

test('normal API istekleri de cookie alabilsin diye withCredentials ile gider', async () => {
  let sent;
  beforeProtectedRequest = (config) => { sent = config; };
  localStorage.setItem('token', 'new-token');

  await apiService.getBalance();

  expect(sent.withCredentials).toBe(true);
});

test('çıkış isteği cookie ile ve özel başlıkla gider', async () => {
  await apiService.logout();

  const [call] = logoutCalls;
  expect(call.withCredentials).toBe(true);
  expect(call.headers['X-Requested-With']).toBe('XMLHttpRequest');
  expect(call.data).toBeUndefined();
});

test('aynı anda 3 istek 401 alırsa refresh yalnızca 1 kez çağrılır', async () => {
  // Refresh cevabı biz bırakana kadar bekler, böylece üç isteğin hepsi 401'i refresh bitmeden alır.
  let releaseRefresh;
  handleRefresh = (config) =>
    new Promise((resolve) => {
      releaseRefresh = () => resolve(ok(config, { token: 'new-token' }));
    });

  const requests = Promise.all([apiService.getMe(), apiService.getBalance(), apiService.getHoldings()]);

  await waitFor(() => expect(refreshCalls).toHaveLength(1));
  // Diğer iki istek de 401'i işlesin diye biraz bekle; bekleme mantığı bozuksa burada ikinci refresh gelir.
  await new Promise((resolve) => setTimeout(resolve, 20));
  releaseRefresh();
  const responses = await requests;

  expect(refreshCalls).toHaveLength(1);
  expect(responses.map((r) => r.data.url)).toEqual([
    '/api/Auth/me',
    '/api/Portfolio/balance',
    '/api/Portfolio/holdings',
  ]);
});

test('refresh reddedilirse oturum temizlenir ve giriş sayfasına yönlendirilir', async () => {
  handleRefresh = unauthorized;

  await expect(apiService.getMe()).rejects.toBeTruthy();

  expect(refreshCalls).toHaveLength(1);
  expect(localStorage.getItem('token')).toBeNull();
  expect(localStorage.getItem('hasSession')).toBeNull();
  expect(sessionStorage.getItem(apiService.SESSION_MESSAGE_KEY)).toBe('auth.sessionExpired');
  expect(window.location.assign).toHaveBeenCalledWith('/signin');
});

test('backend kapalıyken API isteği hata alsa da oturum kapanmaz', async () => {
  handleRefresh = networkError;

  await expect(apiService.getMe()).rejects.toBeTruthy();

  expect(localStorage.getItem('hasSession')).toBe('1');
  expect(window.location.assign).not.toHaveBeenCalled();
});

test('başka sekme token\'ı yenilediyse refresh atılmadan yeni token ile denenir', async () => {
  // İstek eski token ile gitti, cevap gelmeden önce diğer sekme yeni token'ı kaydetti.
  handleRefresh = () => {
    throw new Error('refresh çağrılmamalı');
  };
  beforeProtectedRequest = (config) => {
    if (config.headers.Authorization === 'Bearer old-token') {
      localStorage.setItem('token', 'new-token');
    }
  };

  const response = await apiService.getMe();

  expect(response.data).toEqual({ url: '/api/Auth/me' });
  expect(refreshCalls).toHaveLength(0);
});

// ---------------- Çoklu sekme (Web Locks) ----------------

test('iki sekme aynı anda yenilerse kilit sayesinde tek refresh gider, ikisi de aynı token\'ı alır', async () => {
  installWebLocks();
  const fresh = jwtExpiringIn(600);
  handleRefresh = (config) => Promise.resolve(ok(config, { token: fresh }));
  localStorage.setItem('token', jwtExpiringIn(-60));
  const tabA = openTab();
  const tabB = openTab();

  const tokens = await Promise.all([tabA.getFreshAccessToken(), tabB.getFreshAccessToken()]);

  expect(refreshCalls).toHaveLength(1);
  expect(tokens).toEqual([fresh, fresh]);
});

test('access token silinmişken iki sekme birlikte açılırsa tek refresh gider ve kimse atılmaz', async () => {
  installWebLocks();
  const fresh = jwtExpiringIn(600);
  handleRefresh = (config) => Promise.resolve(ok(config, { token: fresh }));
  localStorage.removeItem('token');
  const tabA = openTab();
  const tabB = openTab();

  await Promise.all([tabA.getFreshAccessToken(), tabB.getFreshAccessToken()]);

  expect(refreshCalls).toHaveLength(1);
  expect(window.location.assign).not.toHaveBeenCalled();
});

test('kilit yokken refresh_token_rotated gelirse diğer sekmenin token\'ı kullanılır', async () => {
  handleRefresh = (config) => {
    setTimeout(() => localStorage.setItem('token', 'new-token'), 50);
    return unauthorized(config, { code: 'refresh_token_rotated' });
  };

  const response = await apiService.getMe();

  expect(response.data).toEqual({ url: '/api/Auth/me' });
  expect(refreshCalls).toHaveLength(1);
  expect(window.location.assign).not.toHaveBeenCalled();
});

test('refresh_token_rotated gelip diğer sekmeden token çıkmazsa oturum kapanır', async () => {
  handleRefresh = (config) => unauthorized(config, { code: 'refresh_token_rotated' });

  await expect(apiService.getMe()).rejects.toBeTruthy();

  expect(window.location.assign).toHaveBeenCalledWith('/signin');
});

test('giriş yapılmamış isteğin 401\'i olduğu gibi döner', async () => {
  localStorage.clear();

  await expect(apiService.getMe()).rejects.toMatchObject({ response: { status: 401 } });

  expect(refreshCalls).toHaveLength(0);
  expect(window.location.assign).not.toHaveBeenCalled();
});


// ---------------- SignalR için güncel token (#102) ----------------

test('getFreshAccessToken süresi dolmamış token\'ı refresh atmadan döner', async () => {
  const token = jwtExpiringIn(10 * 60);
  localStorage.setItem('token', token);

  await expect(apiService.getFreshAccessToken()).resolves.toBe(token);
  expect(refreshCalls).toHaveLength(0);
});

test('getFreshAccessToken süresi dolmuş ya da dolmak üzere olan token için önce refresh yapar', async () => {
  localStorage.setItem('token', jwtExpiringIn(10));

  await expect(apiService.getFreshAccessToken()).resolves.toBe('new-token');
  expect(refreshCalls).toHaveLength(1);
});

test('getFreshAccessToken access token kaybolmuşsa cookie ile yeniler', async () => {
  localStorage.removeItem('token');

  await expect(apiService.getFreshAccessToken()).resolves.toBe('new-token');
  expect(refreshCalls).toHaveLength(1);
});

test('hiç giriş yapılmamışsa getFreshAccessToken refresh denemez', async () => {
  localStorage.clear();

  await expect(apiService.getFreshAccessToken()).resolves.toBeNull();
  expect(apiService.hasSession()).toBe(false);
  expect(refreshCalls).toHaveLength(0);
});

test('getFreshAccessToken backend kapalıyken oturumu kapatmaz', async () => {
  localStorage.setItem('token', jwtExpiringIn(-60));
  handleRefresh = networkError;

  await expect(apiService.getFreshAccessToken()).rejects.toBeTruthy();
  expect(localStorage.getItem('hasSession')).toBe('1');
  expect(window.location.assign).not.toHaveBeenCalled();
});

test('getFreshAccessToken refresh token reddedilirse oturumu kapatır', async () => {
  localStorage.setItem('token', jwtExpiringIn(-60));
  handleRefresh = unauthorized;

  await expect(apiService.getFreshAccessToken()).rejects.toBeTruthy();
  expect(localStorage.getItem('token')).toBeNull();
  expect(window.location.assign).toHaveBeenCalledWith('/signin');
});
