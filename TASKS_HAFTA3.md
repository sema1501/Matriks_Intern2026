# 3. Hafta Görevleri — Roobit Yönü (Exchange Özellikleri)

Bu hafta hedefimiz roobit.com'da gördüğümüz borsa/exchange deneyimine yaklaşmak:
favori coin takibi, fiyat alarmı, geçmiş grafik, piyasa özeti, admin kullanıcı yönetimi,
kurumsal sayfalar (duyurular/yasal) ve tutarlı bildirim sistemi.

Mevcut altyapıya dokunulmuyor: `trackedSymbols.js`, `coinMeta.js`, `binanceService.js` (yeni
fonksiyon eklenebilir ama mevcut fonksiyonlar değişmez), `useBinancePrices.js`, `PriceContext.jsx`.

---

## Görev 1 — Favoriler / İzleme Listesi (Backend)

**Açıklama**
Kullanıcıların belirli coin'leri favorilerine ekleyip listeleyebilmesi için backend altyapısı.

**Yapılacaklar**
- `Models/WatchlistItem.cs`: `UserId`, `Symbol`, `CreatedAt` alanları, migration oluştur
- `IWatchlistService` / `WatchlistService.cs`: `GetByUserAsync`, `AddAsync`, `RemoveAsync`
- `WatchlistController.cs`: `GET /api/Watchlist`, `POST /api/Watchlist/{symbol}`, `DELETE /api/Watchlist/{symbol}` — hepsi `[Authorize]`

**Kabul kriterleri**
- [ ] Swagger'da üç endpoint de çalışıyor
- [ ] Aynı symbol aynı kullanıcı için iki kez eklenemiyor (hata dönüyor)
- [ ] Bir kullanıcı sadece kendi listesini görüyor/değiştiriyor

---

## Görev 2 — Favoriler / İzleme Listesi (Frontend)

**Açıklama**
Görev 1'deki API'yi kullanarak coin kartlarına favori ekleme ve favori sayfası.

**Yapılacaklar**
- `apiService.js`'e `getWatchlist`, `addToWatchlist`, `removeFromWatchlist` ekle
- `CryptoCard.jsx`'e yıldız/favori butonu ekle (giriş yapılmamışsa tıklayınca `/signin`'e yönlendir)
- Yeni sayfa `src/pages/Watchlist/Watchlist.jsx` (`/watchlist` route) — sadece favori coinleri `CryptoGrid` mantığıyla göster
- `Navbar.jsx`'e giriş yapılmışsa "Favorilerim" linki ekle

**Kabul kriterleri**
- [ ] Favori ekleme/çıkarma anında UI'ya yansıyor
- [ ] Sayfa yenilenince favoriler backend'den doğru geliyor
- [ ] `/watchlist` PrivateRoute ile korunuyor

---

## Görev 3 — Piyasa Özeti: En Çok Yükselenler / Düşenler

**Açıklama**
Roobit tarzı bir piyasa özeti şeridi: 24 saatte en çok artan ve en çok düşen coin'ler.

**Yapılacaklar**
- `Home.jsx` üstüne iki liste/kart grubu: "Top 5 Yükselen", "Top 5 Düşen"
- `useGlobalPrices()`'tan gelen veriyi `priceChangePercentage24h`'a göre `useMemo` ile sırala
- Basit bir hero/özet bandı (toplam takip edilen coin sayısı, en yüksek hacim vb. — mevcut veriyle üretilebilenler)

**Kabul kriterleri**
- [ ] Liste canlı veriyle otomatik güncelleniyor, sayfa donmuyor
- [ ] Veri henüz gelmemişse iskelet/boş durum düzgün görünüyor
- [ ] Mobilde de okunabilir

---

## Görev 4 — Coin Detay Sayfası: Geçmiş Fiyat Grafiği

**Açıklama**
`CoinDetail.jsx`'e Binance geçmiş verisiyle çizilen bir fiyat grafiği eklemek.

**Yapılacaklar**
- `binanceService.js`'e `getKlines(symbol, interval)` fonksiyonu ekle (REST: `/api/v3/klines`)
- `CoinDetail.jsx`'te grafik kütüphanesi (`recharts` veya `lightweight-charts`) ile çizgi/mum grafiği göster
- Zaman aralığı butonları: 1 saat / 1 gün / 1 hafta / 1 ay
- İstek hatası durumunda kullanıcıya mesaj, sayfa çökmesin

**Kabul kriterleri**
- [ ] Grafik gerçek geçmiş veriyle çiziliyor
- [ ] Aralık değiştirilince grafik güncelleniyor
- [ ] API hatasında sayfa çökmüyor, hata mesajı görünüyor

---

## Görev 5 — Fiyat Alarmı (Backend)

**Açıklama**
Kullanıcının bir coin için hedef fiyat belirleyip alarm kurabilmesi.

**Yapılacaklar**
- `Models/PriceAlert.cs`: `UserId`, `Symbol`, `TargetPrice`, `Direction` (Above/Below), `IsTriggered`, migration
- `IAlertService` / `AlertService.cs`: `CreateAsync`, `GetByUserAsync`, `DeleteAsync`
- `AlertController.cs`: `POST /api/Alert`, `GET /api/Alert`, `DELETE /api/Alert/{id}` — `[Authorize]`

**Kabul kriterleri**
- [ ] Alarm oluşturulabiliyor, sadece kendi alarmların listeleniyor
- [ ] Alarm silinebiliyor
- [ ] Geçersiz veri (negatif fiyat vb.) reddediliyor

---

## Görev 6 — Fiyat Alarmı (Frontend & Bildirim)

**Açıklama**
Görev 5'teki API ile alarm kurma ve tetiklenince kullanıcıyı uyarma.

**Yapılacaklar**
- `CoinDetail.jsx`'te "Alarm Kur" formu (hedef fiyat + yön seçimi)
- Yeni sayfa veya `Profile.jsx` içine "Alarmlarım" listesi + silme butonu
- `PriceContext` üzerinden gelen canlı fiyat hedefe ulaştığında toast/tarayıcı bildirimi göster (kontrol frontend'de yapılır, backend sadece kaydı tutar)
- Aynı alarm birden fazla kez bildirim spamlamasın (bir kez tetiklenince işaretle)

**Kabul kriterleri**
- [ ] Hedef fiyata ulaşılınca bildirim/toast çıkıyor
- [ ] Tetiklenen alarm tekrar tekrar bildirim üretmiyor
- [ ] Alarm listesi silinebiliyor

---

## Görev 7 — TRY/USD Dönüştürücü

**Açıklama**
Roobit gibi TL bazlı görüntüleme ve basit bir miktar dönüştürücü.

**Yapılacaklar**
- Header'da USD/TRY toggle (ThemeToggle'a benzer bir bileşen)
- Sabit veya basit bir kurdan (örn. bir kur API'sinden çekilen) tüm fiyatları TRY'ye çevirme mantığı (Context ile paylaşılabilir)
- Yeni sayfa `/converter`: kullanıcı miktar + coin seçip karşılık gelen USD/TRY değerini görsün

**Kabul kriterleri**
- [ ] Toggle tüm sayfalarda (kart, detay) tutarlı çalışıyor
- [ ] Converter doğru hesaplama yapıyor
- [ ] Kur bilgisi alınamazsa kullanıcıya bilgi veriliyor (sessizce yanlış veri göstermiyor)

---

## Görev 8 — Admin Panel: Kullanıcı Yönetimi Tablosu

**Açıklama**
Mevcut `GetAllAsync` ve rol endpoint'lerini kullanarak admin için kullanıcı yönetim ekranı.

**Yapılacaklar**
- `Dashboard.jsx` içine veya yeni `/admin/users` sayfasına tüm kullanıcıları listeleyen tablo
- Kullanıcı adı/e-posta ile arama, role göre filtreleme
- Satır içinde rol atama/kaldırma (mevcut `RoleService` endpoint'lerini kullan, backend değişikliği gerekmez)

**Kabul kriterleri**
- [ ] Sadece Admin/SuperAdmin bu sayfayı görebiliyor
- [ ] Arama ve filtreleme çalışıyor
- [ ] Rol atama/kaldırma tablo üzerinden anında yansıyor

---

## Görev 9 — Geri Bildirim (Feedback) Formu

**Açıklama**
Kullanıcıların uygulama hakkında kısa geri bildirim/şikayet/öneri gönderebilmesi
ve adminlerin bunları görebilmesi.

**Yapılacaklar**
- `Models/Feedback.cs`: `UserId` (nullable — giriş yapmamış kullanıcı da gönderebilsin),
  `Message`, `Rating` (1-5, opsiyonel), `CreatedAt`, migration
- `IFeedbackService` / `FeedbackService.cs`: `CreateAsync`, `GetAllAsync`
- `FeedbackController.cs`: `POST /api/Feedback` (herkes gönderebilir), `GET /api/Feedback` (sadece Admin/SuperAdmin)
- Frontend: Footer veya Navbar'dan erişilen basit bir "Geri Bildirim" sayfası/modalı —
  mesaj alanı + opsiyonel 1-5 yıldız/puan, gönderince başarı mesajı
- Admin tarafında gelen geri bildirimleri görebileceği bir liste (Dashboard'a eklenebilir)

**Kabul kriterleri**
- [ ] Boş mesajla gönderim yapılamıyor
- [ ] Giriş yapmamış kullanıcı da form gönderebiliyor
- [ ] Sadece Admin/SuperAdmin `GET /api/Feedback` ile listeyi görebiliyor
- [ ] Gönderim sonrası kullanıcıya net bir başarı/hata mesajı gösteriliyor

---

## Görev 10 — Giriş / Kayıt Ekranlarının Görsel Yenilenmesi (UI/UX)

**Açıklama**
`SignIn.jsx` ve `SignUp.jsx` şu anda düz inline-style'lı formlar; roobit.com'a yakışacak
şekilde, projenin `theme.css` değişkenlerini kullanan modern bir görünüme kavuşturulacak.

**Yapılacaklar**
- Inline style'ları kaldırıp `theme.css`'teki CSS değişkenlerini (`--bg-primary`, `--text-primary`,
  `--card-bg` vb.) kullanan ortalanmış bir kart tasarımı (logo/başlık, gölge, yuvarlatılmış köşeler)
- Şifre alanlarına göster/gizle (göz ikonu) toggle'ı ekle
- Input hata durumlarını (boş alan, eşleşmeyen şifre vb.) alan bazında, kırmızı çerçeve +
  mesajla göster — sadece üstte tek satır hata yerine
- Buton loading state'ini görsel bir spinner ile güçlendir, buton genişliği/hizası tutarlı olsun
- Açık/koyu tema ile bu sayfaların da uyumlu çalıştığından emin ol (`ThemeToggle` zaten var)
- Mobilde tam ekran genişliğinde, kenar boşlukları düzgün bir görünüm

**Kabul kriterleri**
- [ ] Giriş ve kayıt sayfaları görsel olarak tutarlı bir kart tasarımına sahip
- [ ] Şifre göster/gizle toggle'ı çalışıyor
- [ ] Alan bazlı hata mesajları doğru gösteriliyor
- [ ] Açık ve koyu temada sayfa okunabilir/tutarlı görünüyor
- [ ] Mobil genişlikte tasarım bozulmuyor

---

## Görev 11 — Şifremi Unuttum / Şifre Sıfırlama (Backend + Frontend)

**Açıklama**
Şu an şifresini unutan kullanıcı için hiçbir kurtarma yolu yok. Email/username ile
şifre sıfırlama akışı ekleniyor (gerçek SMTP kurulumu bu haftanın kapsamı dışında —
geliştirme ortamında token backend log'una veya response'a yazdırılabilir).

**Yapılacaklar**
- `Models/PasswordResetToken.cs`: `UserId`, `Token`, `ExpiresAt`, `IsUsed`, migration
- `IAuthService`'e ekle: `ForgotPasswordAsync(email)` — kullanıcı bulunursa token üretir
  (örn. 30 dk geçerli), bulunamasa da aynı generic mesajı döner (email enumeration önlenir)
- `ResetPasswordAsync(token, newPassword)` — token'ı doğrula (süresi geçmiş/kullanılmış mı),
  şifreyi BCrypt ile hashleyip güncelle, token'ı `IsUsed = true` yap
- `AuthController.cs`: `POST /api/Auth/forgot-password`, `POST /api/Auth/reset-password`
- Frontend: `SignIn.jsx`'e "Şifremi unuttum" linki; `ForgotPassword.jsx` (email formu) ve
  `ResetPassword.jsx` (`/reset-password/:token` route, yeni şifre formu) sayfaları

**Kabul kriterleri**
- [ ] Var olmayan email için de aynı generic başarı mesajı dönüyor
- [ ] Süresi dolmuş token ile sıfırlama reddediliyor
- [ ] Kullanılmış token tekrar kullanılamıyor
- [ ] Şifre sıfırlandıktan sonra kullanıcı yeni şifreyle giriş yapabiliyor

---

## Genel Kurallar

- Görev 1→2, 5→6 sıralı bağımlılık (önce backend, sonra frontend). 3, 4, 7, 8, 9, 10, 11 paralel yürütülebilir.
- Yeni model eklerken migration'ı mutlaka oluşturun (`dotnet ef migrations add ...`).
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli.
