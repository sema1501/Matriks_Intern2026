# 7. Hafta Görevleri — Binance Testnet, Backtest & Trend Çizgisi Araçları

**Not:** Bu dosya, bu haftanın ilk taslağının (bot genişletme + admin gözetimi, ekip
önceliğiyle yazılmıştı) yerini alıyor. Yönetici toplantıda farklı ve daha öncelikli
istekler iletti, bu hafta onlara odaklanıyoruz. Eski taslaktaki admin gözetim fikirleri
(bot/portföy izleme, kill-switch, audit log) silinmedi, ileride ayrı bir hafta olarak
tekrar ele alınabilir.

**Bu haftanın kararları (toplantıda netleştirildi):**
- Robotun onay bekleme adımı (Pending/Approve/Reject) tamamen kaldırılıyor — robot sinyal
  oluşunca doğrudan işlem yapacak.
- Robotun gerçekten çalıştığını görmek için **Binance Testnet** (testnet.binance.vision —
  sahte parayla çalışan, gerçek bir Binance API'si) kullanılacak. **Hibrit yaklaşım:**
  gerçek emirler Testnet'e gönderilecek (böylece "robot gerçekten işlem yapıyor mu"
  doğrulanabilir), ama kullanıcı bazlı bakiye/holding/liderlik tablosu yine bizim
  veritabanımızda bir **defter (ledger)** olarak tutulmaya devam edecek — çünkü Testnet
  tek bir paylaşılan sahte hesap/bakiye veriyor, kullanıcı başına değil.
- Testnet API key/secret **tek, paylaşılan** bir anahtar olacak, backend'de güvenli şekilde
  saklanacak (appsettings/user-secrets — **kesinlikle git'e commit edilmeyecek**).
- Bot kurulum ekranına bir **Backtest** özelliği eklenecek: seçilen tarih aralığında robot
  çalışsaydı ne olurdu, bunu gösteren bir rapor (grafik + sinyal tablosu).
- Trend çizgilerine sağ tık menüsü (uzatma/tür değiştirme) eklenecek.
- **Bonus (bu haftanın 5 görevine dahil değil, ekip vakit bulursa denenebilir):** düşen bir
  trend çizgisi yukarı kırılınca otomatik alarm üretilmesi. Bunun zor olduğu toplantıda
  konuşuldu — uygulama kapalıyken veri takibi + kırılma tespiti + otomatik alarm oluşturma
  gerektiriyor. Bu haftanın kapsamına almıyoruz, ileride ayrı bir görev olarak ele alınacak.

Sıra: **31 → 32 zorunlu** (kimlik doğrulama olmadan emir gönderilemez). **33 bağımsız**,
paralel başlayabilir. **34, 33'e bağımlı.** **35 tamamen bağımsız.**

---

## Görev 31 — Binance Testnet: Kimlik Doğrulama & Hesap Sorgulama (Backend)

**Açıklama**
Robotun gerçek (test) emir gönderebilmesi için önce Binance Testnet'e kimlik doğrulamalı
istek atabilen bir istemci kurulmalı.

**Yapılacaklar**
- `testnet.binance.vision` üzerinden GitHub ile giriş yapıp bir HMAC API key/secret çifti
  oluşturun (bunu kim yapacak ekip içinde netleştirin — tek kişi oluşturup diğerlerine
  güvenli şekilde paylaşsın)
- Backend'de `Services/BinanceTestnetClient.cs`: API key/secret'ı `appsettings.Development.json`
  veya .NET User Secrets'tan okuyan, her isteği **HMAC-SHA256** ile imzalayan bir istemci sınıfı
- `GET /api/v3/account` (Testnet) çağrısıyla hesap bakiyesini çekebilen bir fonksiyon
- appsettings.Development.json'daki gerçek key/secret'ın **.gitignore'da olduğundan emin olun**
  — asla commit edilmemeli
- Basit bir debug endpoint: `GET /api/Testnet/account` (sadece Admin) — entegrasyonun
  çalıştığını görmek için Testnet bakiyesini dönsün

**Kabul kriterleri**
- [ ] Testnet hesabına kimlik doğrulamalı istek atılabiliyor, bakiye doğru dönüyor
- [ ] API key/secret hiçbir şekilde git geçmişinde/commit'te görünmüyor
- [ ] İmzalama hatalı olursa (yanlış key vb.) anlamlı bir hata alınıyor, uygulama çökmüyor

---

## Görev 32 — Binance Testnet: Emir Gönderme & Bot/Portföy Entegrasyonu (Backend)

**Açıklama**
Görev 31'deki istemcinin üzerine gerçek emir gönderme eklenip, botun ve portföy
servislerinin buna bağlanması — ve onay bekleme adımının tamamen kaldırılması.

**Yapılacaklar**
- `BinanceTestnetClient`'a `POST /api/v3/order` (MARKET tipi BUY/SELL) fonksiyonu ekle
- `PortfolioService.BuyAsync`/`SellAsync` (Görev 17): artık önce Testnet'e gerçek MARKET
  emri gönderecek; emir gerçekleşirse (`FILLED`) bizim veritabanımızdaki `Transaction` /
  `PortfolioHolding` / bakiye kayıtları **defter** olarak güncellenecek (kullanıcı bazlı
  görünüm/liderlik tablosu için). Emir başarısız olursa veritabanına hiç yazılmayacak,
  kullanıcıya/bota anlamlı hata dönecek
- `BotMonitorService.cs` (Görev 21): sinyal oluşunca artık `Pending` sinyal oluşturmuyor —
  doğrudan `BuyAsync`/`SellAsync`'i çağırıp gerçek Testnet emri gönderiyor
- `BotController.cs`'teki onay/red (`approve`/`reject`) endpoint'lerini kaldırın
- **Doğrulama (mutlaka yapılmalı):** art arda 5 kere sabit miktarda (örn. 10$'lık) test
  alımı tetikleyin, Testnet hesabındaki emir geçmişinden/bakiyeden toplamda beklenen
  miktarın (50$) gerçekten alındığını doğrulayın — ekran görüntüsü/log ile kaydedin

**Kabul kriterleri**
- [ ] Bot sinyal ürettiğinde onay beklemeden doğrudan Testnet'e gerçek emir gönderiliyor
- [ ] Emir başarılı olduğunda kullanıcının bizim veritabanımızdaki bakiye/holding kaydı
      doğru güncelleniyor
- [ ] Emir başarısız olursa veritabanı tutarsız hale gelmiyor (yarım güncelleme yok)
- [ ] 5 kere art arda test alımı yapıldığında Testnet hesabında beklenen toplam miktar
      gerçekten alınmış olarak görünüyor
- [ ] Eski onay/red akışı tamamen kaldırıldı, kod ve arayüzde artık görünmüyor

---

## Görev 33 — Backtest Motoru (Backend)

**Açıklama**
Kullanıcının seçtiği tarih aralığında, botun stratejisi geçmiş veride çalıştırılsaydı ne
olurdu — bunu hesaplayan, **gerçek emir göndermeyen** (Testnet'e hiç gitmeyen), saf
simülasyon motoru.

**Yapılacaklar**
- Yeni endpoint: `POST /api/Bot/{id}/backtest` (parametreler: başlangıç tarihi, bitiş tarihi)
- Seçilen tarih aralığı için Binance kline REST verisini çek (Görev 4'teki `getKlines`
  mantığının backend karşılığı — ya da mevcut REST çağrısı backend'den de yapılabilir)
- Botun stratejisine (RSI eşiği veya EMA kesişimi — Görev 21/26) göre bu geçmiş veriyi
  bar bar simüle et: her barda RSI/EMA hesapla, eşik/kesişim koşulu sağlanırsa bir sinyal
  kaydet (tarih, yön, o barın kapanış fiyatı, o andaki indikatör değeri)
- Sonuç: sinyal listesi + özet (toplam sinyal sayısı, hipotetik kâr/zarar) JSON olarak dönsün
- Bu işlem tamamen hesaplama — **hiçbir gerçek veya test emri gönderilmez**

**Kabul kriterleri**
- [ ] Farklı tarih aralıkları için tutarlı, doğru sinyal listesi üretiliyor
- [ ] Sinyal listesindeki fiyat/indikatör değerleri o tarihteki gerçek geçmiş veriyle uyuşuyor
- [ ] Backtest sırasında hiçbir gerçek/test emri gönderilmiyor (bunu ayrıca test edin —
      Testnet hesap geçmişinde backtest'ten kaynaklı işlem görünmemeli)
- [ ] Çok uzun tarih aralıklarında da makul sürede (birkaç saniye) sonuç dönüyor

---

## Görev 34 — Backtest Raporu Ekranı (Frontend)

**Açıklama**
Görev 33'teki motorun sonucunu, yöneticinin istediği şekilde görselleştiren rapor ekranı.

**Yapılacaklar**
- Bot kurulum/yönetim sayfasına (Görev 23) bir **"Backtest"** butonu + tarih aralığı seçici ekle
- Rapor bir modal veya ayrı sayfa olarak açılsın, içinde:
  - **Üstte:** `klinecharts` ile mum/HLC grafiği, sinyal noktalarında **alışa yeşil yukarı ok,
    satışa kırmızı aşağı ok** overlay'i (ilgili barın üzerine yerleştirilecek)
  - **Onun altında, ayrı bir panel:** RSI çizgisi (Görev 12'deki indikatör panelinin aynısı)
  - **En altta bir datagrid:** sütunlar — Tarih, Alış/Satış, Fiyat, RSI Değeri; her sinyal
    bir satır, kronolojik sırayla
- Amaç kullanıcıya net şekilde göstermek: "robot bu tarihler arasında çalışsaydı böyle
  sinyaller üretirdi"

**Kabul kriterleri**
- [ ] Backtest butonu ve tarih seçici çalışıyor
- [ ] Grafikte alış/satış okları doğru barların üzerinde, doğru renkte gösteriliyor
- [ ] RSI paneli grafiğin altında ayrı olarak çiziliyor
- [ ] Datagrid'deki veriler grafikteki sinyallerle birebir eşleşiyor
- [ ] Sonuç boşsa (o aralıkta hiç sinyal yoksa) kullanıcıya anlaşılır bir mesaj gösteriliyor

---

## Görev 35 — Trend Çizgisi Sağ Tık Menüsü

**Açıklama**
Görev 14'te çizilen trend çizgilerine sağ tıklandığında bir işlem menüsü açılması.

**Yapılacaklar**
- Çizilmiş bir trend çizgisine sağ tıklayınca küçük bir context menu aç: **"Sağa Uzat"**,
  **"Sola Uzat"**, **"Yükselen Trende Çevir"**, **"Düşen Trende Çevir"**
- `klinecharts` overlay API'siyle: uzatma seçenekleri çizginin bitiş noktasını zaman
  ekseninde ileri/geri taşımalı (böylece kullanıcı çizginin gelecekte/geçmişte fiyatla
  kesişip kesişmediğini görebilir); tür değiştirme seçenekleri çizginin tipini/etiketini
  (yükselen/düşen trend) güncellemeli — bu etiket ileride (bonus alarm özelliğinde)
  kullanılacağı için veri modelinde saklanmalı
- Menü dışına tıklanınca kapanmalı, mobilde de makul bir şekilde çalışmalı (örn. uzun basma)

**Kabul kriterleri**
- [ ] Sağ tık ile menü açılıyor, dört seçenek de çalışıyor
- [ ] "Uzat" seçenekleri çizgiyi görsel olarak doğru yönde uzatıyor
- [ ] "Trende çevir" seçenekleri çizginin türünü değiştirip bunu kalıcı olarak saklıyor
- [ ] Menü dışına tıklanınca kapanıyor

---

## Genel Kurallar

- Sıra: **31 → 32 zorunlu**. **33 bağımsız** (Testnet'e hiç dokunmuyor, paralel başlayabilir).
  **34, 33'e bağımlı.** **35 tamamen bağımsız**, herkesten ayrı yürütülebilir.
- **Güvenlik:** Testnet API key/secret'ı hiçbir zaman koda gömülmeyecek, hiçbir commit'te
  görünmeyecek. appsettings.Development.json zaten .gitignore'da mı önce kontrol edin.
- Görev 32'yi alacak kişi Görev 17 (`PortfolioService`) ve Görev 21-22 (`BotMonitorService`,
  `BotController`) kodunu iyi bilsin — bunlar üzerinde köklü bir değişiklik yapılıyor.
- Bonus (düşen trend kırılınca otomatik alarm) bu haftanın görevlerine dahil değil — ekip
  vakit bulursa Görev 35 bittikten sonra denenebilir ama teslim kriteri değil.
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli.