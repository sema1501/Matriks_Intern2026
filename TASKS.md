# 1. Hafta Görevleri

Bu dosyadaki 5 görevi ekip arasında bölüştürün.
Her görev için bir GitHub Issue açın ve PR'ınızda o issue'yu kapatın.

---

## Görev 1 — Kayıt & Giriş (Auth)

**Backend:** `AuthService.cs` içindeki `RegisterAsync` ve `LoginAsync` metodlarını implement edin.

Adımlar:
1. `RegisterAsync`: email/username benzersizliğini kontrol et → BCrypt ile şifrele → User kaydet → varsayılan "User" rolünü ata → JWT token üret ve dön
2. `LoginAsync`: kullanıcıyı email VEYA username ile bul → BCrypt.Verify → JWT token üret ve dön

**Frontend:** `SignIn.jsx` ve `SignUp.jsx` sayfalarını tamamlayın, `Navbar.jsx`'in auth bölümünü düzeltin.

Beklenen çıktı: Swagger'dan `/api/Auth/register` ve `/api/Auth/login` başarıyla çalışıyor. Tarayıcıda giriş yapılabiliyor.

---

## Görev 2 — Kullanıcı Yönetimi

**Backend:** `UserService.cs` içindeki tüm metodları implement edin.

Adımlar:
1. `GetByIdAsync`: User'ı UserRoles + Role ile birlikte çek, UserDto'ya map et
2. `GetAllAsync`: Tüm kullanıcıları UserRoles + Role ile çek
3. `UpdateProfileAsync`: username/email güncelle, benzersizlik kontrolü yap
4. `ChangePasswordAsync`: BCrypt.Verify ile mevcut şifreyi doğrula, yeni şifreyi hashle

**Frontend:** `Profile.jsx` sayfasını tamamlayın (bilgileri göster + düzenleme formu + şifre değiştirme).

Beklenen çıktı: `/api/Auth/me` profil bilgisini döndürüyor. Profil sayfasında kullanıcı bilgileri görünüyor.

---

## Görev 3 — Rol Yönetimi

**Backend:** `RoleService.cs` içindeki tüm metodları implement edin.

Adımlar:
1. `GetAllAsync`: Tüm rolleri listele
2. `CreateAsync`: İsim benzersizliğini kontrol et, yeni rol oluştur
3. `AssignRoleAsync`: Kullanıcı ve rolün varlığını doğrula, zaten atanmış mı kontrol et, UserRole ekle
4. `RemoveRoleAsync`: UserRole kaydını bul ve sil
5. `GetUserRolesAsync`: Kullanıcının rol isimlerini döndür

Beklenen çıktı: `/api/Role` endpoint'leri Swagger'da çalışıyor. Admin kullanıcıya role atanabiliyor.

---

## Görev 4 — Frontend Temel Yapı & Routing

**Frontend:** Aşağıdaki konuları tamamlayın.

Adımlar:
1. `Navbar.jsx`: Giriş yapmış kullanıcıya rol rozeti ekle, Admin için Dashboard linki göster, dropdown menü yap
2. Korumalı rota (PrivateRoute) bileşeni yaz — giriş yapılmamışsa `/signin`'e yönlendir
3. `Dashboard.jsx` ve `Profile.jsx` sayfalarını PrivateRoute ile koru
4. Genel hata mesajları için toast/alert mekanizması kur

Beklenen çıktı: Giriş yapılmadan `/profile`'a gidildiğinde login sayfasına yönleniyor. Navbar'da kullanıcı adı ve rol görünüyor.

---

## Görev 5 — Dashboard & Bonus

**Backend + Frontend:**

1. `DashboardController.cs`'deki `daily-new-users` endpoint'ini genişlet:
   - Son 7 günün günlük yeni kullanıcı sayısını döndür (array)
2. `Dashboard.jsx` sayfasına bu veriyi göster (basit tablo veya liste)
3. (Bonus) Toplam kullanıcı sayısı ve toplam rol sayısı için 2 ek endpoint yaz

Beklenen çıktı: Admin hesabıyla `/dashboard`'a girince günlük istatistikler görünüyor.

---

## Genel Kurallar

- Her görev için ayrı bir `feature/gorev-X` branch'i aç
- PR açmadan önce `dotnet build` ve `npm start` hata vermediğinden emin ol
- `appsettings.Development.json` dosyasını **kesinlikle** commit etme
- Takıldığında önce eski projenin ilgili dosyasına bak, sonra sor
