# Görev Arşivi — Kodda Olup Hiçbir TASKS Dosyasında Yazılmamış Görevler

**Neden bu dosya var?** Görev 40'tan sonra birkaç iş doğrudan koda eklendi, ama hiçbir
`TASKS_HAFTA*.md` dosyasına yazılmadı. Tek iz, kod içindeki `// Görev NN` yorumlarıydı.
Bu dosya o işleri, **kodun bugünkü haliyle** geriye dönük olarak kayda geçiriyor.
Hepsi `main`'de mevcut ve tamamlanmış durumda (PR #72–#80).

> **Numaralandırma notu:** Aşağıdaki görev numaraları **kod içi yorumlardaki**
> numaralardır. Bu işlerin PR/commit mesajlarında farklı numaralar kullanılmış
> (örn. yüzde-değişim alarmı kodda "Görev 46", commit'te "Task 41"). Kodda arama
> yapan birinin (`grep -r "Görev 46"`) doğru yere ulaşması için kod numaraları esas alındı.
> Ayrıca `TASKS_HAFTA9.md`'deki **"Görev 51" (test kapsamı)**, koddaki **"Görev 51"
> (işlem geçmişi sayfalama)** ile çakışıyor; ikisi farklı işlerdir.

| Kod no | Özellik | PR / commit | Ana dosyalar |
|---|---|---|---|
| 40 | Uygulama kapalıyken e-posta bildirimi | #72 (Task 40) | `NotificationService.cs`, `IEmailSender.cs`, `SmtpEmailSender.cs`, `LoggingEmailSender.cs` |
| 41 | Çoklu dil (TR/EN) | #80 (Task 48) | `LanguageContext.jsx`, `LanguageToggle.jsx` |
| 42 | Coin karşılaştırma sayfası | #78 (Task 46) | `pages/Compare/Compare.jsx` |
| 43 | Profil fotoğrafı | #79 (Task 47) | `UserService.SetAvatarAsync`, `Profile.jsx`, `User.AvatarUrl` |
| 44 | Portföy dağılım pasta grafiği | #77 (Task 45) | `Portfolio.jsx` |
| 46 | Yüzde-değişim alarmı | #73 (Task 41) | `AlertConditionEvaluator.cs`, `PriceAlert.cs`, `CoinDetail.jsx` |
| 48 | E-posta doğrulama zorunluluğu | #75 (Task 43) | `AuthService.ConfirmEmailAsync/LoginAsync`, `EmailVerificationToken.cs`, `ConfirmEmail.jsx` |
| 50 | Şifre sıfırlama bağlantısının gerçek e-postayla gönderimi | #74 | `AuthService.ForgotPasswordAsync` |
| 51 (kod) | İşlem geçmişi sayfalama + filtre | #76 (Task 44) | `PortfolioService.cs`, `PagedResult.cs`, `Portfolio.jsx` |

---

## Görev 40 — Uygulama Kapalıyken Bildirim (E-posta)

Tanımı `TASKS_HAFTA8.md`'de var; GitHub issue #68. Burada sadece nasıl yapıldığı özetleniyor.

- `IEmailSender` soyutlaması: SMTP ayarı varsa `SmtpEmailSender`, yoksa e-postayı loglayan
  `LoggingEmailSender` (geliştirme ortamı). Seçim `Program.cs`'te yapılır.
- `NotificationService`: `NotifyPriceAlertAsync` ve `NotifyBotSignalAsync`.
  `AlertMonitorService` ve `BotMonitorService` tetiklenmede bunu çağırır.
- Tercih: `User.EmailNotificationsEnabled`, `PUT /api/Auth/me/notifications`, `Profile.jsx`'teki anahtar.
- Spam önleme: `PriceAlert.LastEmailNotifiedAt` / `TradingBot.LastEmailNotifiedAt` +
  `Notifications:CooldownMinutes` (varsayılan 60 dk).
- Testler: `NotificationServiceTests.cs` (bkz. `TASKS_HAFTA9.md`, test kapsamı görevi).

## Görev 41 — Çoklu Dil Desteği (TR/EN)

- Kütüphanesiz, `LanguageContext.jsx` içinde sözlük + `t('anahtar')` fonksiyonu.
- Her sayfada sabit duran `LanguageToggle` bileşeni; seçim tarayıcıda hatırlanır.

## Görev 42 — Coin Karşılaştırma (Compare)

- `/compare` sayfası: iki coin seçilip fiyat ve değişim verileri yan yana gösterilir.
- Sadece frontend; mevcut fiyat verisini kullanır, yeni API ucu yok.

## Görev 43 — Profil Fotoğrafı

- `User.AvatarUrl` (base64 data URL) + migration `AddUserAvatar`.
- `PUT /api/Auth/me/avatar`; `UserService.SetAvatarAsync` 4.000.000 karakterden büyük
  veriyi reddeder (~3 MB görsel).
- `Profile.jsx`'te fotoğraf seçme/kaldırma.

## Görev 44 — Portföy Dağılım Grafiği

- `Portfolio.jsx`'te coin bazında portföy dağılımını gösteren pasta grafik.

## Görev 46 — Yüzde-Değişim Alarmı

- `PriceAlert.Type` (`Price` / `PercentChange`), `ReferencePrice` (alarm kurulurken
  kaydedilen fiyat), `PercentChangeThreshold`.
- `AlertConditionEvaluator.IsPercentChangeConditionSatisfied`: Above = en az eşik kadar
  yükseliş, Below = en az eşik kadar düşüş. Referans fiyat veya eşik eksik, sıfır ya da
  negatifse `false` döner.
- `CoinDetail.jsx`'te alarm türü seçici (fiyat / yüzde).

## Görev 48 — E-posta Doğrulama Zorunluluğu

- Kayıtta `EmailVerificationToken` üretilir ve doğrulama bağlantısı e-postayla gönderilir.
- `POST /api/Auth/confirm-email` → `ConfirmEmailAsync`: token geçersizse, kullanılmışsa
  veya süresi dolmuşsa hata verir.
- `LoginAsync`: `EmailConfirmed = false` olan kullanıcı giriş yapamaz.
- Frontend: `/confirm-email/:token` sayfası (`ConfirmEmail.jsx`), kayıt sonrası bilgilendirme.

## Görev 50 — Şifre Sıfırlama E-postası

- `ForgotPasswordAsync`, sıfırlama bağlantısını (`App:FrontendBaseUrl/reset-password/{token}`)
  Görev 40 altyapısıyla gerçekten e-postalar. SMTP yoksa bağlantı loglanır.
- Güvenlik: e-posta kayıtlı olsun olmasın aynı mesaj döner (hesap varlığı sızdırılmaz).

## Görev 51 (kod) — İşlem Geçmişi Sayfalama

- `GET /api/Portfolio/transactions?pageNumber=&pageSize=&symbol=&type=` → `PagedResult<T>`.
- Sembolde kısmi arama ("eth" → ETHUSDT), işlem türü filtresi.
