# 10. Hafta Görevleri — CI/CD, Güvenlik Sertleştirme, İşlem Geçmişi Dışa Aktarma

**Bağlam:** `TASKS_HAFTA9.md`'deki üç görev (EMA kesişimi stratejisi, test kapsamı,
dokümantasyon) PR #84–#86 ile `main`'e taşındı ve tamamlandı — kod artık README/TASKS
dosyalarıyla senkron (bkz. `TASKS_HAFTA8.md`'nin güncellenmiş durum notu ve
`TASKS_ARSIV.md`). Bu hafta borç kapatma değil; projeyi ileri taşıyan, kod tabanını
incelerken somut olarak eksik bulunan 3 iyileştirme.

> **Numaralandırma notu:** Bu dosyada görevlere kasıtlı olarak "Görev NN" numarası
> vermiyorum. `TASKS_ARSIV.md`'nin kendisi, kod-içi "Görev 51" (işlem geçmişi sayfalama)
> ile `TASKS_HAFTA9.md`'deki "Görev 51"in (test kapsamı) çakıştığını belgeliyor — üç ayrı
> numaralandırma sistemi (haftalık dosya numarası, kod içi `// Görev NN` yorumu, GitHub
> issue/PR numarası: #81-86) aynı anda kullanılıyor. Son üç görev zaten GitHub Issue
> olarak açılıp (#81, #82, #83) oradan numara aldı — bunun devamı en güvenli yol: bu
> dosyadaki 3 görev için de birer GitHub Issue aç, PR'ları o issue numarasına bağla,
> `// Görev NN` yorum biçimini bırak.

---

## 1. CI/CD: Her PR'da Otomatik Build & Test

**Açıklama**
`.github/` altında sadece `ISSUE_TEMPLATE/gorev.md` ve `pull_request_template.md` var —
hiç workflow yok. "Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli" kuralı
her `TASKS_HAFTA*.md` dosyasının sonunda yazılı ama hiçbir otomasyon bunu doğrulamıyor;
tamamen kişinin hatırlamasına bağlı. Proje artık `backend/tests/CryptoTracker.API.Tests`
altında güçlü bir test paketine sahip (RSI, EMA, backtest, bot, alarm, audit log,
bildirim, e-posta doğrulama) ama bu testler yalnızca elle çalıştırılıyor.

**Yapılacaklar**
- `.github/workflows/backend-ci.yml`: `push`/`pull_request` (main, develop) üzerinde
  `dotnet restore`, `dotnet build --configuration Release`, `dotnet test` (`backend/`
  kökünden, `CryptoTracker.sln` ile) çalıştırsın
- `.github/workflows/frontend-ci.yml`: `npm ci`, `npm run build` (`frontend/` altında)
  çalıştırsın
- Testler bir SQL Server'a ihtiyaç duyuyorsa (bazı entegrasyon testleri `AppDbContext`
  kullanıyor olabilir — kontrol et) workflow'a bir `mssql` servis konteyneri ekle; sadece
  in-memory provider kullanan testler için buna gerek yoksa atla
- README'ye kısa bir not/rozet ekle: hangi workflow'un neyi doğruladığı
- (Kod dışı, Sema'nın yapacağı) repo ayarlarından branch protection: bu workflow'lar
  yeşil olmadan `main`/`develop`'a merge engellensin — stajyer bunu önerebilir ama
  ayarı değiştiremez

**Kabul kriterleri**
- [ ] Yeni açılan her PR'da hem backend hem frontend workflow'u otomatik tetikleniyor
- [ ] Kasıtlı olarak bozulmuş bir test veya derleme hatası içeren bir deneme PR'ında
      workflow kırmızı oluyor ve bu PR'da görünür şekilde işaretleniyor
- [ ] Var olan tüm testler CI üzerinde yeşil geçiyor (yerel `dotnet test` sonucuyla tutarlı)
- [ ] `npm run build` CI'da da yerelle aynı sonucu veriyor

---

## 2. Kimlik Doğrulama Uçlarına Rate Limiting

**Açıklama**
`Program.cs`'te hiçbir rate limiting/throttling yok (kod incelendi, doğrulandı).
`POST /api/Auth/login`, `/register`, `/forgot-password`, `/confirm-email` sınırsız
çağrılabiliyor. Bu artık teorik bir risk değil: `forgot-password` gerçek SMTP ile
e-posta gönderiyor (bkz. `AuthService.ForgotPasswordAsync`) — sınırsız çağrıldığında
herhangi bir e-posta adresi spam'lenebilir; `login` sınırsız deneme ile brute-force'a
açık; `register` sınırsız sahte hesap oluşturmaya (ve her birine doğrulama e-postası
göndermeye) açık.

**Yapılacaklar**
- .NET 9'un yerleşik `Microsoft.AspNetCore.RateLimiting` middleware'ini ekle (yeni
  NuGet paketi gerekmiyor, `Microsoft.AspNetCore.App` içinde geliyor)
- İki politika: kimlik doğrulama uçları için sıkı bir sabit pencere/token bucket
  (örn. IP başına dakikada 5 istek) ve geri kalan uçlar için daha gevşek/limitsiz
- `AuthController`'daki `Register`, `Login`, `ForgotPassword`, `ConfirmEmail`
  action'larına ilgili politikayı uygula
- Limit aşıldığında 429 dönmeli, gövdede anlamlı bir mesaj olmalı
- Frontend: `SignIn.jsx`, `SignUp.jsx`, `ForgotPassword.jsx` — 429 yanıtını "çok fazla
  deneme, birazdan tekrar dene" gibi kullanıcı dostu bir mesajla göstersin (şu an
  muhtemelen genel hata mesajına düşüyor)

**Kabul kriterleri**
- [ ] Aynı IP'den kısa sürede çok sayıda login denemesi 429 ile reddediliyor, doğru
      şifreyle bile olsa limit aşılınca geçici olarak engelleniyor
- [ ] `forgot-password` kısa sürede tekrar tekrar çağrıldığında e-posta gönderimi
      429 ile durduruluyor (gerçek e-posta gönderilmiyor)
- [ ] Normal kullanım (tek seferlik login/register/şifre sıfırlama) hiç etkilenmiyor
- [ ] Limit aşımı frontend'de anlaşılır bir mesajla gösteriliyor, sayfa çökmüyor

---

## 3. İşlem Geçmişini CSV Olarak Dışa Aktarma

**Açıklama**
`GET /api/Portfolio/transactions` (`PortfolioService.GetTransactionHistoryAsync`)
sayfalı ve `symbol`/`type` filtreli hale geldi. Kullanıcılar işlem geçmişini kendi
kayıtları/vergi takibi için dışa aktaramıyor — mevcut filtreleme altyapısının doğal bir
sonraki adımı.

**Yapılacaklar**
- Backend: `GET /api/Portfolio/transactions/export` — `GetTransactions` ile aynı
  `symbol`/`type` filtrelerini kabul etsin, sayfalama olmadan (makul bir üst sınırla,
  örn. 5000 satır) tüm sonucu CSV olarak döndürsün (`Content-Type: text/csv`,
  `Content-Disposition: attachment; filename=islemler.csv`)
- CSV'ye UTF-8 BOM ekle — Excel'de Türkçe karakterler (sembol adları, işlem tipi)
  bozuk görünmesin
- Frontend: `Portfolio.jsx`'teki işlem geçmişi tablosunun üstüne "CSV indir" butonu;
  o an ekranda seçili olan filtreleri export isteğine aynen yansıt

**Kabul kriterleri**
- [ ] İndirilen CSV, ekrandaki (filtrelenmiş) tabloyla birebir aynı satırları içeriyor
- [ ] Excel/Google Sheets'te açıldığında Türkçe karakterler doğru görünüyor
- [ ] 3000+ işlemi olan bir kullanıcıda istek zaman aşımına uğramıyor makul sürede dönüyor
- [ ] Başka bir kullanıcının işlem geçmişi hiçbir şekilde export edilemiyor (mevcut
      `[Authorize]` + `userId` filtresi export ucunda da aynen uygulanıyor)

---

## Genel Kurallar

- Üç görev birbirinden bağımsız, istenilen sırada yapılabilir. Önerilen sıra: önce **1
  (CI)** — geri kalan ikisinin doğruluğunu otomatik doğrulayacak altyapıyı en başta kurar.
- Görev 2 için: rate limit testleri `dotnet test` içinde `TestServer` ile yazılabilir,
  gerçek zaman beklemeden (birden fazla ardışık istek atıp N+1.'in 429 döndüğünü kontrol
  etmek yeterli, `IClock` mock'lamaya gerek yok).
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli, `dotnet test` yeşil
  olmalı — Görev 1 tamamlanınca bu zaten otomatik doğrulanacak.

---

## Ayrıca fark edilen, bu haftanın kapsamına alınmayan bir konu

Bu inceleme sırasında yerel checkout'ta 206 dosyanın satır sonu (CRLF/LF) farkı yüzünden
"değişmiş" göründüğü tespit edildi (`git diff --stat` → 45900 ekleme/45900 silme, içerik
aynı). Ayrıca `.git/index.lock` dosyası duruyordu — muhtemelen açık bir Visual
Studio/VS Code örneği yüzünden — bu da `git reset`/`pull` gibi komutları engelliyor.
Bunlar bu haftanın görevi değil ama önerim: açık IDE/git araçlarını kapatıp
`.git/index.lock` dosyasını sil, `git pull` ile `origin/main`'e güncel geç, ve satır
sonu sorununu kalıcı çözmek için köke bir `.gitattributes` (`* text=auto eol=lf` gibi)
ekle — yoksa her yeni ortamda bu 206 dosyalık sahte diff tekrar çıkabilir.
