# 5. Hafta Görevleri — Sanal Portföy / Mock Alım-Satım Modülü

Bu hafta önceliğimiz: **yeni bir roobit-tarzı özellik** — kullanıcıların gerçek para
kullanmadan coin alıp satabildiği bir sanal portföy modülü. Bu, projenin şimdiye kadarki
en somut "exchange deneyimi" özelliği olacak.

"Algoritma çalıştırma" isteği hâlâ netleşmedi — bu haftaya dahil değil, kapsamı
netleşmeden görev tanımlanmayacak (bkz. TASKS_HAFTA4.md Genel Kurallar).

Sıra önemli: **16 → 17 → (18, 19 paralel) → 20**. Görev 16 ve 17 bitmeden diğerleri başlayamaz.

---

## Görev 16 — Sanal Bakiye & Portföy Veri Modeli (Backend)

**Açıklama**
Kullanıcının gerçek para kullanmadan coin alıp satabilmesi için temel veri altyapısı.

**Yapılacaklar**
- `Models/User.cs`'e `VirtualBalance` (decimal) alanı ekle, varsayılan **10.000 USD**
- `Models/PortfolioHolding.cs`: `UserId`, `Symbol`, `Quantity`, `AvgBuyPrice`
- `Models/Transaction.cs`: `UserId`, `Symbol`, `Type` (enum: `Buy`/`Sell`), `Quantity`, `Price`, `CreatedAt`
- Migration oluştur; `Data/DataSeeder.cs`'teki varsayılan test kullanıcısına (admin) da
  varsayılan bakiye uygulanmalı

**Kabul kriterleri**
- [ ] Yeni kayıt olan her kullanıcı otomatik 10.000 USD sanal bakiye ile başlıyor
- [ ] `PortfolioHolding` ve `Transaction` tabloları migration ile oluşuyor
- [ ] Var olan admin test kullanıcısı da bakiyeye sahip oluyor

---

## Görev 17 — Alım / Satım İşlem Servisi (Backend)

**Açıklama**
Görev 16'daki modelin üzerine gerçek alım/satım mantığı ve işlem geçmişi.

**Yapılacaklar**
- `IPortfolioService` / `PortfolioService.cs`: `BuyAsync`, `SellAsync`, `GetHoldingsAsync`,
  `GetTransactionHistoryAsync`, `GetBalanceAsync`
- **Alım:** bakiye yeterli mi kontrol et (yetersizse anlamlı hata), bakiyeden düş, holding'i
  ekle/güncelle (birden fazla alımda **ağırlıklı ortalama** maliyet hesabı), `Transaction` kaydı oluştur
- **Satım:** yeterli miktar var mı kontrol et, holding'den düş (miktar 0'a inerse kaydı sil),
  bakiyeye ekle, `Transaction` kaydı oluştur
- `PortfolioController.cs`: `GET /api/Portfolio/balance`, `GET /api/Portfolio/holdings`,
  `GET /api/Portfolio/transactions`, `POST /api/Portfolio/buy`, `POST /api/Portfolio/sell`
  — hepsi `[Authorize]`

**Kabul kriterleri**
- [ ] Yetersiz bakiyeyle alım yapılamıyor
- [ ] Sahip olunmayan/yetersiz miktarda coin satılamıyor
- [ ] Birden fazla alımda ortalama alış fiyatı doğru hesaplanıyor
- [ ] İşlem geçmişi doğru sırayla (en yeni üstte) dönüyor

---

## Görev 18 — Portföy Sayfası (Frontend)

**Açıklama**
Kullanıcının bakiyesini, sahip olduğu coinleri ve işlem geçmişini görebileceği sayfa.

**Yapılacaklar**
- `apiService.js`'e `getBalance`, `getHoldings`, `getTransactions` ekle
- Yeni sayfa `src/pages/Portfolio/Portfolio.jsx` (`/portfolio` route, `PrivateRoute` ile korunuyor)
- Bakiye + holdings tablosu: miktar, ortalama alış fiyatı, güncel fiyat (`useGlobalPrices`'tan),
  güncel değer, kâr/zarar % — canlı fiyatla anlık güncellenmeli
- İşlem geçmişi tablosu (tarih, tür, miktar, fiyat)
- `Navbar.jsx`'e giriş yapılmışsa "Portföyüm" linki ekle

**Kabul kriterleri**
- [ ] Toplam değer ve kâr/zarar canlı fiyatla güncelleniyor
- [ ] İşlem geçmişi doğru gösteriliyor
- [ ] `/portfolio` giriş yapılmadan açılırsa `/signin`'e yönleniyor

---

## Görev 19 — Coin Detay Sayfasında Al / Sat Formu (Frontend)

**Açıklama**
Kullanıcının coin detay sayfasından doğrudan mock alım-satım yapabilmesi.

**Yapılacaklar**
- `apiService.js`'e `buyCoin`, `sellCoin` ekle
- `CoinDetail.jsx`'e "Al / Sat" formu: miktar gir, mevcut bakiyeyi/holding miktarını göster,
  güncel canlı fiyattan (`useGlobalPrices`) işlemi gerçekleştir
- Hata durumlarında (yetersiz bakiye/coin, geçersiz/negatif miktar) net mesaj göster
- İşlem başarılı olunca toast ile onay ver, bakiye/holding anında (sayfa yenilenmeden) güncellensin

**Kabul kriterleri**
- [ ] Al/Sat formu çalışıyor, hata durumlarında anlamlı mesaj gösteriyor
- [ ] İşlem sonrası bakiye/holding arayüzde anında güncelleniyor
- [ ] Giriş yapmamış kullanıcı formu kullanmaya çalışınca `/signin`'e yönlendiriliyor

---

## Görev 20 — Liderlik Tablosu (Leaderboard)

**Açıklama**
roobit tarzı rekabetçi bir dokunuş: en çok "kâr" eden kullanıcıların görülebildiği bir sıralama.

**Yapılacaklar**
- Backend: portföy değerini (bakiye + holdings'in güncel piyasa değeri) hesaplayıp kullanıcıları
  kâr/zarar yüzdesine göre sıralayan `GET /api/Portfolio/leaderboard` endpoint'i — sadece
  **kullanıcı adı + kâr/zarar yüzdesi** döndürülmeli, e-posta/şifre gibi hassas bilgi asla dönmemeli
- Frontend: `Home.jsx`'e veya yeni bir sayfaya küçük bir "Liderlik Tablosu" widget'ı, ilk 10 kullanıcı

**Kabul kriterleri**
- [ ] Liderlik tablosu en yüksek kârdan düşüğe doğru sıralı geliyor
- [ ] Hassas kullanıcı bilgisi (e-posta, şifre vb.) hiçbir şekilde sızmıyor
- [ ] Veri makul sıklıkla (örn. sayfa açılışında/yenilenmesinde) güncel geliyor

---

## Genel Kurallar

- Sıra: **16 → 17** zorunlu (veri modeli olmadan servis yazılamaz). 17 bitince **18 ve 19
  paralel** yürütülebilir. **20**, 17'deki hesaplamaya dayandığı için ondan sonra gelir.
- Bu tamamen simülasyon/oyunlaştırma amaçlı bir modül — gerçek para, gerçek işlem veya
  gerçek borsa bağlantısı yok, hiçbir yerde gerçek ödeme bilgisi istenmemeli.
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli.