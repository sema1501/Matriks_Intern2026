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
    public DbSet<Transaction>   Transactions   => Set<Transaction>();
    public DbSet<TradingBot> TradingBots => Set<TradingBot>();
    public DbSet<BotSignal> BotSignals => Set<BotSignal>();

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

        // User.VirtualBalance
        modelBuilder.Entity<User>()
            .Property(u => u.VirtualBalance)
            .HasPrecision(18, 8)
            .HasDefaultValue(10_000m);

        // PortfolioHolding → User (bir kullanıcı aynı symbol'ü bir kez tutabilir)
        modelBuilder.Entity<PortfolioHolding>()
            .HasIndex(h => new { h.UserId, h.Symbol })
            .IsUnique();

        modelBuilder.Entity<PortfolioHolding>()
            .HasOne(h => h.User)
            .WithMany(u => u.Holdings)
            .HasForeignKey(h => h.UserId)
            .OnDelete(DeleteBehavior.Cascade);

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
            .WithMany(u => u.Transactions)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.UserId);

        modelBuilder.Entity<Transaction>()
            .HasIndex(t => t.CreatedAt);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Symbol)
            .HasMaxLength(50);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Quantity)
            .HasPrecision(18, 8);

        modelBuilder.Entity<Transaction>()
            .Property(t => t.Price)
            .HasPrecision(18, 8);
            // TradingBot → User
        modelBuilder.Entity<TradingBot>()
            .HasOne(b => b.User)
            .WithMany(u => u.TradingBots)
            .HasForeignKey(b => b.UserId)
            .OnDelete(DeleteBehavior.Cascade);
            // BotSignal → TradingBot
        modelBuilder.Entity<BotSignal>()
            .HasOne(s => s.Bot)
            .WithMany(b => b.Signals)
            .HasForeignKey(s => s.BotId)
            .OnDelete(DeleteBehavior.Cascade);
            // TradingBot.Symbol
        modelBuilder.Entity<TradingBot>()
            .Property(b => b.Symbol)
            .HasMaxLength(50)
            .IsRequired();

            // TradingBot RSI değerleri
        modelBuilder.Entity<TradingBot>()
            .Property(b => b.BuyRsiThreshold)
            .HasPrecision(5, 2);

        modelBuilder.Entity<TradingBot>()
           .Property(b => b.SellRsiThreshold)
           .HasPrecision(5, 2);

           // TradingBot işlem miktarı
        modelBuilder.Entity<TradingBot>()
            .Property(b => b.TradeQuantity)
            .HasPrecision(18, 8);

            // BotSignal RSI ve fiyat değerleri
        modelBuilder.Entity<BotSignal>()
            .Property(s => s.RsiValueAtSignal)
            .HasPrecision(5, 2);
        modelBuilder.Entity<BotSignal>()
            .Property(s => s.PriceAtSignal)
            .HasPrecision(18, 8);

            // BotSignal indeksleri
        modelBuilder.Entity<BotSignal>()
            .HasIndex(s => s.BotId);

            modelBuilder.Entity<BotSignal>()
            .HasIndex(s => s.CreatedAt);

            // Varsayılan değerler
        modelBuilder.Entity<TradingBot>()
           .Property(b => b.IsActive)
           .HasDefaultValue(true);

        modelBuilder.Entity<BotSignal>()
           .Property(s => s.Status)
           .HasDefaultValue(BotSignalStatus.Pending);

           // Bir kullanıcı aynı işlem çifti için yalnızca bir bot oluşturabilir
        modelBuilder.Entity<TradingBot>()
           .HasIndex(b => new { b.UserId, b.Symbol })
           .IsUnique();
    }
}
