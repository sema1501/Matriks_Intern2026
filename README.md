# CryptoTracker — 2025 Staj Projesi

Full Stack kripto para takip uygulaması.  
**Stack:** ASP.NET Core 9 · React 18 · MSSQL · Docker · JWT

---

## Kurulum

### Gereksinimler
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)

### 1. Repoyu klonla
```bash
git clone <repo-url>
cd CryptoTracker
```

### 2. Veritabanını başlat
```bash
cd backend
docker-compose up -d db
# ~15 saniye bekle
```

### 3. Backend'i çalıştır
```bash
cd backend/src/CryptoTracker.API
dotnet restore
dotnet ef database update
dotnet run
# → http://localhost:5002
# → http://localhost:5002/swagger
```

### 4. Frontend'i çalıştır
```bash
cd frontend
npm install
npm start
# → http://localhost:3000
```

### Test kullanıcısı (otomatik oluşturulur)
| Alan | Değer |
|------|-------|
| Username | admin |
| Password | Admin123! |
| Roller | Admin, User |

---

## Bu Hafta Görevler

Görev tanımları için **[TASKS.md](./TASKS.md)** dosyasını oku.

| Görev | Konu | Dosyalar |
|-------|------|---------|
| 1 | Kayıt & Giriş | `AuthService.cs`, `SignIn.jsx`, `SignUp.jsx` |
| 2 | Kullanıcı Yönetimi | `UserService.cs`, `Profile.jsx` |
| 3 | Rol Yönetimi | `RoleService.cs` |
| 4 | Frontend Routing | `Navbar.jsx`, PrivateRoute |
| 5 | Dashboard | `DashboardController.cs`, `Dashboard.jsx` |

---

## Proje Yapısı

```
CryptoTracker/
├── backend/
│   ├── docker-compose.yml
│   ├── Dockerfile
│   └── src/CryptoTracker.API/
│       ├── Controllers/     ← API endpoint'leri
│       ├── Models/          ← Veritabanı modelleri
│       ├── DTOs/            ← Request / Response nesneleri
│       ├── Services/        ← İş mantığı (TODO'lar burada)
│       ├── Data/            ← DbContext + DataSeeder
│       └── Middleware/      ← Hata yakalama
└── frontend/
    └── src/
        ├── components/Navbar/
        ├── pages/           ← SignIn, SignUp, Profile, Dashboard
        ├── services/        ← API çağrıları (apiService.js)
        └── context/         ← AuthContext (JWT state)
```
