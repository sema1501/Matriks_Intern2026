using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>     Users     => Set<User>();
    public DbSet<Role>     Roles     => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<PriceAlert> PriceAlerts => Set<PriceAlert>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Composite PK for join table
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.User)
            .WithMany(u => u.UserRoles)
            .HasForeignKey(ur => ur.UserId);

        modelBuilder.Entity<UserRole>()
            .HasOne(ur => ur.Role)
            .WithMany(r => r.UserRoles)
            .HasForeignKey(ur => ur.RoleId);

        // Unique constraints
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(r => r.Name).IsUnique();

        // PriceAlert → User
        modelBuilder.Entity<PriceAlert>()
            .HasOne(a => a.User)
            .WithMany(u => u.PriceAlerts)
            .HasForeignKey(a => a.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PriceAlert>()
            .HasIndex(a => new { a.UserId, a.Symbol });

        modelBuilder.Entity<PriceAlert>()
            .Property(a => a.Symbol)
            .HasMaxLength(50);

        modelBuilder.Entity<PriceAlert>()
            .Property(a => a.TargetPrice)
            .HasPrecision(18, 8);
    }
}
