# 8. Hafta Görevleri — Taslak (Yönetici Onayı Bekleniyor)

**Bu bir taslak.** Yönetici bu hafta için henüz bir şey söylemedi — burada duran 5 görev
ekibin kendi önceliğine göre hazırlandı, kenarda bekliyor. Yönetici farklı bir yön
belirtirse bu dosya güncellenecek/değişecek.

Kaynak: 7. haftanın ilk taslağında (yönetici sonradan farklı yön verdiği için rafa
kalkan) "admin gözetimi" fikri vardı, o notta "ileride ayrı bir hafta olarak tekrar ele
alınabilir" denmişti — bu hafta onu güncelleyip geri getiriyoruz. Ayrıca uzun süredir
ertelenen iki konuyu da (EMA stratejisi, uygulama kapalıyken e-posta bildirimi) ekledik.
(Not: alarm dakikalık/saatlik/günlük olarak zaten kurulabiliyormuş, bunu ayrı görev
yapmaya gerek yok — düzelttim.)

Sıra: **36 → 37 → 38 sıralı** (panel → müdahale → loglama). **39 ve 40 bağımsız**, paralel yürütülebilir.

---

## Görev 36 — Admin: Bot & Portföy Gözetim Paneli

**Açıklama**
Adminlerin tüm kullanıcıların bot ve portföy (defter) aktivitesini görebileceği bir ekran.
7. haftadan beri botlar gerçek Testnet emri gönderdiği için bu görünürlük daha da önemli hale geldi.

**Yapılacaklar**
- Backend: `GET /api/Admin/bots` (tüm kullanıcıların botları — kullanıcı adı, sembol,
  strateji, aktif durumu), `GET /api/Admin/portfolios` (tüm kullanıcıların bakiye/holding
  defter özeti — Görev 32'den beri bunlar Testnet emirlerinin sonucu) — `[Authorize(Roles = "Admin,SuperAdmin")]`
- Frontend: yeni bir `/admin/bots` sayfasına bu verileri gösteren aranabilir/filtrelenebilir bir tablo

**Kabul kriterleri**
- [ ] Sadece Admin/SuperAdmin bu endpoint'lere ve sayfaya erişebiliyor
- [ ] Tüm kullanıcıların botları ve portföy özetleri doğru listeleniyor
- [ ] Tabloda arama/filtreleme çalışıyor

---

## Görev 37 — Admin: Botu Zorla Durdurma (Kill Switch) + Aşırı İşlem Tespiti

**Açıklama**
Görev 36'daki panelin üzerine, adminin şüpheli bir botu durdurabilmesi. Botlar artık
gerçek Testnet emri gönderdiği için (rate limit / gereksiz spam riski var) bu önceki
haftalara göre daha kritik bir güvenlik önlemi.

**Yapılacaklar**
- Backend: `PATCH /api/Admin/Bot/{id}/force-stop` — botu `IsActive = false` yapar (böylece
  `BotMonitorService` bir sonraki taramada bu botu hiç görmez, Testnet'e emir gitmez),
  isteğe bağlı bir `AdminNote` (durdurma sebebi) alanı eklenir
- Basit bir kötüye kullanım kuralı: bir bot belirli bir sürede (örn. son 1 saatte) belirli
  sayıdan (örn. 20) fazla emir gönderdiyse otomatik olarak "şüpheli" işaretlensin (`IsFlagged`)
- Frontend: Görev 36'daki panelde "Durdur" butonu + sebep girme alanı; şüpheli botlar görsel olarak ayırt edilsin

**Kabul kriterleri**
- [ ] Admin bir botu durdurabiliyor, kullanıcı bunu kendi bot sayfasında görebiliyor
- [ ] Durdurulan bottan sonra gerçekten Testnet'e yeni emir gitmiyor (doğrulanmalı)
- [ ] Aşırı emir gönderen bot otomatik olarak şüpheli işaretleniyor
- [ ] Şüpheli botlar admin panelinde belirgin şekilde görünüyor

---

## Görev 38 — Admin: İşlem / Denetim Günlüğü (Audit Log)

**Açıklama**
Hangi adminin ne zaman hangi botu durdurduğu gibi önemli olayların izlenebildiği bir kayıt.

**Yapılacaklar**
- `Models/AuditLog.cs`: `ActorUserId`, `Action` (örn. `BotForceStopped`, `BotFlagged`),
  `TargetId`, `Details`, `CreatedAt`, migration
- Görev 37'deki bot durdurma/işaretleme olaylarında bu tabloya kayıt düşülsün
- `GET /api/Admin/audit-log` endpoint'i — `[Authorize(Roles = "Admin,SuperAdmin")]`
- Frontend: admin panelinde basit, filtrelenebilir bir denetim günlüğü listesi

**Kabul kriterleri**
- [ ] Bot durdurma/işaretleme olayları günlükte doğru görünüyor
- [ ] Günlük sadece Admin/SuperAdmin tarafından görülebiliyor
- [ ] Günlük tarihe göre sıralı ve filtrelenebilir

---

## Görev 39 — Bot: EMA Kesişimi Stratejisi

**Açıklama**
Bot şu an sadece RSI eşiği ile çalışıyor. Bu görevde ikinci bir strateji seçeneği ekleniyor.

**Yapılacaklar**
- `Models/TradingBot.cs`'e `Strategy` enum ekle: `RsiThreshold` (mevcut), `EmaCrossover`
  (yeni, `ShortEmaPeriod`/`LongEmaPeriod` alanlarıyla)
- Backend'de bağımsız bir EMA hesaplama fonksiyonu yaz
- `BotMonitorService.cs`'i botun `Strategy` alanına göre RSI ya da EMA kesişim kontrolü yapacak şekilde genişlet
- Frontend: bot oluşturma formuna strateji seçici + ilgili parametre alanları ekle

**Kabul kriterleri**
- [ ] EMA kesişimi doğru hesaplanıyor (bilinen bir örnekle elle doğrulanmalı)
- [ ] EMA kesişimi gerçekleştiğinde doğru yönde sinyal üretiliyor ve gerçek Testnet emri gönderiliyor
- [ ] Var olan RSI botları bu değişiklikten etkilenmiyor

---

## Görev 40 — Uygulama Kapalıyken Bildirim (E-posta)

**Açıklama**
Alarm (dakikalık/saatlik/günlük — zaten kurulu) ve bot sinyalleri şu ana kadar sadece
kullanıcı uygulamadayken (polling ile) görünüyordu. Bu görevde uygulama kapalıyken de
kullanıcıyı bilgilendirmek için e-posta bildirimi ekleniyor — Görev 15 ve 24'te bu
kapsam dışı bırakılmıştı, artık ele alıyoruz.

**Yapılacaklar**
- Backend'e basit bir e-posta gönderme servisi ekle (gerçek SMTP varsa onunla, yoksa
  geliştirme ortamında loglayan/sahte bir servisle başlanabilir — ekip karar versin)
- `AlertMonitorService` ve `BotMonitorService`: bir sinyal/tetiklenme oluştuğunda kullanıcının
  e-postasına bildirim gönder
- `Profile.jsx`'e "E-posta bildirimleri" açma/kapama tercihi ekle — kapalıysa e-posta gitmesin
- Spam önleme: aynı olay için kısa sürede tekrar tekrar e-posta gönderilmesin (örn. aynı
  alarm/bot için saatte en fazla 1 e-posta gibi bir sınır)

**Kabul kriterleri**
- [ ] Alarm/bot sinyali tetiklenince kullanıcıya e-posta gidiyor (gerçek SMTP yoksa en
      azından e-postanın içeriği güvenilir şekilde loglanıyor/görülebiliyor)
- [ ] Kullanıcı bildirim tercihini profilinden kapatabiliyor, kapalıysa e-posta gitmiyor
- [ ] Aynı olay için kısa sürede tekrar tekrar e-posta gönderilmiyor

---

## Genel Kurallar

- Bu bir **taslak** — yönetici bu hafta için farklı bir yön belirtirse dosya güncellenecek.
- Sıra: **36 → 37 → 38 sıralı**. **39 ve 40 bağımsız**, paralel yürütülebilir.
- Migration çakışmasını tekrar yaşamamak için: model değişikliği yapan herkes migration
  eklemeden önce `develop`'ı güncel çeksin ve `Migrations/` klasöründe aynı tabloya dokunan
  başka bir migration var mı kontrol etsin.
- Push öncesi `npm run build` ve `dotnet build` hatasız geçmeli.