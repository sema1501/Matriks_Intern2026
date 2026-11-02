# 13. Hafta Görevleri — Docker ile Tek Komutla Çalıştırma, Backtest Risk Metrikleri, Refresh Token'ı HttpOnly Cookie'ye Taşıma

**Bağlam:** `TASKS_HAFTA12.md`'deki üç görev tamamlandı ve kabul kriterlerini karşılıyor:
- #100 — Refresh token temizliği (`RefreshTokenCleanupService`), iki sekme için 10 saniyelik tolerans
  penceresi, başarısız stop-loss / take-profit satışının yeniden denenmesi ve 5. denemede tek e-posta.
- #101 — Frontend test altyapısı (Jest + React Testing Library), `apiService` interceptor testleri,
  `.gitattributes`, CI'da `npm test` adımı. (PR #103 ile `main`'e girdi.)
- #102 — SignalR ile anlık bildirimler (`NotificationHub`, `RealtimeContext`, polling yedeği).
  PR #104 ile `main`'e birleşti; Docker görevindeki WebSocket kısmı artık başlayabilir.

Bu hafta üç konu: projeyi **tek komutla ayağa kaldırılabilir** hale getirmek, **backtest'i** gerçek bir
strateji karşılaştırma aracına çevirmek ve son hafta kurulan oturum yapısının **en zayıf halkasını**
(refresh token'ın `localStorage`'da durması) kapatmak.

| # | Görev | Zorluk |
|---|-------|--------|
| 1 | Tam yığın Docker: `docker compose up` ile veritabanı + API + frontend | Normal |
| 2 | Backtest raporuna risk metrikleri ve değer eğrisi grafiği | Normal |
| 3 | Refresh token'ı HttpOnly cookie'ye taşı | **Zor** |

> **Numaralandırma ve süreç:** Her görev için önce bir **GitHub Issue** aç; branch `feature/<issue-no>-kisa-ad`,
> commit ve PR başlıkları `#<issue-no> ...`. Kod içine `// Görev NN` yazma.
> **Her görev ayrı branch ve ayrı PR olacak.** Geçen hafta #100 ve #101 tek PR'da (#103) geldi; bu sefer
> her biri kendi PR'ında, kendi CI sonucuyla gelsin ki sorun çıkarsa tek görev geri alınabilsin.

---

## 1. Tam Yığın Docker: Tek Komutla Çalıştırma

**Bu görev ne işe yarıyor?**

Bugün projeyi çalıştırmak için bir şey kurmak gerekiyor: `backend/docker-compose.yml` yalnızca SQL Server'ı
başlatıyor; API'yi `dotnet run` ile, frontend'i `npm start` ile ayrı ayrı çalıştırmak lazım. Yeni biri projeyi
denemek ya da Sema bir demo göstermek istediğinde .NET SDK, Node ve doğru portlar şart. Hedef: depo kökünde
`docker compose up --build` yazınca üç parçanın da (veritabanı, API, web arayüzü) çalışması.

*Basit benzetme:* Şu an yemeği yapmak için malzemeleri tek tek bulup sırayla pişirmek gerekiyor. Bu görev
bitince "hazır yemek kutusu" oluyor: kutuyu aç, ısıt, hazır.

**Önce oku:** `backend/Dockerfile`, `backend/docker-compose.yml`, `Program.cs` (bağlantı dizesi, CORS
`Cors:AllowedOrigins`, migration ve seed davranışı), `frontend/src/services/apiService.js`
(`REACT_APP_API_URL`), README'deki "çalıştırma" bölümü.

**Yapılacaklar**
- `frontend/Dockerfile`: çok aşamalı (multi-stage) — birinci aşamada `npm ci && npm run build`, ikinci
  aşamada çıkan statik dosyaları **nginx** ile sunsun. `frontend/nginx.conf`: React Router için bilinmeyen
  yolları `index.html`'e yönlendirsin (yoksa `/portfolio` sayfasını yenileyince 404 alınır).
- `REACT_APP_API_URL` **derleme anında** gömülür (çalışırken değiştirilemez). Bunu Dockerfile'da `ARG` ile
  al ve compose'ta `build.args` ile ver; neden böyle olduğunu README'ye bir cümleyle yaz.
- Depo köküne `docker-compose.yml` (ya da `backend/` içindekini genişlet; hangisini seçtiğini PR'da
  gerekçelendir): `db`, `api`, `web` servisleri. `api`, `db` hazır olmadan başlamasın:
  `db` için `healthcheck`, `api` için `depends_on: condition: service_healthy`.
- **Sırlar kodda durmasın:** SQL şifresi, JWT anahtarı, e-posta ayarları `.env` dosyasından okunsun.
  Depoya `.env.example` ekle (gerçek `.env` `.gitignore`'da olsun; zaten değilse ekle).
  Şu an compose dosyasında şifre düz yazı olarak duruyor — bunu kaldır.
- `api` için bir **sağlık ucu** (`/health`, ASP.NET Core `AddHealthChecks`) ve compose'ta `healthcheck`.
- (#102 birleştikten sonra) nginx, `/hubs/` yolunu API'ye **WebSocket yükseltmesiyle** (`Upgrade` ve
  `Connection` başlıkları) vekil etsin; yoksa SignalR bağlantısı Docker'da WebSocket yerine yavaş
  yedek yönteme düşer. Bunu doğrulamanın yolu: tarayıcıda Network → WS sekmesi.
- Veritabanı verisi bir **volume**'da kalsın (`docker compose down` veriyi silmesin, `down -v` silsin).
- README'ye "Docker ile çalıştır" bölümü: tek komut, hangi adreslerin açıldığı, ilk giriş için seed
  kullanıcısı varsa nasıl bulunacağı, logları görme, her şeyi sıfırlama.
- CI: `.github/workflows/` altına, PR'larda iki Dockerfile'ın da **derlendiğini** kontrol eden bir adım
  ekle (`docker build`; çalıştırmak şart değil).

**Kabul kriterleri**
- [ ] Temiz bir klasörde (`.env.example` → `.env` kopyalayıp) `docker compose up --build` ile üç servis de ayağa kalkıyor
- [ ] `http://localhost:3000` açılıyor, kayıt/giriş çalışıyor, `/portfolio` sayfası yenilenince 404 vermiyor
- [ ] API sağlık ucu 200 dönüyor; `db` hazır olmadan `api` başlamıyor
- [ ] Depoda düz yazı şifre / JWT anahtarı kalmadı (compose ve appsettings'teki geliştirme varsayılanları dışında; bunlar README'de "üretimde değiştir" diye uyarılı)
- [ ] `docker compose down` + `up` sonrası kullanıcılar ve portföy verisi duruyor
- [ ] (#102 sonrası) Tarayıcıda SignalR bağlantısı WebSocket olarak görünüyor
- [ ] CI'daki Docker derleme adımı yeşil

---

## 2. Backtest Raporuna Risk Metrikleri ve Değer Eğrisi Grafiği

**Bu görev ne işe yarıyor?**

Backtest, "bu stratejiyi geçmişte çalıştırsaydım ne olurdu?" sorusunu cevaplıyor — RSI, EMA ve artık
stop-loss / take-profit ile. Ama rapor bugün büyük ölçüde "toplam kâr / zarar" gösteriyor. Tek başına
toplam kâr yanıltıcıdır: %40 kazandıran ama arada %35 değer kaybettiren bir strateji, %25 kazandıran ama
hiç %5'ten fazla düşmeyen stratejiden çoğu kullanıcı için daha kötüdür. Stop-loss'un işe yarayıp yaramadığını
görmek için de bu ek ölçülere ihtiyaç var (stop-loss'lu ve stop-loss'suz iki koşuyu karşılaştırabilmek).

**Önce oku:** `BacktestService.cs` (`BuildResult` ve candle başına risk çıkışı mantığı), `BacktestDtos.cs`,
`BacktestReport.jsx`, `BacktestServiceTests.cs`, `EmaBacktestTests.cs`, `RiskExitTests.cs`. Raporda **zaten
hesaplanan** değerleri yeniden hesaplama; eksik olanları ekle.

**Yapılacaklar — backend**
- Saf (veritabanına ve Binance'e bağımlı olmayan) bir `BacktestMetrics` sınıfı yaz; girdisi işlem listesi ve
  candle başına portföy değeri serisi, çıktısı:
  - **Değer eğrisi (equity curve):** her candle sonundaki toplam değer (nakit + eldeki coin) — grafiğin verisi.
  - **Maksimum düşüş (max drawdown):** eğrinin gördüğü en yüksek noktadan, sonraki en düşük noktaya en büyük
    yüzde kayıp.
  - **Kazanma oranı:** kârla kapanan işlemlerin, kapanan tüm işlemlere oranı. (Hiç kapanan işlem yoksa `null`,
    sıfıra bölme olmasın.)
  - **Kâr faktörü (profit factor):** toplam kâr ÷ toplam zarar. Hiç zarar yoksa `null`.
  - **Ortalama işlem getirisi** ve **işlem sayısı**; stop-loss / take-profit ile kapanan işlem sayıları ayrı ayrı.
  - **"Al ve tut" karşılaştırması:** aynı aralıkta ilk candle'da al, sonda sat deseydin getiri kaç olurdu. Strateji
    bunu geçemiyorsa kullanıcı bunu görmeli.
- `BacktestResult` DTO'suna bu alanları ekle. Mevcut alanları **silme / yeniden adlandırma** (frontend onlara
  bağlı).
- Eğri çok uzun olabilir (yüzlerce candle). API'den en fazla ~300 noktaya indirgenmiş olarak dön
  (örn. eşit aralıkla örnekle, ama **ilk ve son nokta ile en düşük nokta** mutlaka dahil kalsın).

**Yapılacaklar — frontend**
- `BacktestReport`'a: metrik kartları (Kazanma oranı, Maks. düşüş, Kâr faktörü, Al-ve-tut farkı) ve
  `recharts` ile değer eğrisi (`PortfolioHistoryChart`'ın renk / biçim anlayışını kullan; yeni kütüphane **yok**).
  Maks. düşüş değeri kırmızı tonda, "al ve tut"tan iyiyse yeşil bir rozet.
- `null` gelen metrikler "—" olarak gösterilsin, `NaN` / `Infinity` asla ekrana çıkmasın.
- TR/EN metinler `LanguageContext`'te. Her metriğin yanında kısa bir açıklama (ⓘ ile açılan ya da fare üstü):
  "Maks. düşüş nedir?" sorusuna bir cümlelik cevap.

**Test (zorunlu)**
- `BacktestMetrics` için elle hesaplanmış küçük örnekler: sürekli yükselen seri (düşüş 0), düşüp toparlanan seri
  (düşüş değeri elle doğrulanmış), hiç işlem yok, hiç zarar yok (kâr faktörü `null`), tek işlem.
- Mevcut `BacktestServiceTests` hâlâ yeşil; yeni alanların da dolu geldiğini doğrulayan bir test.
- Frontend: `BacktestReport` `null` metriklerle çökmeden "—" gösteriyor.

**Kabul kriterleri**
- [ ] Bir backtest çalıştırınca raporda değer eğrisi ve en az 4 yeni metrik görünüyor
- [ ] Max drawdown elle hesaplanmış örnekle birebir uyuşuyor (testle kanıtlı)
- [ ] Stop-loss'lu ve stop-loss'suz iki koşunun metrikleri farklı çıkıyor ve ekranda karşılaştırılabiliyor
- [ ] Eski API alanları aynen duruyor; mevcut testler yeşil
- [ ] `null` / sıfıra bölme durumlarında ekranda `NaN` yok
- [ ] README'nin backtest bölümüne metriklerin ne anlama geldiği eklendi

---

## 3. (ZOR) Refresh Token'ı HttpOnly Cookie'ye Taşı

**Bu görev ne işe yarıyor?**

Geçen haftalarda kurduğumuz oturum yapısında **refresh token tarayıcının `localStorage`'ında** duruyor. Sayfadaki
herhangi bir JavaScript onu okuyabilir. Projeye bir gün XSS açığı (kullanıcı verisinin ya da bir
bağımlılığın sayfaya kötü script sokması) girerse saldırgan refresh token'ı çalar ve **7 gün boyunca**
kullanıcının oturumunu açık tutar; access token'ın 15 dakikada dolması bu durumda işe yaramaz.
**HttpOnly cookie**, tarayıcının JavaScript'ten gizlediği, yalnızca HTTP istekleriyle otomatik giden bir
çerezdir: script onu okuyamaz, dolayısıyla çalamaz.

Bedeli: cookie tarayıcı tarafından **otomatik** gönderilir, bu da başka bir saldırıya (CSRF — kullanıcı
başka bir sitedeyken o siteden tetiklenen istek) kapı açar. Yani bu görev "tokenı gizle" değil, **iki
saldırı arasında doğru dengeyi kurmak**. Ayrıca #100'de yazılan "iki sekme aynı anda yenileme" mantığı
`localStorage`'daki refresh token'ı karşılaştırarak çalışıyor; o mantık yeniden tasarlanmalı.

**Önce oku:** `AuthController.cs` (`login`, `refresh`, `logout`), `AuthDtos.cs`, `AuthService.RefreshAsync`
(ve tolerans penceresi), `Program.cs` (CORS — `AllowCredentials` zaten SignalR için açık), `apiService.js`
(`saveSession`, `refreshAccessToken`, `waitForOtherTabToken`, `getFreshAccessToken`), `AuthContext.js`,
`apiService.test.js`, `RefreshTokenTests.cs`.

**Yapılacaklar — backend**
- `login` (ve kayıt sonrası otomatik giriş varsa o) yanıtında refresh token'ı **gövdede dönme**,
  `Set-Cookie` ile ver: `HttpOnly`, `SameSite=Lax` (ya da `Strict`; seçimini gerekçelendir),
  `Path=/api/Auth` (cookie yalnızca oturum uçlarına gitsin, her API isteğine değil), ömrü
  `Jwt:RefreshTokenDays`. `Secure` bayrağı yapılandırmadan gelsin: geliştirmede (`http://localhost`) kapalı,
  üretimde (HTTPS) **açık** — varsayılanı güvenli taraf olsun.
- `refresh` uç noktası token'ı **cookie'den** okusun; gövdeden okumasın. Başarılı yanıtta yeni cookie
  yazılsın (rotasyon aynen sürüyor). Cookie yoksa 401.
- `logout` cookie'yi silsin (aynı `Path`/alan ile süresi geçmiş cookie) ve token'ı iptal etsin.
- **CSRF koruması:** `refresh` ve `logout` yalnızca cookie ile çalıştığı için, istek kaynağını doğrula:
  `Origin` başlığı varsa `Cors:AllowedOrigins` listesinde olmalı, değilse **403**. Ek olarak özel bir başlık
  (örn. `X-Requested-With: XMLHttpRequest`) zorunlu olsun — tarayıcılar bu başlığı başka bir siteden
  CORS izni olmadan gönderemez. Bu iki önlemin **neden birlikte** istendiğini kodda kısa yorumla açıkla.
- Tolerans penceresi (#100) ve yeniden kullanım tespiti aynen çalışmaya devam etmeli.
- Eski istemciler: güncelleme sonrası `localStorage`'da kalan eski `refreshToken` değeri işe yaramayacak;
  kullanıcılar **bir kez yeniden giriş yapacak** (bu kabul edilebilir — PR açıklamasına yaz). Frontend, eski
  değeri açılışta `localStorage`'dan silsin.

**Yapılacaklar — frontend**
- `axios` örneği `withCredentials: true` ile cookie gönder/al; `refresh` ve `logout` çağrıları özel
  başlığı göndersin; refresh token'ı **hiçbir yerde** (state, `localStorage`, log) tutma.
- `saveSession` / `endSession` / `AuthContext` içinden refresh token alanını kaldır.
- **Çoklu sekme mantığını yeniden yaz (zor kısım).** Cookie tarayıcıda **tüm sekmelerce paylaşıldığı** için
  iki sekme aynı anda yenileme isterse sunucu ikincisini tolerans penceresinde reddedebilir (`refresh_token_rotated`).
  `waitForOtherTabToken` artık "refresh token değişti mi" diye bakamaz (token'ı görmüyoruz). İki seçenekten
  birini seç ve gerekçelendir: (a) **Web Locks API** (`navigator.locks.request`) ile sekmeler arası tek
  yenileme kilidi; kilidi alan sekme yeniler, diğeri kilit açılınca güncel access token'ı `localStorage`'dan
  okur; (b) `storage` olayı / `BroadcastChannel` ile "yeni access token yazıldı" haberi beklemek.
- **SignalR** (`getFreshAccessToken`) aynı kilidi / mantığı kullanmalı; yoksa bağlantı yeniden kurulurken
  ikinci bir eşzamanlı yenileme çıkar.
- Sayfa ilk açıldığında `localStorage`'da geçerli access token yoksa ama cookie varsa, kullanıcı
  **sessizce giriş sayfasına atılmak yerine** bir refresh denemesi yapılsın (access token kaybolmuş olabilir).
- *(İsteğe bağlı, ek puan)* Access token'ı da `localStorage`'dan çıkarıp yalnızca bellekte tut; sayfa
  yenilenince cookie ile sessiz refresh yap. Bunu yapıyorsan SignalR ve çoklu sekme etkilerini PR'da anlat.

**Test (zorunlu)**
- Backend entegrasyon testleri (`WebApplicationFactory`, `HttpClient` çerezleri `CookieContainer` ile):
  - `login` yanıtında `Set-Cookie` var; `HttpOnly` ve `Path=/api/Auth` bayrakları doğru; **JSON gövdesinde
    `refreshToken` yok**.
  - Cookie ile `refresh` çalışıyor ve **yeni** cookie veriyor; cookie olmadan 401; gövdede token göndermek
    işe yaramıyor.
  - Geçersiz `Origin` ile `refresh` → 403; özel başlık yoksa → 403.
  - `logout` sonrası cookie siliniyor ve eski token ile `refresh` 401.
  - Tolerans penceresi ve yeniden kullanım tespiti testleri (`RefreshTokenTests`) güncellenmiş hâliyle yeşil.
- Frontend: `apiService.test.js` yeni sözleşmeye göre güncellendi — 401 → tek refresh; eşzamanlı 3 istek →
  1 refresh; `refresh_token_rotated` cevabında diğer sekmenin token'ını kullanma; refresh başarısızsa çıkış.
- Elle doğrulama (PR açıklamasına ekran görüntüsüyle): tarayıcı Geliştirici Araçları → Application →
  Cookies'te cookie'nin **HttpOnly** işaretli olduğu; konsola `document.cookie` yazınca refresh token'ın
  **görünmediği**; `localStorage`'da refresh token olmadığı.

**Kabul kriterleri**
- [ ] Refresh token yalnızca HttpOnly cookie'de; `localStorage`, bellek, log ve JSON yanıtlarında yok
- [ ] `document.cookie` ve `localStorage` içinde refresh token görünmüyor (ekran görüntüsü PR'da)
- [ ] `refresh` / `logout` yalnızca izinli `Origin` + özel başlıkla çalışıyor; aksi halde 403 (otomatik testle kanıtlı)
- [ ] İki sekmeyi aynı anda yenilemeye zorlayınca (access token'ı elle sildirip iki sekmede aynı anda sayfa yenile) kullanıcı **atılmıyor**
- [ ] SignalR bağlantısı, access token dolduktan sonra da yeni token ile yeniden bağlanıyor (#102 davranışı bozulmadı)
- [ ] Çıkış yapınca cookie siliniyor; çıkış yapmış kullanıcının eski token'ı çalışmıyor
- [ ] `Secure` bayrağı yapılandırmadan geliyor, üretim için varsayılan açık; README'de nedeni yazılı
- [ ] Mevcut ve yeni tüm testler (backend + frontend) CI'da yeşil
- [ ] README "Oturum yenileme" bölümü yeni mimariye göre güncellendi; XSS ve CSRF için ne yapıldığı iki cümleyle açıklandı

**İpucu:** Önce yalnızca backend'i bitirip entegrasyon testlerini yeşile çevir (Postman / tarayıcı olmadan),
sonra frontend'e geç. Çoklu sekme kısmını en sona bırak ve ayrı bir commit olarak yap.

---

## Genel Kurallar (tüm görevler için)

- Her görev için **ayrı Issue, ayrı branch, ayrı PR**; PR açıklamasında "Closes #<issue-no>".
- PR açmadan önce yerelde `dotnet build`, `dotnet test`, `npm test` ve `npm run build` hatasız geçmeli;
  **CI yeşil olmadan** Sema'ya review için haber verme.
- Kullanıcıya görünen her yeni metin **TR + EN** olarak `LanguageContext`'e eklenmeli.
- Davranışı değişen her şey README'de güncellenmeli.
- Yeni bir **runtime paketi** (npm `dependencies` / NuGet) eklemen gerekirse **önce Sema'ya sor**
  (test kütüphaneleri `devDependencies` / test projesi hariç). Bu hafta yeni paket gerektiren bir görev yok;
  gerekiyor gibi görünüyorsa çözümü bir daha düşün.
- Yeni davranışın testini yazmadan PR açma; "testi sonra eklerim" PR'ı kabul edilmez.
- Takıldığın ya da bir kabul kriterini farklı yorumladığın yerde tahmin yürütmek yerine Issue'ya / PR'a yaz.
