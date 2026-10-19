# 12. Hafta Görevleri — Güvenilirlik, Frontend Testleri, Gerçek Zamanlı Bildirimler

**Bağlam:** `TASKS_HAFTA11.md`'deki üç görev (refresh token #94, bot stop-loss / take-profit #95,
portföy değer geçmişi grafiği #96) PR #97, #98, #99 ile `main`'e taşındı ve kabul kriterlerini
karşılıyor. Bu hafta yeni özellikten çok, o özelliklerin **dayanıklılığını** ve projenin
**test / altyapı eksiklerini** kapatıyoruz; son görev ise projenin en büyük teknik adımı:
sayfa yenileme/polling yerine **anlık (gerçek zamanlı) bildirim**.

| # | Görev | Zorluk |
|---|-------|--------|
| 1 | Güvenilirlik: refresh token temizliği + stop-loss satışı için yeniden deneme | Normal |
| 2 | Frontend test altyapısı + CI'a ekleme + `.gitattributes` | Normal |
| 3 | SignalR ile gerçek zamanlı bildirimler (polling'in yerine) | **Zor** |

> **Numaralandırma:** Önceki haftalardaki gibi — her görev için önce bir **GitHub Issue** aç,
> branch adı `feature/<issue-no>-kisa-ad`, commit ve PR başlığı `#<issue-no> ...` ile başlasın.
> Kod içine `// Görev NN` yorumu yazma.

---

## 1. Güvenilirlik: Refresh Token Temizliği + Stop-Loss Satışı İçin Yeniden Deneme

**Bu görev ne işe yarıyor?**

Geçen haftaki iki özellik çalışıyor ama ikisinde de "ilk kullanımda sorun yok, zamanla
sorun çıkar" türünden eksikler var. Bu görev ikisini de kapatıyor. İki ayrı parça, aynı PR.

### Parça A — Eski refresh token kayıtlarının temizlenmesi

Her giriş ve her token yenilemesi `RefreshToken` tablosuna **yeni bir satır** ekliyor; süresi
dolmuş ya da iptal edilmiş satırları hiçbir şey silmiyor. Kullanıcı sayısı ve süre arttıkça
tablo sınırsız büyüyor. Ayrıca iki sekme aynı anda yenileme yaparsa ikincisi "kullanılmış token"
olarak algılanıp **kullanıcının tüm oturumlarını kapatıyor** (reuse detection) — güvenlik
açısından doğru ama sıradan bir çoklu-sekme kullanımında kullanıcıyı gereksiz yere atıyor.

**Yapılacaklar**
- `RefreshTokenCleanupService` (`BackgroundService` + `PeriodicTimer`; `PortfolioSnapshotService`
  ile aynı kalıp, `IClock` kullan): günde bir çalışsın, **süresi dolmuş** ve **iptal edilmiş
  olup belirli bir süre geçmiş** kayıtları silsin. İptal edilmiş kayıtları hemen silme —
  reuse detection'ın çalışması için iptal tarihinden sonra bir süre (örn. 30 gün) tutulmalı.
  Süreler `appsettings.json`'da ayarlanabilir olsun (`Jwt` altına ya da ayrı bir bölüme).
- Asıl iş mantığını servisten ayır (`PortfolioSnapshotProcessor` gibi) ki hosted service'i
  ayağa kaldırmadan test edilebilsin.
- **Kısa tolerans penceresi (grace window):** Bir refresh token rotasyonla iptal edildikten
  sonra **birkaç saniye** (örn. 10 sn, ayarlanabilir) içinde aynı token tekrar gelirse bu
  bir saldırı sayılmasın: tüm oturumları kapatma, istek reddedilsin (401) ama kullanıcı
  atılmasın. Pencere dışında gelirse mevcut davranış (tüm oturumlar kapanır) aynen kalsın.
  Bunun için `RefreshToken` kaydına "ne zaman iptal edildi" bilgisinin zaten tutulduğunu
  kontrol et (`RevokedAt`); gerekiyorsa migration ekle.

### Parça B — Başarısız stop-loss / take-profit satışının yeniden denenmesi

`BotMonitorService.TryRiskExitAsync` içinde, bir risk çıkışı satışı denendiğinde hata alırsa
(Binance geçici hata verdi, bakiye anlık yetersiz göründü vb.) aynı sinyal için **bir daha
hiç denenmiyor** (`alreadyTried` koruması). Yani fiyat stop-loss seviyesinin çok altına
inebilir, bot hâlâ pozisyonu elinde tutar. Risk yönetiminin asıl amacı tam bu durumda
çalışmak.

**Yapılacaklar**
- `TryRiskExitAsync`'i oku ve `alreadyTried` korumasının neyi engellemeye çalıştığını anla
  (muhtemelen aynı sinyalin sürekli tekrar tekrar üretilmesi / e-posta yağmuru). Amaç bu
  korumayı **kaldırmak değil**, daha akıllı yapmak.
- Başarısız bir risk satışı bir sonraki izleme döngüsünde **yeniden denensin**; ama deneme
  sayısı sınırlı olsun (örn. 5, ayarlanabilir) ve denemeler arasında kısa bir bekleme olsun.
- Sınıra ulaşılırsa kullanıcıya **bir kez** "Stop-loss satışı gerçekleştirilemedi, lütfen
  manuel kontrol edin" bildirimi/e-postası gitsin (TR/EN). Her denemede e-posta gitmesin
  (mevcut 1 saat cooldown kuralı korunmalı).
- Başarılı olursa mevcut davranış aynen devam etsin.

**Test (zorunlu)**
- Cleanup: süresi dolmuş silinir, aktif olan silinmez, yeni iptal edilen silinmez, eski
  iptal edilen silinir (`IClock` ile zamanı ilerlet).
- Grace window: pencere içinde tekrar gelen token → 401 ama diğer oturumlar **hâlâ geçerli**;
  pencere dışında → tüm oturumlar iptal.
- Risk satışı: ilk deneme başarısız → sonraki döngüde yeniden denenir; N başarısız denemeden
  sonra durur ve bildirim bir kez gider; başarılı denemeden sonra tekrar denenmez.

**Kabul kriterleri**
- [ ] Cleanup servisi kayıtlı, günde bir çalışıyor, süreler `appsettings.json`'dan okunuyor
- [ ] İki sekmenin aynı anda yenilemesi kullanıcıyı sistemden atmıyor
- [ ] Kullanılmış token'ın pencere dışında tekrar gelmesi hâlâ tüm oturumları kapatıyor (mevcut testler yeşil)
- [ ] Başarısız risk satışı sınırlı sayıda yeniden deneniyor, sınırda tek bildirim gidiyor
- [ ] Yeni birim testleri yazıldı, tüm testler CI'da yeşil
- [ ] README'deki "Oturum yenileme" ve stop-loss bölümlerine yeni davranışlar eklendi

---

## 2. Frontend Test Altyapısı + CI'a Ekleme + `.gitattributes`

**Bu görev ne işe yarıyor?**

Backend'de 20'den fazla test dosyası var ama **frontend'de tek bir test yok**. Oysa en
kritik ve en hata-yatkın mantıklardan biri frontend'de: `apiService.js` içindeki axios
interceptor'ı (401 gelince refresh token ile yenile, aynı anda gelen birden çok isteği tek bir
yenilemeye bağla — "single-flight"). Bunu bozan bir değişiklik şu an hiçbir yerde
yakalanmıyor. Ayrıca `main`'e her merge'de satır sonu (CRLF/LF) farkından yüzlerce dosya
"değişmiş" görünüyor; bu da gerçek değişiklikleri gözden kaçırmaya yol açıyor.

**Yapılacaklar**
- Frontend'de test çalıştırma altyapısı: `react-scripts` zaten Jest ile geliyor, yeni bir
  test çalıştırıcı kurmaya gerek yok. Bileşen testleri için `@testing-library/react` ve
  `@testing-library/jest-dom` eklemen gerekiyorsa ekle (bunlar test kütüphanesi, Sema'dan
  ayrıca onay gerekmez; ama başka bir paket eklemek istersen önce sor).
- `npm test` komutu CI'da etkileşimsiz çalışacak şekilde (`CI=true npm test -- --watchAll=false`)
  `frontend-ci.yml`'e **build'den önce** bir adım olarak eklensin.
- En az şu testler yazılsın:
  - `apiService` interceptor'ı: 401 → refresh çağrılır → asıl istek yeni token ile tekrarlanır;
    **aynı anda 3 istek 401 alırsa refresh yalnızca 1 kez çağrılır**; refresh de başarısız
    olursa oturum temizlenir ve giriş sayfasına yönlendirilir. (Ağ çağrılarını mock'la —
    gerçek backend'e gitmesin.)
  - `utils/botSignalReason.js`: Strategy / StopLoss / TakeProfit için doğru etiket.
  - `PortfolioHistoryChart`: yükleniyor / hata / boş / tek nokta / çok nokta durumları
    doğru metni gösteriyor (`recharts`'ı mock'lamak gerekebilir; `ResponsiveContainer`
    jsdom'da boyut ölçemez).
- Depo köküne bir `.gitattributes` dosyası ekle (`* text=auto eol=lf` ve ikili dosyalar —
  png, ico, xlsx, woff2 vb. — için `binary` ya da `-text`). Eklemeden önce `git add --renormalize .`
  ile mevcut dosyaların satır sonlarını normalize et ve bunu **ayrı bir commit** olarak yap
  (commit mesajı "satır sonları normalize edildi" gibi), böylece PR'da gerçek değişiklikler ile
  satır sonu gürültüsü karışmaz.

**Kabul kriterleri**
- [ ] `cd frontend && CI=true npm test -- --watchAll=false` yerelde ve CI'da yeşil
- [ ] Single-flight davranışını bozan bir değişiklik (örn. interceptor'daki bekleme mantığını silmek) testi **kırmızı** yapıyor — bunu bir kez deneyip PR açıklamasına yaz
- [ ] `frontend-ci.yml` testleri çalıştırıyor, test kırmızıysa workflow kırmızı
- [ ] `.gitattributes` eklendi; PR sonrası `git status` temiz repo üzerinde "değişmiş" dosya göstermiyor
- [ ] README'ye "Testleri çalıştırma" bölümü (backend + frontend) eklendi

---

## 3. (ZOR) SignalR ile Gerçek Zamanlı Bildirimler

**Bu görev ne işe yarıyor?**

Şu an kullanıcıya gelen bildirimler **polling** ile (frontend belli aralıklarla backend'e
"yeni bir şey var mı?" diye soruyor) çalışıyor: `AlertMonitor.jsx`, `BotSignalApproval.jsx`,
`Profile.jsx` içinde ayrı ayrı `setInterval` var. Bunun üç sorunu var: bildirim en kötü
durumda bir polling aralığı kadar geç görünüyor; kimse bir şey yapmıyorken bile her
kullanıcı sürekli istek atıyor; ve her sayfa kendi zamanlayıcısını yönettiği için mantık
dağınık.

**SignalR**, ASP.NET Core'un hazır gerçek zamanlı iletişim kütüphanesi: tarayıcı backend'e
kalıcı bir bağlantı açıyor, backend olay olunca **kendisi** tarayıcıya mesaj itiyor. Alarm
tetiklendiğinde ya da bot sinyal ürettiğinde kullanıcı anında görüyor.

> **Paket onayı:** Bu görev için frontend'e `@microsoft/signalr` paketini eklemen **onaylı** —
> başka yeni paket gerekirse önce Sema'ya sor. Backend tarafı ASP.NET Core ile birlikte
> geliyor, ek paket gerekmez.

**Önce oku (kod okuma kısmı bu görevin yarısı):**
- `AlertMonitor.jsx`, `BotSignalApproval.jsx`, `Profile.jsx` — hangi veriyi, hangi aralıkla,
  hangi endpoint'ten çekiyorlar?
- `AlertMonitoringProcessor` ve `BotMonitorService` — alarm tetiklenince / sinyal üretilince
  ne yapıyorlar (e-posta, DB kaydı...)? Bildirimi hangi noktada göndereceksin?
- `JwtService` — token'a kullanıcı kimliği hangi claim ile yazılıyor? SignalR'ın
  `Clients.User(...)` ile doğru kullanıcıya göndermesi bu claim'e bağlı.
- `Program.cs` — CORS ve kimlik doğrulama ayarları.

**Yapılacaklar — backend**
- `NotificationHub` (`[Authorize]`) ekle ve `/hubs/notifications` altında yayınla.
- WebSocket bağlantılarında tarayıcı `Authorization` header'ı gönderemez; token
  **`access_token` sorgu parametresi** ile gelir. `JwtBearerEvents.OnMessageReceived` içinde
  bunu yalnızca `/hubs` yolları için oku. (Normal API yollarında sorgu parametresinden token
  **kabul edilmemeli** — güvenlik gereği.)
- CORS politikasını SignalR için güncelle (`AllowCredentials` ve açık origin listesi gerekir;
  `AllowAnyOrigin` ile birlikte kullanılamaz).
- Bir `IRealtimeNotifier` soyutlaması yaz (`SendToUserAsync(userId, eventName, payload)`);
  gerçek implementasyonu `IHubContext<NotificationHub>` kullansın. İş servisleri (alarm
  işlemcisi, bot izleyici) doğrudan hub'a değil bu arayüze bağımlı olsun — test edilebilirlik
  için şart.
- Şu olaylar bağlı kullanıcıya itilsin: **alarm tetiklendi**, **bot yeni sinyal üretti**
  (stop-loss / take-profit dahil, `reason` ile birlikte), **bot sinyali onay bekliyor**.
  Mevcut e-posta ve DB kayıtları aynen kalsın; bu **ek** bir kanal.
- Bir kullanıcının olayı **yalnızca o kullanıcıya** gitmeli. Başka kullanıcının bağlantısına
  asla sızmamalı.

**Yapılacaklar — frontend**
- Tek bir `RealtimeContext` (ya da hook) bağlantıyı yönetsin; bileşenler oradan olaylara
  abone olsun. Üç dosyadaki dağınık `setInterval` mantığı buna taşınsın.
- `withAutomaticReconnect()` kullan. **Dikkat — zor kısım:** access token 15 dakikada bir
  doluyor. `accessTokenFactory` her bağlanışta **güncel** token'ı vermeli (gerekirse önce
  refresh'i tetikle) ve bağlantı koptuğunda yeniden bağlanırken süresi dolmuş token
  kullanılmamalı. Çıkış yapılınca bağlantı kapatılmalı, giriş yapılınca açılmalı.
- **Geri dönüş (fallback):** bağlantı kurulamıyorsa ya da kopmuşsa eski polling devreye
  girsin; bağlantı geri gelince polling dursun. Kullanıcı hiçbir durumda bildirimsiz
  kalmamalı. Arayüzde küçük bir bağlantı durumu göstergesi (bağlı / yeniden bağlanıyor /
  çevrimdışı) olsun (TR/EN).
- Aynı olay hem SignalR hem polling ile gelirse bildirim **iki kez** görünmemeli
  (olay kimliğine göre tekilleştir).

**Test (zorunlu)**
- Backend: `IRealtimeNotifier` sahte (fake) implementasyonuyla, alarm tetiklenince ve bot
  sinyal üretince doğru kullanıcıya doğru olayın gönderildiğini doğrulayan testler.
- Backend: `/hubs/notifications` token'sız bağlantıyı reddediyor; `access_token` sorgu
  parametresi **normal bir API yolunda** (örn. `/api/portfolio/history`) kabul edilmiyor.
- Backend (isteğe bağlı ama çok değerli): `WebApplicationFactory` + SignalR istemcisiyle
  iki kullanıcı bağla, birinin olayının diğerine gitmediğini göster.
- Frontend: `RealtimeContext` için bağlantı koptuğunda polling'e geçiş ve geri gelince
  polling'in durması (SignalR bağlantısını mock'la).

**Kabul kriterleri**
- [ ] Alarm / bot sinyali olduğunda açık sekmede bildirim **polling aralığını beklemeden** görünüyor
- [ ] Başka kullanıcının olayı bende görünmüyor (otomatik testle kanıtlı)
- [ ] Token'sız hub bağlantısı reddediliyor; sorgu parametresindeki token yalnızca `/hubs` için geçerli
- [ ] Access token süresi dolduktan sonra (15 dk) bağlantı kopsa bile otomatik, yeni token ile yeniden bağlanıyor
- [ ] Backend durdurulunca arayüz "çevrimdışı" gösteriyor ve polling devreye giriyor; backend dönünce SignalR'a geri dönüyor
- [ ] Aynı bildirim iki kez görünmüyor
- [ ] Çıkış yapınca bağlantı kapanıyor (tarayıcı geliştirici araçlarının Network → WS sekmesinden doğrula)
- [ ] Docker'da (`docker-compose` / yerel) çalışıyor; WebSocket için proxy ayarı gerekiyorsa README'ye yazıldı
- [ ] README'ye mimari özeti (hub, olaylar, fallback) ve olay listesi eklendi
- [ ] Tüm testler CI'da yeşil

**İpucu:** Önce yalnızca *tek bir olay* (alarm tetiklendi) için uçtan uca çalıştır (hub →
bağlantı → ekranda bildirim), sonra diğer olayları ve fallback'i ekle. Tek PR olacak ama
commit'leri küçük tut.

---

## Genel Kurallar (tüm görevler için)

- Her görev için **ayrı Issue, ayrı branch, ayrı PR**; PR açıklamasına "Closes #<issue-no>".
- PR açmadan önce yerelde `dotnet build`, `dotnet test`, `npm run build` ve (2. görevden sonra)
  `npm test` hatasız geçmeli; PR'daki **CI yeşil olmadan** Sema'ya review için haber verme.
- Kullanıcıya görünen her yeni metin **TR + EN** olarak `LanguageContext`'e eklenmeli.
- Davranışı değişen her şey README'de güncellenmeli.
- Spesifikasyonda olmayan yeni bir paket eklemen gerekirse **önce Sema'ya sor** (geçen hafta
  `recharts` sorulmadan eklendi; bu sefer atlanmasın).
- Takıldığın yerde ya da bir kabul kriterini farklı yorumladığında, tahmin yürütmek yerine
  PR'a ya da Issue'ya yaz.
