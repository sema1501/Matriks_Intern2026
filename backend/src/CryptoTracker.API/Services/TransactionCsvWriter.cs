using System.Globalization;
using System.Text;
using CryptoTracker.API.DTOs;
using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

/// <summary>
/// İşlem geçmişini CSV'ye çevirir (Görev 89).
/// - Ayırıcı ';' ve sayılar Türkçe biçimde (ondalık virgül): Türkçe Excel dosyayı doğrudan sütunlara böler.
/// - Başa UTF-8 BOM eklenir: Excel Türkçe karakterleri (Alış/Satış) bozuk göstermez.
/// </summary>
public static class TransactionCsvWriter
{
    public const char Separator = ';';
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static readonly string[] Headers =
        ["Tarih (UTC)", "İşlem Türü", "Sembol", "Miktar", "Fiyat (USD)", "Toplam (USD)"];

    public static byte[] ToCsvBytes(IEnumerable<TransactionDto> transactions)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(Separator, Headers.Select(Escape))).Append("\r\n");

        foreach (var t in transactions)
        {
            var fields = new[]
            {
                t.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                t.Type == TransactionType.Buy ? "Alış" : "Satış",
                t.Symbol,
                t.Quantity.ToString("0.########", Tr),
                t.Price.ToString("0.########", Tr),
                t.TotalAmount.ToString("0.########", Tr)
            };
            sb.Append(string.Join(Separator, fields.Select(Escape))).Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble(); // UTF-8 BOM: EF BB BF
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return [.. preamble, .. body];
    }

    /// <summary>Ayırıcı, tırnak veya satır sonu içeren alanları tırnak içine alır (RFC 4180).</summary>
    public static string Escape(string value)
    {
        if (value.IndexOfAny([Separator, '"', '\r', '\n']) < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
