using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Data;

public static class DataSeeder
{
    private static readonly string[] DefaultRoles =
        ["SuperAdmin", "Admin", "Moderator", "PremiumUser", "User"];

    public static async Task SeedAsync(AppDbContext db)
    {
        // Seed roles
        foreach (var roleName in DefaultRoles)
        {
            if (!await db.Roles.AnyAsync(r => r.Name == roleName))
                db.Roles.Add(new Role { Name = roleName });
        }
        await db.SaveChangesAsync();

        // Seed a default admin user (dev only)
        if (!await db.Users.AnyAsync(u => u.Username == "admin"))
        {
            var admin = new User
            {
                Username     = "admin",
                Email        = "admin@cryptotracker.dev",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin123!")
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync();

            var adminRole = await db.Roles.FirstAsync(r => r.Name == "Admin");
            var userRole  = await db.Roles.FirstAsync(r => r.Name == "User");
            db.UserRoles.AddRange(
                new UserRole { UserId = admin.Id, RoleId = adminRole.Id },
                new UserRole { UserId = admin.Id, RoleId = userRole.Id }
            );
            await db.SaveChangesAsync();
        }
    }
}
