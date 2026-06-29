# 2. Hafta Görevleri — Crypto Tracker Frontend (Canlı Veri)

Bu hafta mock veriden gerçek Binance WebSocket akışına geçiyoruz.
Altyapı dosyaları önceden kuruldu — görevler bu altyapının üzerine UI inşa eder.

---

## Mevcut Altyapı (Dokunmayın)

Aşağıdaki dosyalar hazır, değiştirmeyin:

| Dosya | Ne yapar |
|---|---|
| `src/data/trackedSymbols.js` | `TRACKED_SYMBOLS` — takip edilen 20 Binance sembolü |
| `src/data/coinMeta.js` | `COIN_META` — sembol → isim/logo eşleştirme tablosu |
| `src/services/binanceService.js` | `buildStreamUrl()` — WebSocket URL üretici |
| `src/hooks/useBinancePrices.js` | `useBinancePrices()` — canlı fiyat hook'u |

### useBinancePrices Hook'u Nasıl Kullanılır?

```js
import { useBinancePrices } from '../hooks/useBinancePrices';

function BirBileşen() {
  const { prices, connectionStatus } = useBinancePrices();

  // prices objesi: { BTCUSDT: { symbol, currentPrice, priceChangePercentage24h, high24h, low24h }, ... }
  // connectionStatus: 'connecting' | 'connected' | 'disconnected'
  console.log(prices['BTCUSDT']?.currentPrice); // anlık BTC fiyatı
}
```

### COIN_META Nasıl Kullanılır?

```js
import { COIN_META } from '../data/coinMeta';

const meta = COIN_META['BTCUSDT'];
// { name: 'Bitcoin', symbol: 'BTC', image: 'https://...' }
```

---

## Görev 1 — Binance WebSocket Bağlantısını Doğrula & Test Et

**Açıklama**
`useBinancePrices` hook'unun gerçekten canlı veri getirdiğini doğrulama
ve bağlantı davranışını elle test etme.

**Yapılacaklar**
- `src/pages/Home/Home.jsx` sayfasında (veya geçici bir test bileşeninde)
  `useBinancePrices()` hook'unu çağır ve gelen `prices` objesini `console.log` ile yazdır
- Tarayıcı konsolunda en az 10 farklı coin için fiyatların değiştiğini gözlemle
- Şu durumları da test et:
  - Ağ bağlantısını kes (Wi-Fi kapat) → `connectionStatus`'ın `'disconnected'` olduğunu gör
  - Bağlantıyı geri aç → hook'un 3 saniye içinde otomatik yeniden bağlandığını gör
  - Sayfadan ayrıl ve DevTools → Network sekmesinde WebSocket bağlantısının kapandığını doğrula
- Test bitince `console.log`'ları temizle, test kodunu bileşenden çıkar

**Kabul kriterleri**
- [ ] Konsolda gerçek, saniyeler içinde değişen Binance fiyatları görünüyor
- [ ] En az 10 coin için eş zamanlı veri akıyor
- [ ] Bağlantı manuel kesilince `connectionStatus` `'disconnected'` oluyor
- [ ] Bağlantı yeniden sağlanınca hook otomatik reconnect yapıyor
- [ ] Sayfadan ayrılınca DevTools'ta WebSocket bağlantısı kapanıyor

---

## Görev 2 — Coin Kartı & Grid (Canlı Veriyle)

**Açıklama**
`COIN_META` (statik isim/logo) ile `useBinancePrices`'tan gelen canlı fiyatı
birleştirip kart ve grid olarak gösterme.

**Yapılacaklar**
- `src/components/CryptoCard/CryptoCard.jsx`:
  - Props: `meta` (COIN_META'dan bir giriş) + `priceData` (useBinancePrices'tan bir giriş)
  - Gösterilecekler: logo, isim, sembol, anlık fiyat (USD), 24s değişim (yeşil/kırmızı + ok ikonu)
  - Fiyat her değiştiğinde kısa bir flash animasyonu: yükselişte yeşil, düşüşte kırmızı
  - `priceData` henüz gelmediyse (undefined) iskelet/loading kart göster
- `src/components/CryptoGrid/CryptoGrid.jsx`:
  - `TRACKED_SYMBOLS` listesini gezip her sembol için `CryptoCard` oluştur
  - `useBinancePrices()` hook'unu burada çağırıp her karta ilgili `priceData`'yı ilet
  - Responsive grid: desktop 4 sütun, tablet 2, mobil 1
- Ana sayfada (`Home.jsx`) `CryptoGrid` bileşenini göster

**Kabul kriterleri**
- [ ] Kartlardaki fiyatlar sayfa yenilenmeden, saniyeler içinde canlı güncelleniyor
- [ ] Fiyat değiştiğinde görsel bir flash/animasyon tetikleniyor (yeşil ↑ / kırmızı ↓)
- [ ] Mobilde tek sütun, desktop'ta 4 sütun grid düzgün çalışıyor
- [ ] Veri bekleyen coin'ler iskelet halinde görünüyor, boş/hatalı değil

---

## Görev 3 — Arama & Sıralama (Canlı Veri Üzerinde)

**Açıklama**
Sürekli güncellenen coin listesi üzerinde anlık arama ve sıralama.

**Yapılacaklar**
- `CryptoGrid.jsx` içine (veya bir üst wrapper bileşenine) ekle:
  - **Arama kutusu**: `COIN_META`'daki `name` veya `symbol` üzerinde anlık filtreleme
  - **Sıralama dropdown'ı**: fiyata göre (↑↓), 24s değişime göre (↑↓) — en az 4 seçenek
- Sıralama, her fiyat güncellemesinde otomatik yeniden hesaplanmalı —
  `useMemo` ile optimize et (gereksiz yeniden hesaplama engellenir)
- Arama + sıralama aynı anda çalışabilmeli
- Arama sonucu boşsa tasarlanmış bir "sonuç bulunamadı" ekranı

**Kabul kriterleri**
- [ ] Arama kutusuna yazınca liste anlık filtreleniyor
- [ ] Sıralama dropdown'ı çalışıyor ve fiyat değiştikçe sıralama da güncelleniyor
- [ ] Hem arama hem sıralama aynı anda uygulanabiliyor
- [ ] Ekran takılma/donma olmuyor (gözle fark edilir gecikme yok)
- [ ] Boş sonuç durumunda tasarlanmış bir mesaj var

---

## Görev 4 — Coin Detay Sayfası (Canlı Fiyat ile)

**Açıklama**
Bir coin kartına tıklanınca o coin'in canlı fiyatını ve 24s istatistiklerini
gösteren detay sayfası.

**Yapılacaklar**
- `App.js`'e `/coin/:symbol` route'u ekle (örn. `/coin/BTCUSDT`)
- `CryptoCard.jsx`'e tıklama ile bu route'a yönlendirme ekle
- `src/pages/CoinDetail/CoinDetail.jsx`:
  - `useParams()` ile `symbol`'ü al
  - `COIN_META[symbol]` yoksa "bulunamadı" ekranı göster, sayfa çökmesin
  - Gösterilecekler: büyük logo, isim, anlık fiyat, 24s değişim, 24s yüksek (`high24h`),
    24s düşük (`low24h`)
  - Canlı fiyat için `useBinancePrices()` doğrudan bu sayfada kullanılabilir;
    **daha iyi çözüm:** fiyat verisini Context ile paylaş (WebSocket bağlantısı çoğalmaz)
- Listeye geri dönüş butonu

**Kabul kriterleri**
- [ ] Karta tıklanınca doğru coin'in detay sayfası açılıyor
- [ ] Detay sayfasındaki fiyat canlı güncelleniyor
- [ ] 24s yüksek ve düşük değerleri gösteriliyor
- [ ] Geçersiz bir sembolle gidilirse (`/coin/yokbukoin`) hata ekranı var, sayfa çökmüyor
- [ ] Sayfalar arası geçişte DevTools'ta WebSocket bağlantı sayısı kontrolsüz artmıyor

---

## Görev 5 — Açık/Koyu Tema & Bağlantı Durumu Göstergesi

**Açıklama**
Kullanıcının tema tercihini yönetmesi ve Binance bağlantı durumunun görünür olması.

**Yapılacaklar**
- **Tema sistemi:**
  - Header'da güneş/ay ikonu ile tema değiştirme butonu
  - CSS değişkenleri (`--bg-primary`, `--text-primary`, `--card-bg` vb.) veya Context API ile
    iki ayrı tema seti — renkleri tersine çevirmek değil, her tema kendi içinde tutarlı olmalı
  - Seçilen tema `localStorage`'da saklanmalı, sayfa yenilenince korunmalı
  - Tüm sayfalarda (liste, detay, header) tema tutarlı uygulanmalı
- **`src/components/ConnectionStatus/ConnectionStatus.jsx`:**
  - `useBinancePrices()`'tan gelen `connectionStatus`'u görsel olarak sun
  - `'connected'` → yeşil nokta + "Canlı"
  - `'connecting'` → sarı/turuncu dönen ikon + "Bağlanıyor"
  - `'disconnected'` → kırmızı uyarı + "Bağlantı kesildi"
  - Bağlantı koptuğunda kartların üzerine hafif bir "canlı değil" overlay/rozeti

**Kabul kriterleri**
- [ ] Tema değiştirme çalışıyor, geçiş anlık oluyor
- [ ] Açık tema görsel olarak tutarlı ve okunabilir (sadece ters çevrilmiş koyu değil)
- [ ] Sayfa yenilenince tercih korunuyor
- [ ] Bağlantı durumu göstergesi gerçek WebSocket durumunu yansıtıyor
- [ ] Bağlantı koptuğunda kullanıcı bunu fark edebiliyor

---

## Genel Kurallar

- `src/data/trackedSymbols.js`, `src/data/coinMeta.js`, `src/services/binanceService.js`,
  `src/hooks/useBinancePrices.js` — bu 4 dosyaya dokunmayın.
- Görev sırasına uymak önerilir: **Görev 1 → 2 → 3** (sıralı bağımlılık), **4 ve 5 paralel**.
- Push yapmadan önce `npm run build`'in hatasız geçtiğini kontrol edin.
- WebSocket bağlantısını gereksiz çoğaltmamak için `useBinancePrices()`'ı mümkün olduğunca
  üst seviye bir bileşende çağırıp aşağıya prop veya Context ile taşıyın.
- Takıldığınızda önce yukarıdaki "Mevcut Altyapı" bölümündeki hook kullanım örneklerine bakın.
