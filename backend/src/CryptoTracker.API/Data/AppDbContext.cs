using CryptoTracker.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CryptoTracker.API.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User>          Users          => Set<User>();
    public DbSet<Role>          Roles          => Set<Role>();
    public DbSet<UserRole>      UserRoles      => Set<UserRole>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<PriceAlert>    PriceAlerts    => Set<PriceAlert>();
    public DbSet<AlertSignal>   AlertSignals   => Set<AlertSignal>();
    public DbSet<Feedback>      Feedbacks      => Set<Feedback>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    
    public DbSet<PortfolioHolding> PortfolioHoldings => Set<PortfolioHolding>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    
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

        // Feedback Relationship
        modelBuilder.Entity<Feedback>()
            .HasOne(f => f.User)
            .WithMany()
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        // Unique constraints
        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<Role>().HasIndex(r => r.Name).IsUnique();

        // WatchlistItem: bir kullanıcı aynı symbol'ü bir kez ekleyebilir
        modelBuilder.Entity<WatchlistItem>()
            .HasIndex(w => new { w.UserId, w.Symbol })
            .IsUnique();

        modelBuilder.Entity<WatchlistItem>()
            .HasOne(w => w.User)
            .WithMany()
            .HasForeignKey(w => w.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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

        modelBuilder.Entity<PriceAlert>()
            .Property(a => a.IsActive)
            .HasDefaultValue(true);

        modelBuilder.Entity<PriceAlert>()
            .Property(a => a.Interval)
            .HasDefaultValue(AlertInterval.Minute);

        // AlertSignal → PriceAlert (cascade with alert deletion, matching User→Alert)
        modelBuilder.Entity<AlertSignal>()
            .HasOne(s => s.Alert)
            .WithMany(a => a.Signals)
            .HasForeignKey(s => s.AlertId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AlertSignal>()
            .HasIndex(s => s.AlertId);

        modelBuilder.Entity<AlertSignal>()
            .HasIndex(s => s.TriggeredAt);

        modelBuilder.Entity<AlertSignal>()
            .Property(s => s.PriceAtTrigger)
            .HasPrecision(18, 8);

        // PasswordResetToken → User
        modelBuilder.Entity<PasswordResetToken>().HasOne(prt => prt.User).WithMany().HasForeignKey(prt => prt.UserId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<PasswordResetToken>().HasIndex(prt => prt.Token).IsUnique();
                // Virtual balance
        modelBuilder.Entity<User>()
            .Property(u => u.VirtualBalance)
            .HasPrecision(18, 2)
            .HasDefaultValue(10000m);

        // PortfolioHolding → User
        modelBuilder.Entity<PortfolioHolding>()
            .HasOne(h => h.User)
            .WithMany()
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<PortfolioHolding>()
            .HasIndex(h => new { h.UserId, h.Symbol })
            .IsUnique();

        modelBuilder.Entity<PortfolioHolding>()
            .Property(h => h.Symbol)
            .HasMaxLength(50);

        modelBuilder.Entity<PortfolioHolding>()
            .Property(h => h.Quantity)
            .HasPrecision(18, 8);

        modelBuilder.Entity<PortfolioHolding>()
            .Property(h => h.AvgBuyPrice)
            .HasPrecision(18, 8);

        // Transaction → User
        modelBuilder.Entity<Transaction>()
            .HasOne(t => t.User)
            .WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.UserId);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Symbol)
            .HasMaxLength(50);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Quantity)
            .HasPrecision(18, 8);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Price)
            .HasPrecision(18, 8);
    }
}
