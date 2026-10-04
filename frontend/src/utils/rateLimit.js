// Görev 88: Backend rate limit aşıldığında 429 döner.
// 429 ise kullanıcı dostu mesajı döndürür, değilse null (çağıran kendi hata mesajını kullanır).
export function getRateLimitMessage(err, t) {
    if (err?.response?.status !== 429) return null;

    const seconds = err.response.data?.retryAfterSeconds;
    return seconds
        ? t('auth.tooManyRequestsWithTime').replace('{seconds}', seconds)
        : t('auth.tooManyRequests');
}
