# 9. Hafta Görevleri — EMA Stratejisi, Dokümantasyon Açığı & Test Kapsamı

**Bağlam:** Bu dosya, projenin mevcut kod tabanı (backend Controllers/Models/Services,
frontend pages) `README.md` ve `TASKS_HAFTA*.md` dosyalarıyla satır satır karşılaştırılarak
hazırlandı. İki bulgu bu haftanın görevlerini belirliyor:

1. `TASKS_HAFTA8.md` hâlâ "Taslak — Yönetici Onayı Bekleniyor" diyor, ama içindeki
   **Görev 36, 37 ve 38 (admin paneli, kill switch, audit log) zaten tamamlanmış**
   (`AdminController.cs`, `AuditLogService.cs`, `AdminBots.js` kodda mevcut). Sadece
   **Görev 39 (EMA kesişimi stratejisi) hiç başlanmamış** — `TradingBot.cs`'de hâlâ
   sadece RSI eşiği var, `BacktestService.cs`'de strateji adı `"RSI"` olarak sabitlenmiş.
2. Görev 40'tan sonra kod içine **hiçbir TASKS dosyasına yazılmadan** Görev 42, 43, 46, 48
   ve 50 numaralı işler eklenmiş (kod içi yorumlardan görülüyor): coin karşılaştırma
   sayfası, profil fotoğrafı, yüzde-değişim alarmı, e-posta doğrulama, şifre sıfırlama
   e-postasının gerçek gönderimi. Bu işlerin hiçbiri README'nin "Özellikler" bölümünde
   yok ve **hiçbirinin testi yok** — projenin geri kalanında (RSI, backtest, bot, audit
   log) güçlü bir test kültürü varken bu son eklenenler tamamen test dışı kalmış.

Bu hafta tek kişi (uzun dönem stajyer) yürütüldüğü için görevler arasında ekip
koordinasyonu gerekmiyor; önerilen sıra: **39 → 51 → 52** (önce teknik riski en yüksek
iş, sonra onun test alışkanlığını pekiştiren görev, en son da her ikisinin sonucunu da
yansıtabilecek dokümantasyon güncellemesi). Üçü de birbirinden bağımsız, istenirse
farklı sırayla da yürütülebilir.

> **Not (numaralandırma):** 39 numarası `TASKS_HAFTA8.md`'den taşınıyor. 51 ve 52,
> kodda referansı bulunan en yüksek görev numarasının (50) devamı olarak seçildi; bu
> inceleme kodun tamamını taramadı, o yüzden dosyayı onaylamadan önce 51/52'nin başka
> bir yerde kullanılmadığını bir `grep -r "Görev 5" .` ile teyit et.

---

## Görev 39 — Bot: EMA Kesişimi Stratejisi

**Açıklama**
Bot şu an yalnızca RSI eşiğiyle çalışıyor. Bu görevde EMA (Üstel Hareketli Ortalama)
kesişimi ikinci bir strateji seçeneği olarak ekleniyor. Hem canlı bot çalıştırma
(`BotMonitorService`) hem backtest (`BacktestService`) bu yeni stratejiyi desteklemeli —
Görev 33'ün orijinal tanımı zaten "RSI eşiği veya EMA kesişimi" diye ikisini birden
varsaymıştı, backtest tarafı bunu hiç karşılamadı.

**Yapılacaklar**
- `Models/TradingBot.cs`: `Strategy` enum ekle (`RsiThreshold` — mevcut, varsayılan;
  `EmaCrossover` — yeni), `ShortEmaPeriod` (varsayılan 12) / `LongEmaPeriod`
  (varsayılan 26) alanları ekle; migration oluştur (`dotnet ef migrations add
  AddBotStrategy`) — mevcut kayıtlar migration sonrası `RsiThreshold` olarak kalmalı
- `Services/RsiCalculator.cs`'teki desene birebir uyan bağımsız bir
  `Services/EmaCalculator.cs` yaz (`CalculateSeries` metodu, klasik EMA formülü:
  `multiplier = 2 / (period + 1)`)
- `Services/BotMonitorService.cs`: botun `Strategy` alanına göre RSI zone-entry
  (`RsiSignalEvaluator.DetermineZoneEntrySignal`) ya da EMA kesişim kontrolü yapacak
  şekilde dallandır (kısa EMA, uzun EMA'yı yukarı keserse AL; aşağı keserse SAT)
- `Services/BacktestService.cs`: satır ~26'daki sabit `StrategyName = "RSI"` kaldırılıp
  `bot.Strategy`'den okunmalı; `Simulate` metodu EMA stratejisi için de çalışmalı (RSI
  serisi yerine EMA serisi hesaplayıp kesişim noktalarını sinyale çevirmeli)
- `DTOs/BotDtos.cs`: `CreateBotRequest` ve `BotResponse`'a `Strategy`, `ShortEmaPeriod`,
  `LongEmaPeriod` alanlarını ekle
- `Services/BotService.cs`: `CreateBotAsync`'teki RSI eşik doğrulamasına (satır ~38-45)
  benzer şekilde, EMA seçiliyse `ShortEmaPeriod < LongEmaPeriod` ve her ikisinin de
  pozitif olduğunu doğrula
- `Controllers/AdminController.cs`: `GetBots`'taki sabit `"RSI"` string'i (satır ~43)
  yerine gerçek `b.Strategy` değerini dön
- Frontend `pages/Bot/Bot.jsx`: bot oluşturma formuna strateji seçici (RSI/EMA) + seçilen
  stratejiye göre ilgili parametre alanları (RSI eşikleri ya da EMA periyotları) ekle;
  `services/apiService.js`'teki `createBot` çağrısını yeni alanlarla güncelle

**Kabul kriterleri**
- [ ] EMA kesişimi backend'de doğru hesaplanıyor (bilinen bir örnekle elle doğrulanmalı —
      örn. Excel/Python ile çapraz kontrol)
- [ ] EMA kesişimi gerçekleştiğinde doğru yönde sinyal üretiliyor ve gerçek Testnet emri
      gönderiliyor (`BotAutoTradeExecutor` değişmeden çalışmalı)
- [ ] Backtest (Görev 33/34), EMA stratejili bir bot için de doğru sinyal listesi ve özet
      üretiyor
- [ ] Var olan RSI botları (mevcut veritabanı kayıtları) bu değişiklikten etkilenmiyor
- [ ] Admin panelindeki bot listesi (Görev 36) her botun gerçek stratejisini gösteriyor,
      artık hepsi "RSI" yazmıyor
- [ ] Frontend'de strateji seçimine göre form alanları doğru değişiyor, geçersiz
      kombinasyon (örn. `ShortEmaPeriod >= LongEmaPeriod`) reddediliyor

---

## Görev 51 — Test Kapsamı: Son Eklenen, Test Edilmemiş Özellikler

**Açıklama**
`backend/tests/CryptoTracker.API.Tests` altında RSI, backtest, bot, alarm ve audit log
için güçlü bir test kültürü var (`RsiCalculatorTests.cs`, `BotMonitorServiceTests.cs`,
`AuditLogServiceTests.cs` vb.) ama Görev 40/46/48 ile eklenen `NotificationService`,
yüzde-değişim alarmı ve e-posta doğrulama akışının **hiç testi yok**. Bu servisler
cooldown/zamanlama gibi kolayca gözden kaçan uç durumlar içeriyor.

**Yapılacaklar**
- `Services/NotificationService.cs` için `NotificationServiceTests.cs` yaz: cooldown
  süresi (`NotificationOptions.CooldownMinutes`, varsayılan 60dk) dolmadan ikinci
  bildirim gönderilmediğini, `EmailNotificationsEnabled = false` iken hiç
  gönderilmediğini, kullanıcı e-postası boşsa hata fırlatmadan sessizce atlandığını,
  cooldown süresi dolunca yeniden gönderildiğini doğrula (hem `NotifyPriceAlertAsync`
  hem `NotifyBotSignalAsync` için). `triggeredAt` parametre olarak verildiği için zaman
  mock'lamaya gerek yok — farklı `triggeredAt` değerleri geçirerek test edilebilir.
- Var olan `AlertConditionEvaluatorTests.cs` dosyasını genişlet: `AlertType.PercentChange`
  dalı (`IsPercentChangeConditionSatisfied`, satır ~37-53) hiç test edilmemiş. En az şu
  durumları kapsa: normal tetiklenme (Above/Below), `ReferencePrice == null` → false,
  `ReferencePrice <= 0` → false, `PercentChangeThreshold <= 0` → false
- `Services/AuthService.cs`'teki `ConfirmEmailAsync` için: token bulunamazsa,
  daha önce kullanılmışsa (`IsUsed`), süresi dolmuşsa (`ExpiresAt` geçmişse) doğru hata
  fırlattığını test et; `LoginAsync`'in `EmailConfirmed = false` olan kullanıcıyı
  reddettiğini test et
- (Vakit kalırsa) `Services/UserService.cs`'teki `SetAvatarAsync`'in 4MB üzeri base64
  string'i reddettiğini test et

**Kabul kriterleri**
- [ ] Yeni testler `dotnet test` ile yeşil geçiyor, var olan hiçbir testi kırmıyor
- [ ] Yüzde-değişim alarmı testleri en az 4 farklı senaryoyu (normal tetiklenme, eksik
      referans fiyat, sıfır/negatif referans fiyat, sıfır/negatif eşik) kapsıyor
- [ ] `NotificationService` cooldown testleri hem "cooldown içinde → gönderilmez" hem
      "cooldown dışında → gönderilir" durumlarını, ikisi için de sahte bir
      `IEmailSender` (mock/fake) kullanarak kapsıyor
- [ ] E-posta doğrulama testleri geçersiz/kullanılmış/süresi dolmuş token senaryolarının
      üçünü de ayrı ayrı kapsıyor

---

## Görev 52 — Dokümantasyon: README ve Görev Geçmişini Güncel Hale Getir

**Açıklama**
Kod, dokümantasyondan ilerde. `TASKS_HAFTA8.md` hâlâ taslak görünüyor ama 36-38 bitmiş;
Görev 40, 42, 43, 46, 48, 50 ise hiçbir TASKS dosyasında yok. Bu görevde dokümantasyon
kodun gerçek durumunu yansıtacak şekilde güncelleniyor — bundan sonraki bir yıl boyunca
"gerçekte ne yapıldı" sorusunun cevabı kodun içine gömülü kalmamalı.

**Yapılacaklar**
- `TASKS_HAFTA8.md`: dosya başındaki "Taslak — Yönetici Onayı Bekleniyor" notunu kaldır;
  Görev 36/37/38'in kabul kriteri kutucuklarını gerçek duruma göre işaretle; Görev 39'un
  bu hafta (HAFTA9) devam ettiğini not olarak ekle
- Kodda bulunan ama hiçbir TASKS dosyasında yer almayan Görev 40, 42, 43, 46, 48, 50'yi
  özetleyen kısa görev tanımları yaz — bunları ya bu dosyanın başına "Geçmiş, dokümante
  edilmemiş görevler" bölümü olarak ekle ya da ayrı bir `TASKS_ARSIV.md` aç (hangisini
  seçtiğini README'de belirt)
- `README.md` → "Özellikler" bölümüne eksik olanları ekle: coin karşılaştırma (Compare),
  profil fotoğrafı, yüzde-değişim alarmı tipi, e-posta doğrulama zorunluluğu
- `README.md` → "API Uçları" tablosuna eksik uçları ekle (gerçek route adlarını
  `AuthController.cs` / kullanıcı profil ucundan kontrol ederek — ör. e-posta doğrulama
  ucu)
- `README.md` → "Proje Yapısı" bölümündeki `pages/` listesine `Compare`, `ConfirmEmail`
  eksik, ekle
- `README.md` → "Görev Geçmişi" tablosuna bu `TASKS_HAFTA9.md` dosyasının satırını ekle

**Kabul kriterleri**
- [ ] `TASKS_HAFTA8.md` artık "taslak" görünmüyor, gerçek durumu yansıtıyor
- [ ] Kodda yorum olarak referansı bulunan her görev numarası (40, 42, 43, 46, 48, 50)
      en az bir TASKS dosyasında kısaca da olsa tanımlanmış durumda
- [ ] README "Özellikler" bölümü kodda var olan her sayfa/özellikle birebir eşleşiyor —
      README'yi okuyan biri uygulamada olmayan bir şey görmemeli, uygulamada olup
      README'de olmayan bir şey de kalmamalı
- [ ] README "API Uçları" tablosu güncel controller listesiyle eşleşiyor
- [ ] "Görev Geçmişi" tablosu bu dosyayı da içeriyor

---

## Genel Kurallar

- Bu hafta tek kişi yürütüldüğü için görevler arasında paralel ekip koordinasyonu
  gerekmiyor; önerilen sıra **39 → 51 → 52**, ama üçü de bağımsız.
- Görev 39 için migration eklemeden önce mevcut `Migrations/` klasöründe `TradingBot`
  tablosuna dokunan başka bir migration olmadığından emin ol.
- Görev 51'deki testler Görev 39'un EMA stratejisini beklemeden, mevcut kod üzerinde
  bağımsız olarak yazılabilir.
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli, `dotnet test` yeşil
  olmalı.
