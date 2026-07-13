# 4. Hafta Görevleri — Grafik Modülü (Faz 1)

Yönetici talebi: trend çizgisi, EMA, RSI, Bollinger indikatörleri, mum/OHLC seçenekli
bar gösterimi. Bu hafta bunun **temelini ve ilk sürümünü** kuruyoruz — "algoritma
çalıştırma" isteği kapsam netleşene kadar bu haftaya dahil değil (bkz. Genel Kurallar).

Kütüphane kararı: **`klinecharts`** (npm, MIT lisanslı, ücretsiz). Mum/OHLC bar seçimi,
MA/EMA/RSI/BOLL gibi indikatörler ve trend çizgisi çizim araçları built-in geliyor —
sıfırdan indikatör matematiği veya canvas çizim aracı yazmamıza gerek kalmıyor.
Herkes başlamadan önce kütüphanenin resmi dokümantasyonuna (klinecharts.com) göz atsın.

Sıra önemli: **Görev 4 önce bitmeli**, 12/13/14 onun üzerine inşa ediliyor. Görev 10 bağımsız.

---

## Görev 4 — Grafik Modülü Temeli (klinecharts entegrasyonu + Mum/OHLC seçici)

**Açıklama**
Coin detay sayfasına gerçek geçmiş veriyle çizilen, mum veya OHLC bar olarak
gösterilebilen bir grafik. Bu haftaki diğer indikatör/çizim görevlerinin temeli.

**Yapılacaklar**
- `npm install klinecharts` (frontend)
- `binanceService.js`'e `getKlines(symbol, interval, limit)` fonksiyonu ekle
  (Binance REST: `GET /api/v3/klines`), açık/yüksek/düşük/kapanış/hacim/zaman alanlarını
  `klinecharts`'ın beklediği formata çevir
- `CoinDetail.jsx`'te (veya yeni `ChartModule.jsx` bileşeninde) `klinecharts` ile grafiği başlat
- Zaman aralığı butonları: 1 saat / 1 gün / 1 hafta / 1 ay (`interval` parametresiyle yeniden veri çek)
- Bar gösterimi seçici: **Mum (candlestick) / OHLC bar** arasında geçiş yapılabilen toggle
- API hatasında (rate limit, ağ hatası vb.) kullanıcıya mesaj, sayfa çökmesin

**Kabul kriterleri**
- [ ] Grafik gerçek geçmiş veriyle çiziliyor
- [ ] Zaman aralığı değiştirilince grafik güncelleniyor
- [ ] Mum / OHLC bar geçişi çalışıyor
- [ ] API hatasında sayfa çökmüyor, hata mesajı gösteriliyor

---

## Görev 10 — Giriş / Kayıt Ekranlarının Görsel Yenilenmesi (UI/UX)

*(3. haftadan devreden görev, aynen geçerli — bkz. TASKS_HAFTA3.md Görev 10)*

**Açıklama**
`SignIn.jsx` ve `SignUp.jsx` şu an düz inline-style'lı formlar; `theme.css`
değişkenlerini kullanan modern bir görünüme kavuşturulacak.

**Yapılacaklar**
- Inline style'ları kaldırıp `theme.css`'teki CSS değişkenlerini kullanan ortalanmış
  bir kart tasarımı (logo/başlık, gölge, yuvarlatılmış köşeler)
- Şifre alanlarına göster/gizle (göz ikonu) toggle'ı ekle
- Alan bazlı hata mesajları (boş alan, eşleşmeyen şifre vb.) — tek satır genel hata yerine
- Buton loading state'ini görsel bir spinner ile güçlendir
- Açık/koyu tema uyumu (`ThemeToggle` zaten var)
- Mobil genişlikte düzgün görünüm

**Kabul kriterleri**
- [ ] Giriş ve kayıt sayfaları tutarlı bir kart tasarımına sahip
- [ ] Şifre göster/gizle toggle'ı çalışıyor
- [ ] Alan bazlı hata mesajları doğru gösteriliyor
- [ ] Açık ve koyu temada sayfa okunabilir/tutarlı
- [ ] Mobil genişlikte tasarım bozulmuyor

---

## Görev 12 — EMA ve RSI İndikatörleri

**Açıklama**
Görev 4'teki grafik modülünün üzerine, `klinecharts`'ın built-in EMA ve RSI
indikatörlerini kullanıcının açıp kapatabildiği bir katman olarak eklemek.

**Yapılacaklar**
- Grafiğin üstüne/altına küçük bir indikatör seçim menüsü (checkbox/dropdown): EMA aç/kapa, RSI aç/kapa
- `klinecharts` dokümantasyonundaki indikatör oluşturma API'siyle (`createIndicator` vb.) EMA'yı
  ana grafiğin üzerine overlay, RSI'ı ayrı bir alt panel olarak çizdir
- EMA periyodu (örn. 12/26) kullanıcı tarafından değiştirilebilsin (basit bir input/dropdown)
- İndikatör kapatıldığında grafikten tamamen kaldırılsın (bellek/performans sorunu olmasın)

**Kabul kriterleri**
- [ ] EMA açıldığında ana grafik üzerinde doğru çiziliyor
- [ ] RSI açıldığında ayrı panelde 0-100 aralığında doğru çiziliyor
- [ ] İndikatörler bağımsız olarak açılıp kapatılabiliyor
- [ ] Zaman aralığı değiştiğinde indikatörler de güncelleniyor

---

## Görev 13 — Bollinger Bands İndikatörü + İndikatör Paneli

**Açıklama**
Görev 12'deki indikatör menüsünü genişleterek Bollinger Bands eklemek ve
indikatör yönetimini tek bir düzenli panelde toplamak.

**Yapılacaklar**
- Görev 12'deki indikatör menüsüne Bollinger Bands (BOLL) ekle, `klinecharts`'ın
  built-in BOLL indikatörünü kullan
- İndikatör panelini tek bir bileşende topla (`IndicatorPanel.jsx` gibi): hangi
  indikatörlerin aktif olduğu görünsün, kolayca aç/kapa yapılabilsin
- Bollinger periyot/standart sapma parametrelerinin (varsayılan 20, 2) değiştirilebilmesi
- Panel mobilde de kullanılabilir olsun (aşağı açılır/kapanır vb.)

**Kabul kriterleri**
- [ ] Bollinger Bands doğru çiziliyor (üst/orta/alt bant)
- [ ] Parametreler değiştirilince bantlar güncelleniyor
- [ ] Aynı anda EMA + RSI + BOLL birlikte açık kalabiliyor, grafik performansı bozulmuyor
- [ ] Panel mobilde kullanılabilir

---

## Görev 14 — Trend Çizgisi Çizim Aracı

**Açıklama**
Kullanıcının grafik üzerine manuel trend çizgisi çizebilmesi — `klinecharts`'ın
built-in çizim (overlay) araçlarını kullanarak.

**Yapılacaklar**
- Grafiğe küçük bir araç çubuğu: "Trend Çizgisi Çiz" modu aç/kapa
- `klinecharts` dokümantasyonundaki overlay/çizim API'siyle (`createOverlay` vb.)
  kullanıcının iki nokta tıklayarak trend çizgisi çizebilmesini sağla
- Çizilen çizgiyi silme (tekli veya "tümünü temizle") özelliği
- Zaman aralığı değiştiğinde veya sayfadan çıkılıp geri dönüldüğünde çizgilerin davranışı
  net olsun (kalıcılık bu hafta zorunlu değil — en azından çökme/hata olmasın)

**Kabul kriterleri**
- [ ] Çizim modu açıkken grafik üzerine tıklayarak trend çizgisi çizilebiliyor
- [ ] Çizilen çizgi(ler) silinebiliyor
- [ ] Çizim modu kapatıldığında normal grafik etkileşimine (zoom/pan) dönülüyor
- [ ] Hiçbir adımda sayfa çökmüyor veya grafik bozulmuyor

---

## Görev 15 — Kalıcı (Persistent) Fiyat Alarmı / Dakikalık Sinyal Sistemi

**Açıklama**

Şu anki alarm mekanizması (3. hafta Görev 5-6'da yapıldı) **tek seferlik**: kullanıcı bir
hedef fiyat girer, o fiyata ulaşılınca **sadece kullanıcı o an sayfadaysa** bir toast
gösterilir ve alarm bir daha hiç tetiklenmez. Kodu kontrol ettim — `PriceAlert.cs`'te
`IsTriggered` diye bir alan var ama onu `true` yapan hiçbir servis metodu yok; yani
şu anda sunucu tarafında hiçbir "kontrol eden" mekanizma çalışmıyor, her şey tarayıcıda
açıkken oluyor. Kullanıcı uygulamayı kapatırsa alarm da fiilen çalışmıyor.

Yöneticimizin istediği: kullanıcı bir alarm kurduğunda, bu alarm **sunucu tarafında,
kullanıcı uygulamada olsun olmasın, düzenli aralıklarla (bu hafta: dakikada bir) Binance
verisine bakıp kontrol edilecek** ve koşul sağlandığı her seferinde (tek seferlik değil,
tekrar tekrar — "kalıcı sinyal") bir bildirim/kayıt üretecek. Bu hafta sadece "dakikalık"
periyot destekleniyor ama ileride "saatlik" ve "günlük" seçeneklerin de ekleneceği
söylendiği için, alt yapı bunlara kolayca genişleyecek şekilde tasarlanmalı.

**Yapılacaklar — Backend**

1. `Models/PriceAlert.cs`'e iki alan ekle:
   - `IsActive` (bool, varsayılan `true`) — kullanıcı alarmı durdurmak isterse burası `false`
     olur, arka plan servisi bu alarmı bir daha kontrol etmez (siline gerek kalmadan durdurma).
   - `Interval` (enum: `Minute = 0` bu hafta tek desteklenen değer; `Hourly = 1`, `Daily = 2`
     ileride eklenecek şekilde enum'a şimdiden yer açılsın, ama bu hafta sadece `Minute`
     için kod yazılacak — diğerleri "henüz desteklenmiyor" hatası dönebilir).
   - `IsTriggered` alanı kalabilir ama artık "hiç tetiklenmedi mi" anlamında değil, sinyal
     geçmişi ayrı bir tabloda tutulacağı için bu alanın anlamını ekip içinde netleştirin
     (örn. tamamen kaldırılabilir de).
2. Yeni model `Models/AlertSignal.cs`: `Id`, `AlertId`, `PriceAtTrigger`, `TriggeredAt`.
   Her tetiklenmede (dakikada bir koşul hâlâ sağlanıyorsa) buraya yeni bir satır eklenir —
   böylece kullanıcı geçmişte kaçırdığı sinyalleri de görebilir. Migration oluşturmayı unutma.
3. Arka planda periyodik çalışan bir servis yaz: `Services/AlertMonitorService.cs`,
   `BackgroundService`'ten türeyen bir `IHostedService`. İçeride `PeriodicTimer` ile
   her 60 saniyede bir:
   - `IsActive == true` olan tüm alarmları veritabanından çek
   - Alarmları **sembole göre grupla** (aynı coin'i izleyen 10 alarm varsa Binance'e 10 değil
     1 istek at — rate limit'e takılmamak için önemli)
   - Binance REST `/api/v3/ticker/price?symbols=[...]` ile güncel fiyatları toplu çek
   - Her alarmın `TargetPrice` + `Direction` koşulunu kontrol et, sağlanıyorsa yeni bir
     `AlertSignal` kaydı oluştur
4. `Program.cs`'e servisi kaydet: `builder.Services.AddHostedService<AlertMonitorService>();`
5. Yeni endpoint'ler (`AlertController.cs`): `GET /api/Alert/{id}/signals` (bir alarmın
   geçmiş tetiklenmeleri), `PATCH /api/Alert/{id}/toggle` (aktif/pasif yapma)

**Yapılacaklar — Frontend**

6. Alarm kurma formuna "Sıklık" seçici ekle — bu hafta sadece "Dakikalık" seçilebilir olsun,
   "Saatlik" ve "Günlük" seçenekleri görünsün ama devre dışı (disabled) olarak dursun; ileride
   backend hazır olunca aktif edilecek.
7. Alarm listesinde her alarm için: son ne zaman tetiklendiği, kaç kez tetiklendiği
   (`AlertSignal` geçmişinden), aktif/pasif durumu ve durdurma butonu gösterilsin.
8. Kullanıcı uygulama açıkken yeni sinyalleri görebilmesi için: sayfa belirli aralıklarla
   (örn. 30-60 saniyede bir) yeni sinyal var mı diye kontrol etsin (polling) ve varsa
   toast/tarayıcı bildirimi göstersin. (Not: uygulama kapalıyken bildirim almak push
   notification/e-posta gerektirir — bu, kapsam dışı, ayrı bir görev olarak ele alınmalı.)

**Kabul kriterleri**

- [ ] Alarm kurulduktan sonra kullanıcı sayfadan/uygulamadan çıksa bile, backend arka planda
      çalışmaya devam ediyor (veritabanında yeni `AlertSignal` kayıtları oluşarak doğrulanabilir)
- [ ] Fiyat koşulu geçerli olduğu sürece **her dakika** yeni bir sinyal kaydı oluşuyor,
      tek seferlik değil
- [ ] Kullanıcı uygulamayı tekrar açtığında kaçırdığı sinyalleri (geçmişi) görebiliyor
- [ ] Alarm pasif (`IsActive = false`) yapılırsa arka plan servisi o alarmı bir daha kontrol etmiyor
- [ ] Aynı sembolü izleyen birden fazla alarm varsa, Binance'e dakikada sembol başına
      **tek** istek atılıyor (alarm sayısı kadar değil)
- [ ] Backend yeniden başlatıldığında aktif alarmlar kaybolmuyor (veritabanından devam ediyor)
- [ ] "Saatlik"/"Günlük" seçenekleri arayüzde görünüyor ama net şekilde "yakında" / devre dışı

---

## Genel Kurallar

- Sıra: **Görev 4 önce** (12, 13, 14 buna bağımlı). Görev 10 ve Görev 15 bağımsız, paralel yürütülebilir.
- "Algoritma çalıştırma" bu haftaya dahil değil — kapsamı (backtesting mi, canlı sinyal mi,
  otomatik emir mi) yöneticiden netleşmeden görev tanımlanmayacak.
- `klinecharts` sürüm ve API detayları için ekip resmi dokümantasyona baksın, API isimleri
  sürüme göre değişebilir.
- Görev 15'i alacak kişi, `Services/AlertService.cs` ve `Controllers/AlertController.cs`'i
  değiştireceği için Görev 5-6'yı (mevcut alarm sistemi) yapan arkadaşla koordineli çalışsın.
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli.
