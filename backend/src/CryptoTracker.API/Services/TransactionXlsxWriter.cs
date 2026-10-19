using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;
using CryptoTracker.API.DTOs;

namespace CryptoTracker.API.Services;

/// <summary>
/// İşlem geçmişini gerçek bir Excel dosyasına (.xlsx) çevirir. Ek kütüphane kullanmaz:
/// .xlsx aslında birkaç XML dosyası içeren bir zip'tir, burada en sade hâli elle yazılır.
/// CSV'den farkı: sayılar ve tarihler hücreye sayı olarak yazılır, bu yüzden Excel hangi
/// dilde/bölgede açılırsa açılsın sütunlar ve ondalıklar doğru görünür.
/// </summary>
public static class TransactionXlsxWriter
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    // styles.xml'deki hücre biçimleri (cellXfs sırası)
    private const int StyleHeader = 1;
    private const int StyleDate = 2;
    // Sayılar "General" biçimde kalır: Excel ondalık ayırıcıyı kendi bölge ayarına göre gösterir.
    private const int StyleNumber = 0;

    public static byte[] ToXlsxBytes(IEnumerable<TransactionDto> transactions, TransactionExportText text)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            Add(zip, "[Content_Types].xml", ContentTypesXml);
            Add(zip, "_rels/.rels", RootRelsXml);
            Add(zip, "xl/workbook.xml", WorkbookXml(text.SheetName));
            Add(zip, "xl/_rels/workbook.xml.rels", WorkbookRelsXml);
            Add(zip, "xl/styles.xml", StylesXml);
            Add(zip, "xl/worksheets/sheet1.xml", SheetXml(transactions, text));
        }

        return stream.ToArray();
    }

    private static string SheetXml(IEnumerable<TransactionDto> transactions, TransactionExportText text)
    {
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sb.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">");
        // Başlık satırı kaydırınca sabit kalsın.
        sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        sb.Append("<cols>")
          .Append("<col min=\"1\" max=\"1\" width=\"20\" customWidth=\"1\"/>")
          .Append("<col min=\"2\" max=\"3\" width=\"14\" customWidth=\"1\"/>")
          .Append("<col min=\"4\" max=\"6\" width=\"18\" customWidth=\"1\"/>")
          .Append("</cols>");
        sb.Append("<sheetData>");

        var row = 1;
        sb.Append($"<row r=\"{row}\">");
        for (var c = 0; c < text.Headers.Length; c++)
            AppendText(sb, CellRef(c, row), text.Headers[c], StyleHeader);
        sb.Append("</row>");

        foreach (var t in transactions)
        {
            row++;
            sb.Append($"<row r=\"{row}\">");
            AppendNumber(sb, CellRef(0, row), ToExcelDate(t.CreatedAt), StyleDate);
            AppendText(sb, CellRef(1, row), text.TypeLabel(t.Type), 0);
            AppendText(sb, CellRef(2, row), t.Symbol, 0);
            AppendNumber(sb, CellRef(3, row), t.Quantity, StyleNumber);
            AppendNumber(sb, CellRef(4, row), t.Price, StyleNumber);
            AppendNumber(sb, CellRef(5, row), t.TotalAmount, StyleNumber);
            sb.Append("</row>");
        }

        sb.Append("</sheetData></worksheet>");
        return sb.ToString();
    }

    private static void AppendText(StringBuilder sb, string cellRef, string value, int style)
    {
        sb.Append($"<c r=\"{cellRef}\" t=\"inlineStr\"{StyleAttr(style)}><is><t>")
          .Append(SecurityElement.Escape(value))
          .Append("</t></is></c>");
    }

    private static void AppendNumber(StringBuilder sb, string cellRef, decimal value, int style)
    {
        sb.Append($"<c r=\"{cellRef}\"{StyleAttr(style)}><v>")
          .Append(value.ToString(CultureInfo.InvariantCulture))
          .Append("</v></c>");
    }

    private static string StyleAttr(int style) => style == 0 ? "" : $" s=\"{style}\"";

    /// <summary>Sütun sırası (0 → A) ve satır numarasından hücre adresi, örn. (3, 2) → "D2".</summary>
    internal static string CellRef(int column, int row) => $"{(char)('A' + column)}{row}";

    /// <summary>Excel tarihleri 1899-12-30'dan bu yana geçen gün sayısıdır (kesirli kısım saat).</summary>
    /// Tick üzerinden decimal ile hesaplanır; double/8 hane yuvarlama 12:30:00'ı 12:29:59 gösterebiliyordu.
    internal static decimal ToExcelDate(DateTime value) =>
        (decimal)(value - new DateTime(1899, 12, 30)).Ticks / TimeSpan.TicksPerDay;

    private static void Add(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static string WorkbookXml(string sheetName) =>
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" " +
        "xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
        $"<sheets><sheet name=\"{SecurityElement.Escape(sheetName)}\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
        "</workbook>";

    private const string ContentTypesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
        "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
        "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
        "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
        "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
        "</Types>";

    private const string RootRelsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
        "</Relationships>";

    private const string WorkbookRelsXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
        "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
        "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
        "</Relationships>";

    // 0: normal, 1: kalın başlık, 2: tarih-saat
    private const string StylesXml =
        "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
        "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
        "<numFmts count=\"1\">" +
        "<numFmt numFmtId=\"164\" formatCode=\"yyyy-mm-dd hh:mm:ss\"/>" +
        "</numFmts>" +
        "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font>" +
        "<font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
        "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>" +
        "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
        "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
        "<cellXfs count=\"3\">" +
        "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
        "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>" +
        "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>" +
        "</cellXfs>" +
        "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
        "</styleSheet>";
}
