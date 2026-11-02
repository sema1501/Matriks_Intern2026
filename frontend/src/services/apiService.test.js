// apiService'teki 401 yakalayıcısının testleri. Gerçek backend'e gidilmez: axios'un adapter'ı
// sahte bir fonksiyonla değiştirilir, her istek bu fonksiyona düşer.

import { waitFor } from '@testing-library/react';

let axios;
let apiService;
let refreshCalls;
let handleRefresh;
let beforeProtectedRequest;

const ok = (config, data) => ({ data, status: 200, statusText: 'OK', headers: {}, config, request: {} });

const unauthorized = (config) => {
  const response = { data: {}, status: 401, statusText: 'Unauthorized', headers: {}, config, request: {} };
  return Promise.reject(new axios.AxiosError('401', 'ERR_BAD_REQUEST', config, {}, response));
};

// Korunan uçlar yalnızca "new-token" ile 200 döner; eski token 401 alır.
const fakeAdapter = (config) => {
  if (config.url === '/api/Auth/refresh') {
    refreshCalls.push(JSON.parse(config.data));
    return handleRefresh(config);
  }
  beforeProtectedRequest(config);
  if (config.headers.Authorization === 'Bearer new-token') {
    return Promise.resolve(ok(config, { url: config.url }));
  }
  return unauthorized(config);
};

const refreshSucceeds = (config) =>
  Promise.resolve(ok(config, { token: 'new-token', refreshToken: 'new-refresh' }));

const originalLocation = window.location;

beforeEach(() => {
  // Her testte modüller baştan yüklenir: interceptor'daki refreshPromise testler arasında taşınmaz.
  jest.resetModules();
  axios = require('axios');
  axios.defaults.adapter = fakeAdapter;
  apiService = require('./apiService');

  refreshCalls = [];
  handleRefresh = refreshSucceeds;
  beforeProtectedRequest = () => {};
  localStorage.clear();
  sessionStorage.clear();
  localStorage.setItem('token', 'old-token');
  localStorage.setItem('refreshToken', 'old-refresh');

  // jsdom sayfa değiştirmeyi desteklemiyor; yönlendirme sahte assign ile yakalanır.
  delete window.location;
  window.location = { pathname: '/portfolio', assign: jest.fn() };
});

afterEach(() => {
  window.location = originalLocation;
});

test('401 alan istek refresh sonrası yeni token ile tekrarlanır', async () => {
  const response = await apiService.getMe();

  expect(response.data).toEqual({ url: '/api/Auth/me' });
  expect(refreshCalls).toEqual([{ refreshToken: 'old-refresh' }]);
  expect(localStorage.getItem('token')).toBe('new-token');
  expect(localStorage.getItem('refreshToken')).toBe('new-refresh');
});

test('aynı anda 3 istek 401 alırsa refresh yalnızca 1 kez çağrılır', async () => {
  // Refresh cevabı biz bırakana kadar bekler, böylece üç isteğin hepsi 401'i refresh bitmeden alır.
  let releaseRefresh;
  handleRefresh = (config) =>
    new Promise((resolve) => {
      releaseRefresh = () => resolve(ok(config, { token: 'new-token', refreshToken: 'new-refresh' }));
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

test('refresh de başarısız olursa oturum temizlenir ve giriş sayfasına yönlendirilir', async () => {
  handleRefresh = unauthorized;

  await expect(apiService.getMe()).rejects.toBeTruthy();

  expect(refreshCalls).toHaveLength(1);
  expect(localStorage.getItem('token')).toBeNull();
  expect(localStorage.getItem('refreshToken')).toBeNull();
  expect(sessionStorage.getItem(apiService.SESSION_MESSAGE_KEY)).toBe('auth.sessionExpired');
  expect(window.location.assign).toHaveBeenCalledWith('/signin');
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

test('giriş yapılmamış isteğin 401\'i olduğu gibi döner', async () => {
  localStorage.clear();

  await expect(apiService.getMe()).rejects.toMatchObject({ response: { status: 401 } });

  expect(refreshCalls).toHaveLength(0);
  expect(window.location.assign).not.toHaveBeenCalled();
});

