# 11. Hafta Görevleri — Oturum Yenileme, Bot Risk Yönetimi, Portföy Değer Geçmişi

**Bağlam:** `TASKS_HAFTA10.md`'deki üç görev (CI/CD, rate limiting, CSV export) PR #90,
#92 ve #93 ile `main`'e taşındı ve tamamlandı. Bu haftanın görevleri, kod tabanı
incelenirken somut olarak eksik bulunan üç noktadan çıkarıldı. Üçü de orta zorlukta;
her biri hem backend hem frontend değişikliği içeriyor.

Bu hafta tek stajyer yürütecek. Görevler birbirinden bağımsız; önerilen sıra:
**1 → 2 → 3** (önce her sayfayı etkileyen oturum sorunu, sonra bot tarafı, en son yeni
bir arka plan servisi ve grafik gerektiren portföy geçmişi).

> **Numaralandırma:** `TASKS_HAFTA10.md`'deki karar aynen geçerli — her görev için
> `.github/ISSUE_TEMPLATE/gorev.md` şablonuyla bir GitHub Issue aç, branch'i
> `feature/<issue-no>-kisa-ad` biçiminde adlandır, commit ve PR başlıklarını
> `#<issue-no> ...` ile başlat. Kod içine `// Görev NN` yorumu ekleme.

---

## 1. Oturum Süresi Dolunca Düzgün Çıkış ve Refresh Token

**Açıklama**
`JwtService` token'ı 8 saat geçerli üretiyor (`DateTime.UtcNow.AddHours(8)`), ama
frontend bu sürenin dolmasını hiç ele almıyor. `apiService.js`'te yalnızca istek
interceptor'ı var; yanıt interceptor'ı yok. `AuthContext.js` token'ı yalnızca sayfa ilk
açıldığında (`getMe()`) kontrol ediyor. Sonuç: sekmesi uzun süre açık kalan bir kullanıcı
8 saat sonra her istekte 401 alıyor, sayfalar boş ya da genel bir hata mesajıyla kalıyor,
ve kullanıcı ne olduğunu anlamıyor. Kullanıcıyı her 8 saatte bir yeniden giriş yapmaya
zorlamak yerine kısa ömürlü access token + uzun ömürlü refresh token yapısına geçilecek.

**Yapılacaklar**
- Backend `Models/RefreshToken.cs`: `UserId`, `TokenHash` (token'ın kendisi değil, SHA-256
  hash'i saklanmalı — dikkat: mevcut `PasswordResetToken` token'ı düz metin saklıyor, onu
  örnek alma),
  `ExpiresAt`, `CreatedAt`, `RevokedAt`, `ReplacedByTokenHash`. `AppDbContext`'e `DbSet`
  ekle, migration oluştur (`dotnet ef migrations add AddRefreshTokens`)
- `JwtService`: access token süresini kısalt (örn. 15 dk, `appsettings.json`'dan
  okunabilir olsun), kriptografik olarak güvenli rastgele refresh token üretimi ekle
  (`RandomNumberGenerator`) — refresh token geçerliliği örn. 7 gün
- `AuthService.LoginAsync`: yanıtta access token'ın yanında refresh token da dönsün
  (`DTOs/AuthDtos.cs`'teki login yanıtını güncelle)
- Yeni uçlar: `POST /api/Auth/refresh` (refresh token alır, yeni access + **yeni**
  refresh token döner, eskisini iptal eder — *token rotation*) ve `POST /api/Auth/logout`
  (verilen refresh token'ı iptal eder)
- Güvenlik: iptal edilmiş (zaten kullanılmış) bir refresh token tekrar gelirse bu bir
  çalınma belirtisidir — o kullanıcının tüm aktif refresh token'larını iptal et
- Şifre değiştirme (`me/password`) ve şifre sıfırlama (`reset-password`) başarılı
  olduğunda kullanıcının tüm refresh token'ları iptal edilsin
- `refresh` ucuna da 10. haftadaki rate limiting politikası (`RateLimiting/RateLimitingSetup.cs`) uygulansın
- Frontend `services/apiService.js`: yanıt interceptor'ı ekle — 401 gelince bir kez
  `refresh` çağır, başarılıysa orijinal isteği yeni token'la tekrar dene. Aynı anda
  birden fazla istek 401 alırsa yalnızca **tek** bir refresh isteği atılmalı (diğerleri
  onu beklemeli). Refresh de başarısızsa token'ları temizle ve kullanıcıyı
  "Oturumunuzun süresi doldu, lütfen tekrar giriş yapın" mesajıyla `/signin`'e yönlendir
- `context/AuthContext.js`: `loginUser`/`logoutUser` refresh token'ı da saklasın/silsin;
  `logoutUser` backend'deki `logout` ucunu da çağırsın
- Yeni metinler TR/EN dil dosyalarına (`LanguageContext`) eklensin

**Kabul kriterleri**
- [ ] Access token süresi dolduğunda kullanıcı fark etmeden oturum yenileniyor, sayfa
      çalışmaya devam ediyor (test için access token süresini geçici olarak 1 dk yap)
- [ ] Refresh token da geçersizse kullanıcı anlaşılır bir mesajla giriş sayfasına
      yönlendiriliyor, boş/çökmüş sayfa görünmüyor
- [ ] Aynı anda birden fazla istek 401 aldığında Network sekmesinde yalnızca bir
      `refresh` isteği görünüyor
- [ ] Kullanılmış bir refresh token ikinci kez gönderildiğinde reddediliyor ve
      kullanıcının diğer oturumları da iptal ediliyor
- [ ] Çıkış yap ve şifre değiştirme sonrası eski refresh token çalışmıyor
- [ ] Veritabanında refresh token düz metin olarak değil, hash olarak duruyor
- [ ] `refresh`, `logout` ve rotation/yeniden kullanım senaryoları için testler yazıldı

---

## 2. Bot: Stop-Loss ve Take-Profit

**Açıklama**
`TradingBot` modelinde yalnızca giriş/çıkış sinyali üreten strateji parametreleri var
(RSI eşikleri veya EMA periyotları). Fiyat ters yöne giderse botun zararı sınırlamasının
ya da hedeflenen kâra ulaşınca pozisyonu kapatmasının hiçbir yolu yok — bot ancak
strateji SAT sinyali üretirse satıyor. Bu, gerçek alım-satım botlarındaki en temel risk
yönetimi özelliği. Anlık geri bildirim mekanizmamız da olsun botun hareketleri için.

**Yapılacaklar**
- `Models/TradingBot.cs`: `StopLossPercent` ve `TakeProfitPercent` alanları ekle
  (`decimal?` — null ise devre dışı; EMA alanlarının neden nullable olduğuna dair modeldeki
  açıklamaya bak). Migration oluştur; mevcut botlar etkilenmemeli (ikisi de null kalmalı)
- `TradingBot.Validate()`: değerler verilmişse pozitif olmalı, stop-loss %100'den küçük
  olmalı
- Botun açık pozisyonunun giriş fiyatını belirle: botun son başarılı (`Approved`) AL
  sinyalinin `PriceAtSignal` değeri. Son başarılı sinyal SAT ise açık pozisyon yok
  demektir
- `BotMonitorService`: her değerlendirmede, strateji kontrolünden **önce**, açık pozisyon
  varsa güncel fiyatı giriş fiyatıyla karşılaştır; `StopLossPercent` kadar düşmüşse ya da
  `TakeProfitPercent` kadar yükselmişse `BotAutoTradeExecutor` üzerinden SAT emri gönder.
  Bu karşılaştırma mantığını ayrı, saf (veritabanına dokunmayan) bir sınıfa koy
  (`RsiSignalEvaluator` / `EmaCrossoverEvaluator` desenine uygun, örn.
  `RiskExitEvaluator`) — böylece kolay test edilir
- `BotSignal`'a sinyalin nedenini kaydet (`Reason` enum: `Strategy`, `StopLoss`,
  `TakeProfit`); migration'da mevcut kayıtlar `Strategy` olmalı. E-posta bildirimi
  (`NotificationService`) bu nedeni metne yansıtsın
- `BacktestService.Simulate`: stop-loss/take-profit backtest'te de uygulanmalı, sinyal
  tablosunda neden görünmeli (`DTOs/BacktestDtos.cs`)
- `DTOs/BotDtos.cs` ve `BotService.CreateBotAsync`: yeni alanları al, doğrula, dön
- Frontend `pages/Bot/Bot.jsx`: bot oluşturma formuna isteğe bağlı iki alan (Stop-loss %,
  Take-profit %), bot kartında ayarlı değerlerin gösterimi; sinyal listesinde ve
  `BacktestReport`'ta sinyal nedeni (rozet/etiket olarak). Metinler TR/EN

**Kabul kriterleri**
- [ ] Stop-loss/take-profit ayarlanmamış mevcut botlar eskisi gibi çalışıyor
- [ ] Fiyat giriş fiyatına göre stop-loss oranında düştüğünde bot satış yapıyor ve sinyal
      `StopLoss` nedeniyle kaydediliyor (take-profit için de aynısı)
- [ ] Açık pozisyon yokken (son başarılı sinyal SAT ise) stop-loss/take-profit hiçbir
      emir üretmiyor; aynı pozisyon için iki kez satış yapılmıyor
- [ ] Backtest, aynı ayarlarla stop-loss/take-profit çıkışlarını sinyal tablosunda ve
      özet kâr/zararda doğru gösteriyor
- [ ] Geçersiz değerler (negatif, sıfır, stop-loss ≥ %100) hem backend'de hem formda
      reddediliyor
- [ ] `RiskExitEvaluator` için sınır değerleri de içeren birim testleri, backtest için
      en az bir stop-loss senaryosu testi yazıldı

---

## 3. Portföy Değer Geçmişi Grafiği

**Açıklama**
`Portfolio.jsx` kullanıcının bakiyesini, holding'lerini, kâr/zararını ve dağılım pasta
grafiğini **yalnızca o anki** haliyle gösteriyor. Portföy değerinin zaman içinde nasıl
değiştiği hiçbir yerde saklanmıyor — kullanıcı "geçen haftaya göre ne durumdayım?"
sorusunu cevaplayamıyor. Liderlik tablosu da yalnızca anlık değere bakıyor.

**Yapılacaklar**
- `Models/PortfolioSnapshot.cs`: `UserId`, `TakenAt` (UTC), `CashBalance`,
  `HoldingsValue`, `TotalValue`. `(UserId, TakenAt)` için index ekle; migration oluştur
- Yeni arka plan servisi `Services/PortfolioSnapshotService.cs`
  (`AlertMonitorService`/`BotMonitorService` desenini izle, `IClock` kullan): saatte bir
  tüm kullanıcılar için anlık görüntü alsın. Fiyatlar için mevcut `BinancePriceService`'i
  kullan — kullanıcı başına ayrı Binance isteği **atma**, her turda fiyatları bir kez çek
- Aynı saat içinde ikinci kez çalışırsa (örn. uygulama yeniden başladı) aynı kullanıcıya
  çift kayıt oluşmasın
- Saklama politikası: 30 günden eski saatlik kayıtlar günde bire indirgensin (her gün
  için son kayıt kalsın) — tablo sınırsız büyümesin
- Yeni uç: `GET /api/Portfolio/history?range=24h|7d|30d|all` — yalnızca giriş yapan
  kullanıcının kayıtlarını dönsün
- Frontend `Portfolio.jsx`: üst kısma toplam değerin zaman çizgi grafiği + aralık
  seçici (24s / 7g / 30g / Tümü) + seçilen aralıktaki değişim (USD ve %). Grafik için
  projede zaten bulunan `klinecharts` ya da basit bir SVG kullanılabilir — yeni bir
  grafik kütüphanesi eklemeden önce Sema'ya sor. Henüz kayıt yoksa "Geçmiş verisi
  birikiyor" gibi bir boş durum mesajı göster. Metinler TR/EN, açık/koyu tema uyumlu

**Kabul kriterleri**
- [ ] Servis çalışırken her kullanıcı için saatte bir kayıt oluşuyor; uygulama aynı saat
      içinde yeniden başlatılınca çift kayıt oluşmuyor
- [ ] Kaydedilen `TotalValue`, o anda Portföy sayfasında görünen toplam değerle tutarlı
- [ ] Bir tur sırasında Binance'e kullanıcı sayısından bağımsız olarak sabit sayıda
      istek atılıyor (loglardan doğrula)
- [ ] 30 günden eski veriler günlüğe indirgeniyor, daha yeni veriler bozulmuyor
- [ ] Başka bir kullanıcının geçmişine hiçbir parametreyle erişilemiyor
- [ ] Grafik aralık değişince doğru veriyi gösteriyor, veri yokken sayfa çökmüyor
- [ ] Snapshot hesaplama, çift kayıt önleme ve saklama politikası için testler yazıldı
      (`IClock` mock'lanarak, gerçek zaman beklemeden)

---

## Genel Kurallar

- Her görev için ayrı branch ve ayrı PR; PR açıklamasında `.github/pull_request_template.md`
  doldurulmalı ve ilgili issue bağlanmalı (`Closes #NN`).
- Migration ekleyen görevlerde (üçü de ekliyor) PR'lar sırayla merge edilecek; kendi
  branch'ini `main`'e güncel tutmadan migration oluşturma — çakışma olursa
  `dotnet ef migrations remove` ile geri alıp güncel `main` üzerinde yeniden oluştur.
- Yeni kullanıcıya görünen her metin TR/EN olarak eklenmeli (`LanguageContext`).
- CI (`backend-ci.yml`, `frontend-ci.yml`) yeşil olmadan PR merge edilmez; yerelde de
  `dotnet build`, `dotnet test` ve `npm run build` hatasız geçmeli.
- Swagger'da yeni uçlar görünmeli; `README.md`'deki "Özellikler" ve "API Uçları"
  bölümleri yapılan değişikliğe göre güncellenmeli.
