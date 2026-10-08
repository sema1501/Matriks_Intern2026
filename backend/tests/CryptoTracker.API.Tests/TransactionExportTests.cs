using System.Text;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CryptoTracker.API.Tests;

/// <summary>Görev 89: işlem geçmişini CSV olarak dışa aktarma.</summary>
public class TransactionExportTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static void AddTx(AppDbContext db, int userId, string symbol, TransactionType type, int minutesAgo)
    {
        db.Transactions.Add(new Transaction
        {
            UserId = userId,
            Symbol = symbol,
            Type = type,
            Quantity = 0.5m,
            Price = 100m,
            CreatedAt = DateTime.UtcNow.AddMinutes(-minutesAgo)
        });
    }

    // ── Servis: filtreler, kullanıcı izolasyonu, üst sınır ─────────────

    [Fact]
    public async Task Export_ReturnsOnlyOwnTransactions()
    {
        await using var db = CreateDb();
        AddTx(db, userId: 1, "BTCUSDT", TransactionType.Buy, 1);
        AddTx(db, userId: 2, "ETHUSDT", TransactionType.Buy, 2);
        await db.SaveChangesAsync();

        var result = await new PortfolioService(db).GetTransactionsForExportAsync(1);

        Assert.Single(result);
        Assert.Equal("BTCUSDT", result[0].Symbol);
    }

    [Fact]
    public async Task Export_AppliesSameFiltersAsPagedList()
    {
        await using var db = CreateDb();
        AddTx(db, 1, "BTCUSDT", TransactionType.Buy, 1);
        AddTx(db, 1, "BTCUSDT", TransactionType.Sell, 2);
        AddTx(db, 1, "ETHUSDT", TransactionType.Buy, 3);
        await db.SaveChangesAsync();
        var service = new PortfolioService(db);

        var export = await service.GetTransactionsForExportAsync(1, symbol: "btc", type: TransactionType.Buy);
        var paged = await service.GetTransactionHistoryAsync(1, 1, 100, symbol: "btc", type: TransactionType.Buy);

        Assert.Single(export);
        Assert.Equal(paged.Items.Select(t => t.Id), export.Select(t => t.Id));
    }

    [Fact]
    public async Task Export_IsNotPaged_AndRespectsMaxRows()
    {
        await using var db = CreateDb();
        for (var i = 0; i < 150; i++)
            AddTx(db, 1, "BTCUSDT", TransactionType.Buy, i);
        await db.SaveChangesAsync();
        var service = new PortfolioService(db);

        var all = await service.GetTransactionsForExportAsync(1);
        var capped = await service.GetTransactionsForExportAsync(1, maxRows: 10);

        Assert.Equal(150, all.Count);   // sayfalı liste en fazla 100 döner, export hepsini döner
        Assert.Equal(10, capped.Count);
        Assert.True(all[0].CreatedAt >= all[^1].CreatedAt); // en yeni üstte
    }

    [Fact]
    public async Task Export_3000Transactions_ReturnsAll()
    {
        await using var db = CreateDb();
        for (var i = 0; i < 3000; i++)
            AddTx(db, 1, "BTCUSDT", i % 2 == 0 ? TransactionType.Buy : TransactionType.Sell, i);
        await db.SaveChangesAsync();

        var result = await new PortfolioService(db).GetTransactionsForExportAsync(1);
        var bytes = TransactionCsvWriter.ToCsvBytes(result);
        var lines = Encoding.UTF8.GetString(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3000, result.Count);
        Assert.Equal(3001, lines.Length); // başlık + 3000 satır
    }

    // ── CSV biçimi ───────────────────────────────────────────────────

    [Fact]
    public void Csv_StartsWithUtf8Bom()
    {
        var bytes = TransactionCsvWriter.ToCsvBytes([]);

        Assert.True(bytes.Length >= 3);
        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes[..3]);
    }

    [Fact]
    public void Csv_HasHeaderAndTurkishTypeNames()
    {
        var rows = new List<TransactionDto>
        {
            new(1, "BTCUSDT", TransactionType.Buy, 0.5m, 100m, 50m, new DateTime(2026, 10, 1, 12, 30, 0)),
            new(2, "ETHUSDT", TransactionType.Sell, 2m, 10.25m, 20.5m, new DateTime(2026, 10, 2, 8, 0, 0))
        };

        var text = Encoding.UTF8.GetString(TransactionCsvWriter.ToCsvBytes(rows)).TrimStart('﻿');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Tarih (UTC);İşlem Türü;Sembol;Miktar;Fiyat (USD);Toplam (USD)", lines[0]);
        Assert.Equal("01.10.2026 12:30:00;Alış;BTCUSDT;0,5;100;50", lines[1]);
        Assert.Equal("02.10.2026 08:00:00;Satış;ETHUSDT;2;10,25;20,5", lines[2]);
    }

    [Theory]
    [InlineData("BTCUSDT", "BTCUSDT")]
    [InlineData("a;b", "\"a;b\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    public void Csv_EscapesSpecialCharacters(string input, string expected)
    {
        Assert.Equal(expected, TransactionCsvWriter.Escape(input));
    }
}
