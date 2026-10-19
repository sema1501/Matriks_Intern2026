using System.Globalization;
using CryptoTracker.API.Models;

namespace CryptoTracker.API.Services;

/// <summary>
/// İşlem geçmişi dışa aktarımının dile bağlı metinleri ve biçimleri.
/// Kullanıcı arayüzde hangi dili seçtiyse CSV/Excel dosyası da o dilde iner.
/// </summary>
public sealed record TransactionExportText(
    string Language,
    string[] Headers,
    string BuyLabel,
    string SellLabel,
    string SheetName,
    string FileBaseName,
    char CsvSeparator,
    string CsvDateFormat,
    CultureInfo NumberCulture)
{
    /// <summary>Türkçe Excel ';' ile böler ve ondalık için virgül bekler.</summary>
    public static readonly TransactionExportText Turkish = new(
        "tr",
        ["Tarih (UTC)", "İşlem Türü", "Sembol", "Miktar", "Fiyat (USD)", "Toplam (USD)"],
        "Alış",
        "Satış",
        "İşlemler",
        "islemler",
        ';',
        "dd.MM.yyyy HH:mm:ss",
        CultureInfo.GetCultureInfo("tr-TR"));

    /// <summary>İngilizce CSV standardı: ',' ayırıcı, ondalık için nokta.</summary>
    public static readonly TransactionExportText English = new(
        "en",
        ["Date (UTC)", "Type", "Symbol", "Quantity", "Price (USD)", "Total (USD)"],
        "Buy",
        "Sell",
        "Transactions",
        "transactions",
        ',',
        "yyyy-MM-dd HH:mm:ss",
        CultureInfo.InvariantCulture);

    public string TypeLabel(TransactionType type) =>
        type == TransactionType.Buy ? BuyLabel : SellLabel;

    /// <summary>"tr" veya "en" (büyük/küçük harf fark etmez). Boşsa Türkçe.</summary>
    public static TransactionExportText For(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
            return Turkish;

        return language.Trim().ToLowerInvariant() switch
        {
            "tr" => Turkish,
            "en" => English,
            _ => throw new ArgumentException("Geçersiz dil. Kullanılabilir değerler: tr, en.")
        };
    }
}
