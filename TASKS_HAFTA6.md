# 6. Hafta Görevleri — Alım-Satım Robotu (Faz 1: Sinyal Önerisi)

Yönetici/ekip talebi: alım-satımlar için bir "robot". Konuştuğumuz gibi bu hafta **tam
otonom bir strateji motoru değil**, dar kapsamlı bir proof-of-concept yapıyoruz:

- **Otomasyon seviyesi:** Robot işlemi kendisi yapmıyor — bir **sinyal/öneri** üretiyor
  ("RSI 28, BTCUSDT için AL önerisi"), kullanıcı onaylarsa işlem sanal portföyde gerçekleşiyor.
  Böylece hatalı bir strateji sanal bakiyeyi tek seferde eritmiyor, ekip de tam otomasyona
  güvenmeden önce robotun mantığını gözlemleyebiliyor.
- **Strateji:** Bu hafta tek ve basit bir kural — **RSI eşik değeri** (RSI 30 altına inince
  AL, 70 üstüne çıkınca SAT). Genel bir "strateji motoru" kurmuyoruz, tek kuralı sağlam
  şekilde çalıştırıyoruz.
- **Kapsam:** Her şey 5. haftada kurulan **sanal portföy** üzerinde çalışıyor. Gerçek para,
  gerçek borsa bağlantısı yok. Eğer ileride gerçek otomatik işlem gündeme gelirse bu çok
  daha büyük ve dikkatli ele alınması gereken ayrı bir konu — bu haftanın kapsamında değil.

Mimari not: Görev 15'teki `AlertMonitorService` (dakikada bir çalışan arka plan servisi) ile
aynı desene benziyor — çoğu ekip üyesi o kodu zaten biliyor, referans olarak kullanılabilir.
Fark: klinecharts'ın RSI hesaplaması **frontend'de** çalışıyor, bot ise sunucu tarafında
çalışacağı için RSI'ı **backend'de bağımsız olarak yeniden hesaplamak** gerekiyor.

Sıra: **21 → 22 zorunlu** (veri modeli + servis önce). 22 bitince **23, 24, 25 paralel**.

---

## Görev 21 — Bot Veri Modeli, RSI Hesaplama & Periyodik Değerlendirme Servisi (Backend)

**Açıklama**
Robotun temeli: kullanıcının kurduğu botları düzenli aralıklarla kontrol edip RSI eşiği
sağlandığında bir sinyal kaydı oluşturan arka plan servisi.

**Yapılacaklar**
- `Models/TradingBot.cs`: `UserId`, `Symbol`, `IsActive`, `BuyRsiThreshold` (varsayılan 30),
  `SellRsiThreshold` (varsayılan 70), `TradeQuantity` (her sinyalde alınacak/satılacak miktar), migration
- `Models/BotSignal.cs`: `BotId`, `SignalType` (Buy/Sell), `RsiValueAtSignal`, `PriceAtSignal`,
  `CreatedAt`, `Status` (enum: `Pending`, `Approved`, `Rejected`, `Expired`), migration
- Backend'de **bağımsız bir RSI hesaplama fonksiyonu** yaz (klinecharts'a bağımlı değil —
  o sadece frontend'de çiziyor). Binance kline REST verisiyle standart RSI formülünü uygula
- `Services/BotMonitorService.cs` (`BackgroundService`, `PeriodicTimer`, her 60 saniyede bir):
  aktif (`IsActive`) botları çek, sembole göre grupla (Görev 15'teki gibi Binance'e gereksiz
  istek atmayın), her sembol için kline verisiyle RSI hesapla, eşik koşulu sağlanıyorsa yeni
  `BotSignal` (`Status = Pending`) kaydı oluştur — **aynı koşul için art arda tekrar sinyal
  üretmesin** (örn. bekleyen bir `Pending` sinyal varken yenisini oluşturma)
- `Program.cs`'e servisi kaydet

**Kabul kriterleri**
- [ ] RSI değeri backend'de doğru hesaplanıyor (bilinen bir örnekle elle doğrulanmalı)
- [ ] Aktif bot, eşik koşulu sağlanınca yeni bir `BotSignal` (`Pending`) oluşturuyor
- [ ] Aynı koşul için art arda gereksiz sinyal spamlanmıyor
- [ ] Aynı sembolü izleyen birden fazla bot varsa Binance'e sembol başına tek istek atılıyor
- [ ] Bot pasif yapılırsa (`IsActive = false`) kontrol edilmiyor

---

## Görev 22 — Bot API'si & Sinyal Onaylama → Portföy Entegrasyonu (Backend)

**Açıklama**
Kullanıcının bot kurabildiği, sinyalleri onaylayıp reddedebildiği ve onaylanan sinyalin
gerçekten sanal portföyde işlem yaptığı API katmanı.

**Yapılacaklar**
- `BotController.cs`: `GET /api/Bot` (kullanıcının botları), `POST /api/Bot` (yeni bot: sembol,
  eşikler, miktar), `PATCH /api/Bot/{id}/toggle` (aktif/pasif), `GET /api/Bot/{id}/signals`
  — hepsi `[Authorize]`
- `POST /api/Bot/signals/{signalId}/approve`: `PortfolioService.BuyAsync`/`SellAsync`'i
  (Görev 17) çağırır, başarılıysa sinyali `Status = Approved` yapar
- `POST /api/Bot/signals/{signalId}/reject`: sinyali `Status = Rejected` yapar, hiçbir
  portföy işlemi yapılmaz
- Bekleyen bir sinyal belirli bir süre (örn. 15 dakika) onaylanmazsa `Status = Expired`
  olacak şekilde basit bir kontrol ekleyin (Görev 21'deki servisin içinde ya da ayrı bir kontrol)

**Kabul kriterleri**
- [ ] Bot oluşturma/aç-kapa çalışıyor, sadece sahibi kendi botlarını görüp değiştirebiliyor
- [ ] Sinyal onaylanınca sanal portföyde gerçekten alım/satım oluyor (bakiye/holding güncelleniyor)
- [ ] Reddedilen sinyalde hiçbir portföy değişikliği olmuyor
- [ ] Süresi geçen sinyal `Expired` oluyor, artık onaylanamıyor
- [ ] Onaylama sırasında bakiye yetersizse (Görev 17'deki kural) anlamlı hata dönüyor

---

## Görev 23 — Bot Kurulum & Yönetim Sayfası (Frontend)

**Açıklama**
Kullanıcının bot kurup yönetebileceği arayüz.

**Yapılacaklar**
- `apiService.js`'e bot fonksiyonlarını ekle (`getBots`, `createBot`, `toggleBot`, `getBotSignals`, vb.)
- Yeni sayfa `src/pages/Bot/Bot.jsx` (`/bot` route, `PrivateRoute` ile korunuyor): coin seçimi,
  RSI eşikleri (varsayılan 30/70, değiştirilebilir), işlem miktarı ile bot oluşturma formu
- Kullanıcının mevcut botlarını listeleyen tablo: sembol, eşikler, aktif/pasif durumu, aç-kapa butonu
- `Navbar.jsx`'e giriş yapılmışsa "Robot" veya "Botlarım" linki

**Kabul kriterleri**
- [ ] Bot oluşturma formu çalışıyor, geçersiz eşik değerleri (örn. buy > sell) reddediliyor
- [ ] Bot listesi doğru gösteriliyor, aç/kapa anında yansıyor
- [ ] `/bot` giriş yapılmadan açılırsa `/signin`'e yönleniyor

---

## Görev 24 — Sinyal Onay Ekranı & Bildirim (Frontend)

**Açıklama**
Kullanıcının robotun ürettiği bekleyen sinyalleri görüp onaylayabildiği/reddedebildiği ekran.

**Yapılacaklar**
- Bot sayfasında (veya ayrı bir panelde) bekleyen (`Pending`) sinyalleri listele: "RSI 28.4,
  BTCUSDT için AL önerisi" gibi okunaklı bir metinle, "Onayla" / "Reddet" butonlarıyla
- Kullanıcı uygulamayı açıkken yeni sinyal geldiğinde toast/bildirim göster (Görev 15'teki
  polling deseniyle aynı mantık kullanılabilir)
- Onaylanan/reddedilen/süresi geçen sinyalleri ayrı bir "geçmiş" listesinde göster

**Kabul kriterleri**
- [ ] Bekleyen sinyaller doğru ve anlaşılır şekilde gösteriliyor
- [ ] Onayla/Reddet butonları doğru API'yi çağırıyor ve sonucu anında yansıtıyor
- [ ] Yeni sinyal geldiğinde kullanıcı bir bildirim görüyor
- [ ] Geçmiş sinyaller (onaylanan/reddedilen/süresi geçen) ayrı görülebiliyor

---

## Görev 25 — Bot Performans Özeti

**Açıklama**
Robotun ürettiği ve onaylanan işlemlerin portföy üzerindeki etkisinin görülebildiği bir özet.

**Yapılacaklar**
- Bot sayfasına (veya Portföy sayfasına) küçük bir "Bot Performansı" bölümü: toplam üretilen
  sinyal sayısı, onaylanan/reddedilen/süresi geçen oranı, bot üzerinden yapılan işlemlerin
  toplam kâr/zarara katkısı (Görev 18'deki portföy hesaplamasından faydalanılabilir)
- Bu veriler sade bir tablo/kart olarak yeterli, karmaşık grafik gerekmiyor bu hafta

**Kabul kriterleri**
- [ ] Sinyal sayıları (toplam/onaylanan/reddedilen/süresi geçen) doğru gösteriliyor
- [ ] Bot işlemlerinin portföy değerine etkisi doğru hesaplanıyor
- [ ] Kullanıcı hangi işlemlerin bot tarafından mı yoksa elle mi yapıldığını ayırt edebiliyor

---

## Genel Kurallar

- Sıra: **21 → 22 zorunlu**. 22 bitince **23, 24, 25 paralel** yürütülebilir.
- Bu robot **gerçek para veya gerçek borsa hesabıyla işlem yapmıyor** — sadece sanal portföy
  üzerinde çalışıyor. İleride gerçek otomatik işlem gündeme gelirse bu ayrı, çok daha dikkatli
  ele alınması gereken bir konu olacak; bu haftanın parçası değil.
- Görev 21'i alacak kişi Görev 15'teki `AlertMonitorService` kodunu incelesin, aynı deseni
  (gruplama, periyodik kontrol, rate-limit önlemi) tekrar kullanabilir.
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli.