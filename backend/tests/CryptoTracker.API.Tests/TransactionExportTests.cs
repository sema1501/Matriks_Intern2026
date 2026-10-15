using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CryptoTracker.API.Data;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;
using CryptoTracker.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public void Csv_English_UsesEnglishHeadersCommaSeparatorAndDecimalPoint()
    {
        var text = Encoding.UTF8.GetString(
            TransactionCsvWriter.ToCsvBytes(SampleRows(), TransactionExportText.English)).TrimStart('\uFEFF');
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("Date (UTC),Type,Symbol,Quantity,Price (USD),Total (USD)", lines[0]);
        Assert.Equal("2026-10-01 12:30:00,Buy,BTCUSDT,0.5,100,50", lines[1]);
        Assert.Equal("2026-10-02 08:00:00,Sell,ETHUSDT,2,10.25,20.5", lines[2]);
    }

    [Fact]
    public void Csv_English_EscapesCommaInsteadOfSemicolon()
    {
        Assert.Equal("\"a,b\"", TransactionCsvWriter.Escape("a,b", ','));
        Assert.Equal("a;b", TransactionCsvWriter.Escape("a;b", ','));
    }

    [Theory]
    [InlineData(null, "tr")]
    [InlineData("", "tr")]
    [InlineData("tr", "tr")]
    [InlineData("EN", "en")]
    [InlineData(" en ", "en")]
    public void ExportText_For_ReturnsLanguage(string? input, string expected)
    {
        Assert.Equal(expected, TransactionExportText.For(input).Language);
    }

    [Fact]
    public void ExportText_For_UnknownLanguage_Throws()
    {
        Assert.Throws<ArgumentException>(() => TransactionExportText.For("de"));
    }

    // ── Excel (.xlsx) ────────────────────────────────────────────────

    [Fact]
    public void Xlsx_IsValidZipWithWorkbookParts()
    {
        var bytes = TransactionXlsxWriter.ToXlsxBytes(SampleRows(), TransactionExportText.Turkish);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        var names = zip.Entries.Select(e => e.FullName).ToList();
        Assert.Contains("[Content_Types].xml", names);
        Assert.Contains("xl/workbook.xml", names);
        Assert.Contains("xl/worksheets/sheet1.xml", names);
        Assert.Contains("xl/styles.xml", names);
    }

    [Theory]
    [InlineData("tr", "İşlem Türü", "Alış", "Satış", "İşlemler")]
    [InlineData("en", "Type", "Buy", "Sell", "Transactions")]
    public void Xlsx_UsesSelectedLanguage(string lang, string header, string buy, string sell, string sheetName)
    {
        var bytes = TransactionXlsxWriter.ToXlsxBytes(SampleRows(), TransactionExportText.For(lang));

        var sheet = ReadEntry(bytes, "xl/worksheets/sheet1.xml");
        Assert.Contains($"<t>{header}</t>", sheet);
        Assert.Contains($"<t>{buy}</t>", sheet);
        Assert.Contains($"<t>{sell}</t>", sheet);
        Assert.Contains($"name=\"{sheetName}\"", ReadEntry(bytes, "xl/workbook.xml"));
    }

    [Fact]
    public void Xlsx_WritesNumbersAsNumbers_NotLocaleText()
    {
        var bytes = TransactionXlsxWriter.ToXlsxBytes(SampleRows(), TransactionExportText.Turkish);
        var sheet = ReadEntry(bytes, "xl/worksheets/sheet1.xml");

        // Türkçe dosyada bile hücre değeri "10,25" değil, sayı olarak 10.25 yazılır;
        // Excel kendi bölge ayarına göre virgülle gösterir.
        Assert.Contains("<c r=\"E3\"><v>10.25</v></c>", sheet);
        Assert.Contains("<c r=\"D2\"><v>0.5</v></c>", sheet);
    }

    [Fact]
    public void Xlsx_EscapesXmlSpecialCharacters()
    {
        var rows = new List<TransactionDto>
        {
            new(1, "A&B<C>", TransactionType.Buy, 1m, 1m, 1m, new DateTime(2026, 10, 1))
        };

        var sheet = ReadEntry(TransactionXlsxWriter.ToXlsxBytes(rows, TransactionExportText.English), "xl/worksheets/sheet1.xml");

        Assert.Contains("<t>A&amp;B&lt;C&gt;</t>", sheet);
    }

    [Fact]
    public void Xlsx_ExcelDate_IsDaysSince1899()
    {
        Assert.Equal(45200m, TransactionXlsxWriter.ToExcelDate(new DateTime(2023, 10, 1)));
        Assert.Equal(45200.5m, TransactionXlsxWriter.ToExcelDate(new DateTime(2023, 10, 1, 12, 0, 0)));
    }

    private static List<TransactionDto> SampleRows() =>
    [
        new(1, "BTCUSDT", TransactionType.Buy, 0.5m, 100m, 50m, new DateTime(2026, 10, 1, 12, 30, 0)),
        new(2, "ETHUSDT", TransactionType.Sell, 2m, 10.25m, 20.5m, new DateTime(2026, 10, 2, 8, 0, 0))
    ];

    private static string ReadEntry(byte[] xlsx, string path)
    {
        using var zip = new ZipArchive(new MemoryStream(xlsx));
        using var reader = new StreamReader(zip.GetEntry(path)!.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
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

/// <summary>Dışa aktarma ucunun dil ve dosya biçimi parametreleri, gerçek HTTP üzerinden.</summary>
public class TransactionExportIntegrationTests(AlertApiFactory factory) : IClassFixture<AlertApiFactory>
{
    private const int UserId = 70_001;

    [Fact]
    public async Task Export_English_Csv_ReturnsEnglishFile()
    {
        await SeedAsync();
        var response = await CreateClient().GetAsync("/api/Portfolio/transactions/export?lang=en");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("transactions.csv", response.Content.Headers.ContentDisposition?.FileName);
        var body = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Date (UTC),Type,Symbol", body);
        Assert.Contains(",Buy,", body);
    }

    [Fact]
    public async Task Export_DefaultsToTurkishCsv()
    {
        await SeedAsync();
        var response = await CreateClient().GetAsync("/api/Portfolio/transactions/export");

        Assert.Equal("islemler.csv", response.Content.Headers.ContentDisposition?.FileName);
        var body = Encoding.UTF8.GetString(await response.Content.ReadAsByteArrayAsync());
        Assert.Contains("Tarih (UTC);İşlem Türü;Sembol", body);
    }

    [Theory]
    [InlineData("tr", "islemler.xlsx")]
    [InlineData("en", "transactions.xlsx")]
    public async Task Export_Xlsx_ReturnsExcelFile(string lang, string fileName)
    {
        await SeedAsync();
        var response = await CreateClient().GetAsync($"/api/Portfolio/transactions/export?lang={lang}&format=xlsx");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(TransactionXlsxWriter.ContentType, response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(fileName, response.Content.Headers.ContentDisposition?.FileName);

        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet1.xml"));
    }

    [Theory]
    [InlineData("lang=de")]
    [InlineData("format=pdf")]
    public async Task Export_InvalidLanguageOrFormat_Returns400(string query)
    {
        var response = await CreateClient().GetAsync($"/api/Portfolio/transactions/export?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task SeedAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        if (await db.Transactions.AnyAsync(t => t.UserId == UserId))
            return;
        db.Transactions.Add(new Transaction
        {
            UserId = UserId, Symbol = "BTCUSDT", Type = TransactionType.Buy,
            Quantity = 0.5m, Price = 100m, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private HttpClient CreateClient()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Test");
        client.DefaultRequestHeaders.Add("X-Test-UserId", UserId.ToString());
        return client;
    }
}
