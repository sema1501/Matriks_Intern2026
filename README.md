# CryptoTracker — Staj Projesi

Canlı kripto para takibi, teknik analiz ve sanal (Binance Testnet üzerinden) alım-satım
botu içeren full-stack bir uygulama.

**Stack:** ASP.NET Core 9 · React 18 · MSSQL · Docker · JWT · Binance API (public + Testnet) · klinecharts

---

## Kurulum

### Gereksinimler
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### Önemli not
- Komutları depo kökünden başlatın.
- `backend/src/CryptoTracker.API` klasörüne gidin; `backend/backend/src/...` gibi çift `backend` yolu kullanmayın.
- SQL Server konteynerini çalıştırmadan önce Docker Desktop açık olmalı.

### 1. Repoyu klonla
```bash
git clone <repo-url>
cd CryptoTracker
```

### 2. Veritabanını başlat
```bash
cd backend
docker-compose up -d db
# ~15 saniye bekle
```

### 3. Backend ayarlarını tamamla

`backend/src/CryptoTracker.API/appsettings.Development.json` dosyasını oluşturun
(bu dosya `.gitignore`'da — repoda yok, elle oluşturmanız gerekiyor):

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=127.0.0.1,14330;Database=CryptoTrackerDb;User Id=SA;Password=YourStrong@Passw0rd;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Key": "en-az-32-karakterlik-kendi-secret-keyiniz"
  },
  "BinanceTestnet": {
    "ApiKey": "testnet.binance.vision üzerinden aldığınız key",
    "ApiSecret": "testnet.binance.vision üzerinden aldığınız secret"
  }
}
```

Binance Testnet key/secret olmadan da uygulama açılır — sadece alım-satım botu gerçek
emir gönderemez, `TestnetController` hata döner. Key almak için: `testnet.binance.vision`
adresine GitHub hesabınızla giriş yapıp bir HMAC API key oluşturun. **Bu değerleri asla
`appsettings.json`'a yazmayın veya commit etmeyin.**

### 4. Backend'i çalıştır
```bash
cd backend/src/CryptoTracker.API
dotnet restore
dotnet ef database update
dotnet run
# → http://localhost:5002
# → http://localhost:5002/swagger
```

Eğer `dotnet run` farklı bir port açarsa, [backend/src/CryptoTracker.API/Properties/launchSettings.json](./backend/src/CryptoTracker.API/Properties/launchSettings.json) dosyasının uygulandığını kontrol edin.

### Yaygın hata nedenleri
- Docker Desktop kapalıysa `docker-compose up -d db` çalışmaz.
- Komutlar yanlış klasörde çalıştırılırsa `No project was found` hatası gelir.
- `dotnet ef database update` için ilk migration'ın repoda bulunması gerekir.
- `JWT Key is not configured` hatası, `appsettings.Development.json` eksikse oluşur.
- Migration çakışması yaşarsanız: aynı tabloyu iki farklı migration ekliyor olabilir —
  `dotnet ef migrations remove` ile geri alıp tek migration'da yeniden oluşturun.

### 5. Frontend'i çalıştır
```bash
cd frontend
npm install
npm start
# → http://localhost:3000
```

### 6. Testleri çalıştır (opsiyonel)
```bash
cd backend
dotnet test
```

### Test kullanıcısı (otomatik oluşturulur)
| Alan | Değer |
|------|-------|
| Username | admin |
| Password | Admin123! |
| Roller | Admin, User |

> Bu bilgiler kaynak kodda (`Data/DataSeeder.cs`) açıkça yazılı — repo public olduğu için
> bilerek burada. Eğer proje internete açık bir yere deploy edilirse bu hesabın şifresi
> mutlaka değiştirilmeli/silinmelidir.

---

## Özellikler

**Kimlik & Kullanıcı**
Kayıt/giriş (JWT), **e-posta doğrulama zorunluluğu** (doğrulanmamış hesap giriş
yapamaz), şifremi unuttum/sıfırlama (bağlantı e-postayla gönderilir), profil
görüntüleme/düzenleme, **profil fotoğrafı** (en fazla ~3 MB), şifre değiştirme,
e-posta bildirimi açma/kapama, rol yönetimi (Admin/SuperAdmin/User).

**Kripto Takip**
Binance WebSocket üzerinden 20 coin için canlı fiyat akışı, arama/sıralama, favori
listesi (watchlist), coin detay sayfası, **iki coini yan yana karşılaştırma (Compare)**,
TRY/USD dönüştürücü, açık/koyu tema, **Türkçe/İngilizce dil seçimi**.

**Grafik & Teknik Analiz**
`klinecharts` ile mum/OHLC grafiği, zaman aralığı seçimi, RSI/EMA/Bollinger Bands
indikatörleri, manuel trend çizgisi çizimi (uzatma, tür değiştirme).

**Fiyat Alarmları**
Dakikalık/saatlik/günlük periyotlarla arka planda (`AlertMonitorService`) çalışan kalıcı
alarm sistemi — kullanıcı uygulamada olmasa bile Binance verisine göre kontrol edilir.
İki alarm tipi: **sabit fiyat** (hedef fiyatın üstü/altı) ve **yüzde değişim** (alarm
kurulduğundaki fiyata göre en az %X yükseliş/düşüş).

**E-posta Bildirimi**
Alarm veya bot sinyali tetiklendiğinde kullanıcıya e-posta gider (uygulama kapalıyken de).
Aynı alarm/bot için saatte en fazla 1 e-posta (cooldown). SMTP ayarı yoksa e-posta içeriği
loglanır. Profil sayfasından kapatılabilir.

**Sanal Portföy**
Her kullanıcı 10.000 USD sanal bakiye ile başlar, coin detay sayfasından alım/satım
yapabilir, işlem geçmişini (sayfalı, sembol/tür filtreli) ve kâr/zararını görebilir,
portföy dağılımını pasta grafikte izleyebilir. Kullanıcılar arası liderlik tablosu.

**Alım-Satım Botu**
İki strateji seçeneği olan, arka planda (`BotMonitorService`) periyodik kontrol yapan bir
bot: **RSI eşiği** (RSI alım/satım bölgesine girince) veya **EMA kesişimi** (kısa EMA uzun
EMA'yı yukarı keserse AL, aşağı keserse SAT). Sinyal oluşunca **Binance Testnet**'e gerçek (sahte parayla) emir gönderir; sonuç
kullanıcının sanal portföy defterine işlenir. Seçilen tarih aralığında botun geçmişte nasıl
sonuç verdiğini gösteren **backtest** raporu (grafik üzerinde alış/satış okları + RSI paneli
+ sinyal tablosu) — her iki strateji için de çalışır.

**Admin Paneli**
Tüm kullanıcıların bot ve portföy aktivitesini görüntüleme, şüpheli/aşırı işlem yapan
botları tespit etme ve durdurma (kill switch), denetim günlüğü (audit log), geri bildirim
listesi, haftalık yeni kullanıcı istatistiği.

**Diğer**
Kullanıcı geri bildirim formu.

---

## API Uçları (özet)

Tüm uçların ayrıntılı şeması için backend çalışırken `/swagger` adresine bakın. Kısa özet:

| Controller | Ne işe yarar |
|---|---|
| `AuthController` | Kayıt (`register`), e-posta doğrulama (`confirm-email`), giriş (`login`), şifre sıfırlama (`forgot-password`, `reset-password`), profil (`GET/PUT me`), şifre değiştirme (`me/password`), e-posta bildirim tercihi (`me/notifications`), profil fotoğrafı (`me/avatar`), kullanıcı listesi (`users`) |
| `RoleController` | Rol listeleme/oluşturma/atama (Admin) |
| `WatchlistController` | Favori coin ekleme/çıkarma |
| `AlertController` | Fiyat / yüzde-değişim alarmı oluşturma, listeleme, silme, açma-kapama (`toggle`), tetiklenme geçmişi (`signals`) |
| `PortfolioController` | Bakiye, holdings, işlem geçmişi (sayfalı + filtreli), alım/satım, liderlik tablosu |
| `BotController` | Bot oluşturma (RSI / EMA), aç-kapa, silme, sinyaller, performans, backtest |
| `FeedbackController` | Geri bildirim gönderme/listeleme (Admin) |
| `TestnetController` | Binance Testnet hesap durumu (Admin, debug amaçlı) |
| `AdminController` | Tüm bot/portföy gözetimi (`bots`, `portfolios`), kill switch (`bots/{id}/kill`), işaretleme (`Bot/{id}/flag`), aşırı işlem listesi (`overtrading`), audit log (`audit-log`) |
| `DashboardController` | Günlük/haftalık yeni kullanıcı istatistiği (`daily-new-users`, Admin) |

---

## Proje Yapısı

```
CryptoTracker/
├── backend/
│   ├── docker-compose.yml
│   ├── Dockerfile
│   ├── src/CryptoTracker.API/
│   │   ├── Controllers/     ← API endpoint'leri (yukarıdaki tablo)
│   │   ├── Models/          ← Veritabanı modelleri
│   │   ├── DTOs/             ← Request / Response nesneleri
│   │   ├── Services/        ← İş mantığı (bot, alarm, backtest, testnet, vb.)
│   │   ├── Data/             ← DbContext + DataSeeder
│   │   ├── Migrations/      ← EF Core migration geçmişi
│   │   └── Middleware/      ← Hata yakalama
│   └── tests/CryptoTracker.API.Tests/  ← Servis ve entegrasyon testleri
└── frontend/
    └── src/
        ├── components/       ← Navbar, CryptoCard/Grid, ChartModule, IndicatorPanel,
        │                        BacktestReport, TradeForm, BotSignalApproval,
        │                        LanguageToggle, vb.
        ├── pages/            ← Home, SignIn/Up, ConfirmEmail, Profile, Dashboard,
        │                        Watchlist, CoinDetail, Compare, Portfolio, Bot,
        │                        AdminBots, Leaderboard, Converter, Feedback,
        │                        ForgotPassword/ResetPassword
        ├── services/         ← API çağrıları (apiService.js), binanceService.js
        ├── hooks/            ← useBinancePrices
        └── context/          ← Auth, Price, Theme, Currency, Watchlist, Language
```

---

## Görev Geçmişi

Her haftanın görev tanımları ayrı dosyalarda tutuluyor:

| Dosya | İçerik |
|---|---|
| [TASKS_HAFTA3.md](./TASKS_HAFTA3.md) | Auth, kullanıcı/rol yönetimi, watchlist, alarm, converter, admin, feedback |
| [TASKS_HAFTA4.md](./TASKS_HAFTA4.md) | Grafik modülü (klinecharts), EMA/RSI/Bollinger, trend çizimi, kalıcı alarm |
| [TASKS_HAFTA5.md](./TASKS_HAFTA5.md) | Sanal portföy / mock alım-satım, liderlik tablosu |
| [TASKS_HAFTA6.md](./TASKS_HAFTA6.md) | RSI tabanlı alım-satım botu (onaylı sinyal modeli) |
| [TASKS_HAFTA7.md](./TASKS_HAFTA7.md) | Binance Testnet entegrasyonu, onay akışının kaldırılması, backtest, trend çizgisi menüsü |
| [TASKS_HAFTA8.md](./TASKS_HAFTA8.md) | Admin gözetim paneli, kill switch, audit log, e-posta bildirimi (EMA → 9. haftaya taşındı) |
| [TASKS_HAFTA9.md](./TASKS_HAFTA9.md) | EMA kesişimi stratejisi (canlı + backtest), test kapsamı, dokümantasyon güncellemesi |
| [TASKS_ARSIV.md](./TASKS_ARSIV.md) | Kodda olup hiçbir haftalık dosyaya yazılmamış görevler (40–51): e-posta, çoklu dil, Compare, profil fotoğrafı, yüzde alarmı, e-posta doğrulama vb. |

> **Arşiv kararı:** Dokümante edilmemiş eski görevler `TASKS_HAFTA9.md`'nin başına değil,
> ayrı bir `TASKS_ARSIV.md` dosyasına yazıldı. Böylece HAFTA9 sadece o haftanın işlerini
> anlatıyor; geçmiş ise tek yerde, tablo halinde bulunuyor.
