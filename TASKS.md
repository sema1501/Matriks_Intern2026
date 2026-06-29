# 1. Hafta Görevleri — Crypto Tracker Frontend

Bu hafta hiçbir gerçek API'ye bağlanmıyoruz. Tüm veriler `src/data/mockData.js`
dosyasından geliyor. Amaç: görsel/UI katmanını sağlam kurmak. API entegrasyonu
ileride ayrı bir görev olarak gelecek.

---

## mockData.js Nasıl Kullanılır?

Dosya `src/data/mockData.js` altında ve `mockCoins` adında bir dizi export ediyor.
30 coin içeriyor, her biri şu alanlara sahip:

```js
{
  id: "bitcoin",                  // benzersiz kimlik -> /coin/:id route'unda kullanılır
  rank: 1,                        // market cap sıralaması
  name: "Bitcoin",
  symbol: "BTC",
  image: "https://...png",        // gerçek logo görseli (CoinGecko CDN, statik link)
  currentPrice: 66532.14,         // USD fiyatı
  priceChangePercentage24h: -2.99,// son 24s yüzde değişim (+ veya -)
  marketCap: 1292433808489.2,
  volume24h: 109667322195.82,
  high24h: 68164.86,
  low24h: 65820.07,
  circulatingSupply: 19432156.0,
  sparkline7d: [66532.14, 63003.27, ...] // 7 günlük SAHTE fiyat serisi (sadece grafik için)
}
```

**Projeye nasıl import edilir:**

```js
import mockCoins from '../data/mockData';

function CryptoList() {
  return (
    <div>
      {mockCoins.map(coin => (
        <CryptoCard key={coin.id} coin={coin} />
      ))}
    </div>
  );
}
```

**Önemli noktalar:**
- `sparkline7d` gerçek fiyat geçmişi değildir, sadece görsel doldurma amaçlıdır. Görev 4'te grafik çizerken bunu kullanın.
- `id` alanı route'larda kullanılacak (`/coin/bitcoin` gibi), `useParams()` ile `id`'yi alıp `mockCoins.find(c => c.id === id)` şeklinde coin'i bulabilirsiniz.
- Hiçbir görevde `fetch`, `axios` veya başka bir HTTP isteği YOK. Her şey bu diziden okunuyor.

---

## Görev 1 — Mock Veri & Coin Kartı Bileşeni

**Açıklama**
`mockData.js`'teki veriyi kullanarak tek bir coin'i gösteren kart bileşeninin oluşturulması.

**Yapılacaklar**
- `CryptoCard.jsx`: `coin` prop'u alan, tek bir coin'i kart olarak gösteren bileşen
  - Logo (`coin.image`), isim, sembol, fiyat, 24s değişim (yeşil/kırmızı renk + ok ikonu)
- `priceChangePercentage24h` pozitifse yeşil + yukarı ok, negatifse kırmızı + aşağı ok
- Kart hover efekti (hafif büyüme veya kenarlık rengi değişimi)
- Test için `mockCoins[0]` (Bitcoin) ile kartı tek başına render edip görsel kontrolü yapın

**Kabul kriterleri**
- [ ] Kart, `mockData.js`'ten gelen bir coin objesini prop olarak alıp doğru gösteriyor
- [ ] Pozitif değişim yeşil, negatif değişim kırmızı renkte
- [ ] Logo görseli (CoinGecko CDN linki) düzgün yükleniyor
- [ ] Hover'da görsel bir tepki var

---

## Görev 2 — Grid Layout & Responsive Tasarım

**Açıklama**
`mockCoins` dizisinin tamamının kart olarak, düzenli bir grid içinde listelenmesi.

**Yapılacaklar**
- `CryptoGrid.jsx`: `mockCoins` dizisini `.map()` ile gezip her biri için `CryptoCard` render et
- Responsive breakpoint'ler: desktop 4 sütun, tablet 2 sütun, mobil 1 sütun
- Sayfa genel layout'u: header, ana içerik alanı, footer
- Koyu/gradient arkaplan ve glassmorphism (yarı şeffaf, blur'lu kart) tasarım dili kur — bu tasarım dili sonraki görevlerde referans olacak

**Kabul kriterleri**
- [ ] `mockCoins` dizisindeki 30 coin'in tamamı grid içinde görünüyor
- [ ] Tarayıcı küçültülünce sütun sayısı düzgün azalıyor, kartlar taşmıyor/bozulmuyor
- [ ] Header ve footer tüm sayfalarda tutarlı
- [ ] Görsel tasarım koyu/modern bir his veriyor

---

## Görev 3 — Arama & Sıralama (İstemci Tarafı)

**Açıklama**
`mockCoins` dizisi üzerinde anlık arama ve sıralama özelliklerinin eklenmesi.

**Yapılacaklar**
- Arama kutusu: `coin.name` veya `coin.symbol`'a göre anlık filtreleme — `mockCoins.filter(...)` ile, herhangi bir dış istek yok
- Sıralama dropdown'ı: `currentPrice`, `priceChangePercentage24h`, `marketCap` alanlarına göre artan/azalan sıralama (`.sort()`)
- Arama sonucu boşsa "sonuç bulunamadı" şeklinde tasarlanmış bir boş durum ekranı

**Kabul kriterleri**
- [ ] Arama kutusuna yazınca liste anlık filtreleniyor
- [ ] Sıralama dropdown'ı en az 2 kritere göre çalışıyor (örn. fiyat ve değişim)
- [ ] Boş arama sonucunda kırık bir görünüm yok, tasarlanmış bir mesaj var

---

## Görev 4 — Coin Detay Sayfası

**Açıklama**
Bir coin kartına tıklandığında, o coin'in `mockData.js`'teki tüm bilgilerini gösteren bir detay sayfası.

**Yapılacaklar**
- React Router ile `/coin/:id` route'u kur
- `CoinDetail.jsx`: `useParams()` ile `id`'yi al, `mockCoins.find(c => c.id === id)` ile coin'i bul
  - Büyük logo, isim, fiyat, `marketCap`, `volume24h`, `high24h`, `low24h`, `circulatingSupply` göster
- `sparkline7d` dizisini kullanarak basit bir çizgi grafik çiz (örn. `recharts` ile) — bu veri sahte olsa da görsel olarak tamamlanmış bir grafik oluşturmalı
- Listeye geri dönüş butonu

**Kabul kriterleri**
- [ ] Bir coin kartına tıklanınca ilgili coin'in detay sayfası açılıyor
- [ ] Detay sayfasında liste görünümünden daha fazla bilgi var (high/low, supply, volume)
- [ ] `sparkline7d` verisiyle bir grafik görünüyor
- [ ] Detaydan listeye geri dönülebiliyor
- [ ] `mockCoins` içinde olmayan bir `id` ile gidilirse (örn. `/coin/yokbukoin`) hata mesajı gösteriliyor, sayfa çökmüyor

---

## Görev 5 — Açık/Koyu Tema Sistemi

**Açıklama**
Kullanıcının açık ve koyu tema arasında geçiş yapabilmesi, tercihin korunması.

**Yapılacaklar**
- Tema değiştirme butonu (header'da, güneş/ay ikonu ile)
- CSS değişkenleri (CSS variables) veya Context API ile iki tema setini tanımla — renkler, arkaplan, kart rengi, yazı rengi her iki tema için ayrı ayrı belirlenmeli
- Açık temada da glassmorphism/modern hissin korunması (sadece renkleri tersine çevirmek yetmez, açık tema kendi başına tutarlı ve göz yormayan olmalı)
- Seçilen tema `localStorage`'da saklanmalı, sayfa yenilenince korunmalı
- Tüm sayfalarda (liste, detay, header, footer) tema tutarlı şekilde uygulanmalı

**Kabul kriterleri**
- [ ] Tema değiştirme butonu çalışıyor, anlık geçiş oluyor
- [ ] Açık tema okunabilir ve görsel olarak tutarlı (sadece ters renk değil, tasarlanmış bir tema)
- [ ] Sayfa yenilenince seçilen tema korunuyor
- [ ] Detay sayfası dahil tüm ekranlarda tema doğru uygulanıyor

---

## Genel Kurallar

- Hiçbir görevde gerçek bir API çağrısı (`fetch`, `axios`, vb.) yapılmıyor. Tüm veri `src/data/mockData.js`'ten geliyor.
- Branch kullanmadan sırayla `main`'e push edeceğiz. Push yapmadan önce **mutlaka `git pull` çekin.**
- Önerilen sıra: Görev 1 → Görev 2 (Görev 2, Görev 1'deki kart bileşenini kullanıyor) → Görev 3 ve 4 paralel → Görev 5 en son (diğer tüm bileşenler tamamlandıktan sonra tema değişkenlerini uygulamak daha kolay).
- Takıldığında önce `mockData.js`'teki alan adlarını tekrar kontrol et, sonra sor.
