using System.Globalization;
using System.Text;
using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

/// <summary>
/// İşlem geçmişini CSV'ye çevirir (Görev 89).
/// - Başlıklar, işlem türü, ayırıcı ve sayı biçimi seçilen dile göre (TransactionExportText).
///   Türkçe: ';' ayırıcı ve ondalık virgül, Türkçe Excel dosyayı doğrudan sütunlara böler.
///   İngilizce: ',' ayırıcı ve ondalık nokta.
/// - Başa UTF-8 BOM eklenir: Excel Türkçe karakterleri (Alış/Satış) bozuk göstermez.
/// </summary>
public static class TransactionCsvWriter
{
    public const char Separator = ';';

    public static string[] Headers => TransactionExportText.Turkish.Headers;

    public static byte[] ToCsvBytes(IEnumerable<TransactionDto> transactions) =>
        ToCsvBytes(transactions, TransactionExportText.Turkish);

    public static byte[] ToCsvBytes(IEnumerable<TransactionDto> transactions, TransactionExportText text)
    {
        var separator = text.CsvSeparator;
        var sb = new StringBuilder();
        sb.Append(string.Join(separator, text.Headers.Select(h => Escape(h, separator)))).Append("\r\n");

        foreach (var t in transactions)
        {
            var fields = new[]
            {
                t.CreatedAt.ToString(text.CsvDateFormat, CultureInfo.InvariantCulture),
                text.TypeLabel(t.Type),
                t.Symbol,
                t.Quantity.ToString("0.########", text.NumberCulture),
                t.Price.ToString("0.########", text.NumberCulture),
                t.TotalAmount.ToString("0.########", text.NumberCulture)
            };
            sb.Append(string.Join(separator, fields.Select(f => Escape(f, separator)))).Append("\r\n");
        }

        var preamble = Encoding.UTF8.GetPreamble(); // UTF-8 BOM: EF BB BF
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        return [.. preamble, .. body];
    }

    /// <summary>Ayırıcı, tırnak veya satır sonu içeren alanları tırnak içine alır (RFC 4180).</summary>
    public static string Escape(string value, char separator = Separator)
    {
        if (value.IndexOfAny([separator, '"', '\r', '\n']) < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
