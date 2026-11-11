# CryptoTracker — Staj Projesi

[![Backend CI](https://github.com/sema1501/Matriks_Intern2026/actions/workflows/backend-ci.yml/badge.svg)](https://github.com/sema1501/Matriks_Intern2026/actions/workflows/backend-ci.yml)
[![Frontend CI](https://github.com/sema1501/Matriks_Intern2026/actions/workflows/frontend-ci.yml/badge.svg)](https://github.com/sema1501/Matriks_Intern2026/actions/workflows/frontend-ci.yml)
[![Docker CI](https://github.com/sema1501/Matriks_Intern2026/actions/workflows/docker-ci.yml/badge.svg)](https://github.com/sema1501/Matriks_Intern2026/actions/workflows/docker-ci.yml)

Canlı kripto para takibi, teknik analiz ve sanal (Binance Testnet üzerinden) alım-satım
botu içeren full-stack bir uygulama.

**Stack:** ASP.NET Core 9 · React 18 · MSSQL · Docker · JWT · Binance API (public + Testnet) · klinecharts · Recharts

---

## Docker ile çalıştır

Tek gereken [Docker Desktop](https://www.docker.com/products/docker-desktop). Depo kökünde:

```bash
cp .env.example .env
docker compose up --build
```

İlk derleme birkaç dakika sürer. Hazır olunca:

| Adres | Ne var |
|---|---|
| http://localhost:3000 | Web arayüzü. `/api` ve `/hubs` istekleri nginx üzerinden API'ye gider |
| http://localhost:5002/health | API sağlık ucu, yalnızca bu bilgisayardan erişilir |
| `localhost,14330` | SQL Server, yalnızca veritabanına bakmak isterseniz |

İlk giriş için aşağıdaki [test kullanıcısı](#test-kullanıcısı-otomatik-oluşturulur) otomatik oluşur (`admin` / `Admin123!`).
Yeni kayıt açarsanız ve `.env`'de `SMTP_HOST` boşsa e-posta doğrulama bağlantısı gönderilmez, API loguna yazılır:
`docker compose logs api | grep confirm-email`.

```bash
docker compose logs -f api    # bir servisin logları (db, api, web)
docker compose down           # durdurur, veriler kalır
docker compose down -v        # durdurur ve veritabanını siler, her şey sıfırlanır
```

Daha önce eski `backend/docker-compose.yml` ile kurduysanız 14330 portu dolu olur, önce eski konteyneri durdurun
(`docker stop cryptotracker-db`). Yeni kurulum kendi boş veritabanıyla başlar, eski veriler eski volume'da kalır.

Şifre ve anahtarlar `.env` dosyasından okunur, bu dosya git'e girmez. `.env.example`'daki, `appsettings.json`'daki
ve seed kullanıcısındaki değerler yalnızca yerel geliştirme içindir, **üretimde mutlaka değiştirin**.

`REACT_APP_API_URL` çalışma anında değil `npm run build` sırasında koda gömülür, tarayıcıda çalışan
koda ortam değişkeni ulaşamaz; bu yüzden Dockerfile'da `ARG` ile alınır, değiştirince imaj yeniden derlenmelidir.

Swagger bu kurulumda kapalıdır çünkü API `Production` ortamında çalışır; Swagger için aşağıdaki `dotnet run` kurulumunu kullanın.
Rate limiting nginx arkasında da IP başına çalışsın diye compose `ForwardedHeaders__Enabled=true` verir. API'yi
doğrudan internete açarsanız bu ayarı kapatın, yoksa sahte `X-Forwarded-For` başlığıyla limit aşılabilir.

---

## Kurulum (Docker olmadan geliştirme)

### Gereksinimler
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 24+](https://nodejs.org/) (npm 11; `package-lock.json` bu sürümle oluşturuldu)
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
Depo kökünde (`.env` yoksa önce `cp .env.example .env`):
```bash
docker compose up -d db
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
- Docker Desktop kapalıysa `docker compose up -d db` çalışmaz.
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

### 6. Testleri çalıştır

**Backend** (xUnit, InMemory veritabanı kullanır, SQL Server gerekmez):
```bash
cd backend
dotnet test
```

**Frontend** (Jest + React Testing Library, `react-scripts` ile gelir):
```bash
cd frontend
npm test                                # izleme modunda, dosya değiştikçe tekrar çalışır
CI=true npm test -- --watchAll=false    # tek sefer çalışır, CI'daki komut bu
```
Windows PowerShell'de tek sefer çalıştırmak için: `$env:CI="true"; npm test -- --watchAll=false`

Frontend testleri ağ çağrılarını mock'lar, backend'in açık olması gerekmez. Test dosyaları
test ettikleri dosyanın yanında `*.test.js` / `*.test.jsx` adıyla durur:
- `services/apiService.test.js`: 401 gelince refresh, aynı anda gelen 401'lerde tek refresh, refresh başarısız olursa çıkış
- `utils/botSignalReason.test.jsx`: Strateji / Stop-loss / Take-profit etiketleri (TR/EN)
- `components/PortfolioHistoryChart/PortfolioHistoryChart.test.jsx`: yükleniyor, hata, boş, tek kayıt ve grafik durumları
- `context/RealtimeContext.test.jsx`: bağlantı kopunca polling'e geçiş, geri gelince polling'in durması, çıkışta bağlantının kapanması
- `components/AlertMonitor/AlertMonitor.test.jsx`: aynı sinyal SignalR ve polling'den gelince tek bildirim

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

## Sürekli Entegrasyon (CI)

Her `push` ve `pull_request` (main, develop) için GitHub Actions üç workflow çalıştırır:

| Workflow | Dosya | Ne doğrular |
|---|---|---|
| Backend CI | `.github/workflows/backend-ci.yml` | `dotnet restore`, `dotnet build --configuration Release`, `dotnet test` (`backend/CryptoTracker.sln`) |
| Frontend CI | `.github/workflows/frontend-ci.yml` | `npm ci`, `npm test` (Jest), `npm run build` (`frontend/`, Node 24) |
| Docker CI | `.github/workflows/docker-ci.yml` | `docker compose config`, iki Dockerfile için `docker build` (konteyner çalıştırmaz) |

Testler `Testing` ortamında InMemory veritabanı kullandığı için CI'da SQL Server gerekmez. Frontend testlerinden biri kırmızıysa build adımına geçilmez. Workflow kırmızıysa PR birleştirilmeden önce düzeltilmelidir.

Depo kökündeki `.gitattributes` metin dosyalarını depoda LF satır sonuyla tutar; Windows'ta da
satır sonu farkı yüzünden dosyalar "değişmiş" görünmez.

---

## Özellikler

**Kimlik & Kullanıcı**
Kayıt/giriş (JWT), **e-posta doğrulama zorunluluğu** (doğrulanmamış hesap giriş
yapamaz), şifremi unuttum/sıfırlama (bağlantı e-postayla gönderilir), profil
görüntüleme/düzenleme, **profil fotoğrafı** (en fazla ~3 MB), şifre değiştirme,
e-posta bildirimi açma/kapama, rol yönetimi (Admin/SuperAdmin/User).
**Oturum yenileme:** 15 dakikalık access token + 7 günlük refresh token. Access token
dolunca frontend oturumu fark ettirmeden yeniler (her yenilemede yeni refresh token
verilir, eskisi iptal edilir). Kullanılmış bir refresh token tekrar gelirse kullanıcının
tüm oturumları kapatılır. Tek istisna: iki sekme aynı anda yenileme yaparsa, yeni
dönüştürülmüş token ilk 10 saniye içinde tekrar geldiğinde yalnızca 401 döner, oturumlar
kapanmaz; frontend bu durumda diğer sekmenin aldığı yeni token'ı kullanır
(`Jwt:RefreshReuseGraceSeconds`). Çıkış, şifre değiştirme ve şifre sıfırlama refresh token'ları
iptal eder. Refresh token veritabanında yalnızca SHA-256 hash'i olarak saklanır.
`RefreshTokenCleanupService` günde bir süresi dolmuş token'ları ve 30 günden eski iptal
edilmiş token'ları siler (`RefreshTokenCleanup` ayarları).

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
Aynı alarm/bot için saatte en fazla 1 e-posta (cooldown); stop-loss / take-profit satışı
önemli olduğu için cooldown'a takılmaz ve e-postada satış nedeni yazar. SMTP ayarı yoksa e-posta içeriği
loglanır. Profil sayfasından kapatılabilir.

**Anlık Bildirimler (SignalR)**
Uygulama açıkken alarm tetiklenmesi ve bot sinyalleri sayfa yenilemeden, polling aralığını
beklemeden ekrana gelir. Navbar'daki gösterge bildirim bağlantısının durumunu gösterir
(canlı / yeniden bağlanıyor / çevrimdışı). Bağlantı yoksa eski polling otomatik devreye girer.
Ayrıntılar aşağıda "Anlık Bildirim Mimarisi" bölümünde.

**Sanal Portföy**
Her kullanıcı 10.000 USD sanal bakiye ile başlar, coin detay sayfasından alım/satım
yapabilir, işlem geçmişini (sayfalı, sembol/tür filtreli) ve kâr/zararını görebilir,
portföy dağılımını pasta grafikte izleyebilir. Kullanıcılar arası liderlik tablosu.
İşlem geçmişi o anki filtrelerle **CSV** ya da **Excel (.xlsx)** olarak indirilebilir; dosyadaki
başlıklar ve Alış/Satış yazıları arayüzde seçili dilde (TR/EN) gelir.

**Portföy Değer Geçmişi**
Arka plan servisi (`PortfolioSnapshotService`) saatte bir her kullanıcının portföy değerini
(nakit + coinlerin o anki Binance fiyatıyla değeri) kaydeder. Fiyatlar her turda tek seferde
çekilir, aynı saat içinde ikinci kayıt atılmaz. Son 30 gün saatlik, 30 gün–1 yıl arası günlük
(her günün son kaydı) tutulur, 1 yıldan eski kayıtlar silinir (`PortfolioSnapshot` ayarları).
Portföy sayfasının üstünde 24s / 7g / 30g / Tümü seçilebilen çizgi grafik (`recharts`) ve seçilen aralıktaki
değişim (USD ve %) gösterilir.

**Alım-Satım Botu**
İki strateji seçeneği olan, arka planda (`BotMonitorService`) periyodik kontrol yapan bir
bot: **RSI eşiği** (RSI alım/satım bölgesine girince) veya **EMA kesişimi** (kısa EMA uzun
EMA'yı yukarı keserse AL, aşağı keserse SAT). Sinyal oluşunca **Binance Testnet**'e gerçek (sahte parayla) emir gönderir; sonuç
kullanıcının sanal portföy defterine işlenir. Seçilen tarih aralığında botun geçmişte nasıl
sonuç verdiğini gösteren **backtest** raporu (grafik üzerinde alış/satış okları + RSI paneli
+ sinyal tablosu) — her iki strateji için de çalışır.
İsteğe bağlı **stop-loss / take-profit**: bot kurulurken yüzde olarak girilir (örn. %5 / %10).
Açık pozisyonun fiyatı alış fiyatına göre bu kadar düşerse ya da yükselirse bot, strateji
sinyalini beklemeden otomatik satar. Bu satışlar sinyal geçmişinde ve backtest tablosunda
"Stop-loss" / "Take-profit" rozetiyle, sayfadaki bildirimde ve e-postada nedeniyle gösterilir.
Backtest de aynı kuralı uygular. Bu satış başarısız olursa (örn. Testnet hatası) bot sonraki
turlarda en fazla 5 kez, denemeler arasında 2 dakika bekleyerek tekrar dener; beklerken
strateji sinyali çalışmaz. 5. deneme de başarısız olursa kullanıcıya bir kez TR/EN e-posta
gider (`TradingBot:RiskExitMaxAttempts`, `TradingBot:RiskExitRetryDelayMinutes`).

**Admin Paneli**
Tüm kullanıcıların bot ve portföy aktivitesini görüntüleme, şüpheli/aşırı işlem yapan
botları tespit etme ve durdurma (kill switch), denetim günlüğü (audit log), geri bildirim
listesi, haftalık yeni kullanıcı istatistiği.

**Diğer**
Kullanıcı geri bildirim formu.

---

## Anlık Bildirim Mimarisi

```
Alarm tetiklendi → AlertMonitoringProcessor ─┐
Bot sinyali      → BotAutoTradeExecutor ─────┴→ IRealtimeNotifier → NotificationHub → RealtimeContext
                                                                      (Clients.User)    (tarayıcı)
RealtimeContext → AlertMonitor (toast), BotSignalApproval (sinyal listesi), Profile (alarm listesi)
```

- **Hub:** `NotificationHub` (`[Authorize]`), `/hubs/notifications` adresinde. Tarayıcı yalnızca dinler.
- **Kime gider:** `Clients.User(userId)` ile yalnızca olayın sahibine. Kullanıcı kimliği token'daki
  `NameIdentifier` claim'inden okunur; başka kullanıcının bağlantısına gitmez (testle doğrulandı).
- **Token:** WebSocket bağlantısında header gönderilemediği için token `access_token` sorgu parametresiyle
  gelir. Bu yalnızca `/hubs` yollarında kabul edilir, normal API'de sorgu parametresindeki token yok sayılır.
- **CORS:** SignalR kimlik bilgisiyle bağlandığı için `AllowCredentials` açık, origin'ler
  `Cors:AllowedOrigins` ayarından okunur (varsayılan `http://localhost:3000`).
- **Ek kanal:** E-posta ve veritabanı kayıtları eskisi gibi devam eder. Anlık gönderim başarısız olursa
  sadece loglanır, izleme servisleri durmaz.

**Olaylar**

| Olay | Ne zaman | İçerik |
|---|---|---|
| `alertTriggered` | Fiyat alarmı tetiklendi | `id` (AlertSignal), `alertId`, `symbol`, `priceAtTrigger`, `triggeredAt` |
| `botSignal` | Bot işlem yaptı ya da işlem başarısız oldu (strateji, stop-loss, take-profit) | `BotSignalResponse` ile aynı: `id`, `botId`, `signalType`, `status`, `reason`, ... |
| `botSignalPending` | Bot sinyali onay bekliyor | `botSignal` ile aynı. Botlar artık otomatik işlem yaptığı için şu an üretilmiyor, eski onay akışı için duruyor |

**Frontend**

- `RealtimeContext` giriş yapılınca bağlantıyı açar, çıkış yapılınca kapatır. Bileşenler
  `useRealtimeEvent` ile olaylara abone olur.
- `withAutomaticReconnect()` kullanılır. `accessTokenFactory` her bağlanışta ve yeniden bağlanışta
  `getFreshAccessToken`'ı çağırır: token 30 saniye içinde dolacaksa önce refresh yapılır, süresi dolmuş
  token ile bağlanılmaz.
- Otomatik denemeler biterse (backend uzun süre kapalı) durum "çevrimdışı" olur ve bağlantı 15 saniyede bir
  yeniden denenir.
- **Yedek polling:** `usePollingFallback` bağlantı yokken eski aralıklarla (alarm 45 sn, bot 15 sn) istek atar,
  bağlantı gelince durur ve arada kaçan olaylar için bir kez daha çeker.
- **Tekilleştirme:** Aynı sinyal hem SignalR hem polling ile gelirse sinyal `id`'sine göre bir kez gösterilir.

**Docker / proxy:** `dotnet run` ile backend doğrudan çalıştığında ek ayar gerekmez. `docker compose` ile
istekler nginx'ten geçer; `frontend/nginx.conf` `/hubs/` yolunu `Upgrade` ve `Connection` başlıklarıyla
vekil eder, bunlar olmazsa bağlantı LongPolling'e düşer. Tarayıcıda Network → WS sekmesinden görülebilir.

Frontend başka bir adresten açılıyorsa o adres `Cors:AllowedOrigins` listesine eklenmeli.

---

## API Uçları (özet)

Tüm uçların ayrıntılı şeması için backend çalışırken `/swagger` adresine bakın. Kısa özet:

| Controller | Ne işe yarar |
|---|---|
| `AuthController` | Kayıt (`register`), e-posta doğrulama (`confirm-email`), giriş (`login`), oturum yenileme (`refresh`), çıkış (`logout`), şifre sıfırlama (`forgot-password`, `reset-password`), profil (`GET/PUT me`), şifre değiştirme (`me/password`), e-posta bildirim tercihi (`me/notifications`), profil fotoğrafı (`me/avatar`), kullanıcı listesi (`users`) |
| `RoleController` | Rol listeleme/oluşturma/atama (Admin) |
| `WatchlistController` | Favori coin ekleme/çıkarma |
| `AlertController` | Fiyat / yüzde-değişim alarmı oluşturma, listeleme, silme, açma-kapama (`toggle`), tetiklenme geçmişi (`signals`) |
| `PortfolioController` | Bakiye, holdings, işlem geçmişi (sayfalı + filtreli), alım/satım, liderlik tablosu |
| `BotController` | Bot oluşturma (RSI / EMA, isteğe bağlı `stopLossPercent` / `takeProfitPercent`), aç-kapa, silme, sinyaller (`reason`: Strategy / StopLoss / TakeProfit), performans, backtest |
| `FeedbackController` | Geri bildirim gönderme/listeleme (Admin) |
| `TestnetController` | Binance Testnet hesap durumu (Admin, debug amaçlı) |
| `AdminController` | Tüm bot/portföy gözetimi (`bots`, `portfolios`), kill switch (`bots/{id}/kill`), işaretleme (`Bot/{id}/flag`), aşırı işlem listesi (`overtrading`), audit log (`audit-log`) |
| `DashboardController` | Günlük/haftalık yeni kullanıcı istatistiği (`daily-new-users`, Admin) |

---

## Proje Yapısı

```
CryptoTracker/
├── docker-compose.yml        ← db + api + web, tek komutla çalıştırır
├── .env.example              ← .env olarak kopyalanır (şifreler ve anahtarlar)
├── backend/
│   ├── Dockerfile
│   ├── src/CryptoTracker.API/
│   │   ├── Controllers/     ← API endpoint'leri (yukarıdaki tablo)
│   │   ├── Models/          ← Veritabanı modelleri
│   │   ├── DTOs/             ← Request / Response nesneleri
│   │   ├── Services/        ← İş mantığı (bot, alarm, backtest, testnet, vb.)
│   │   ├── Data/             ← DbContext + DataSeeder
│   │   ├── Migrations/      ← EF Core migration geçmişi
│   │   ├── Hubs/            ← SignalR NotificationHub
│   │   └── Middleware/      ← Hata yakalama
│   └── tests/CryptoTracker.API.Tests/  ← Servis ve entegrasyon testleri
└── frontend/
    ├── Dockerfile            ← derleme + nginx
    ├── nginx.conf            ← React Router, /api ve /hubs vekili
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
        └── context/          ← Auth, Price, Theme, Currency, Watchlist, Language, Realtime
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
| [TASKS_HAFTA10.md](./TASKS_HAFTA10.md) | CI/CD (GitHub Actions), kimlik doğrulama uçlarına rate limiting, işlem geçmişini CSV dışa aktarma |
| [TASKS_HAFTA11.md](./TASKS_HAFTA11.md) | Refresh token ve oturum süresi yönetimi, bot stop-loss/take-profit, portföy değer geçmişi grafiği |
| [TASKS_ARSIV.md](./TASKS_ARSIV.md) | Kodda olup hiçbir haftalık dosyaya yazılmamış görevler (40–51): e-posta, çoklu dil, Compare, profil fotoğrafı, yüzde alarmı, e-posta doğrulama vb. |

> **Arşiv kararı:** Dokümante edilmemiş eski görevler `TASKS_HAFTA9.md`'nin başına değil,
> ayrı bir `TASKS_ARSIV.md` dosyasına yazıldı. Böylece HAFTA9 sadece o haftanın işlerini
> anlatıyor; geçmiş ise tek yerde, tablo halinde bulunuyor.
