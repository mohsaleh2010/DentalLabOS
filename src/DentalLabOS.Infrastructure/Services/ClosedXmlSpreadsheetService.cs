using System.Globalization;
using ClosedXML.Excel;
using DentalLabOS.Application.Abstractions;
using DentalLabOS.Application.Common;

namespace DentalLabOS.Infrastructure.Services;

/// <summary>Reads and writes .xlsx with ClosedXML. Formulas are read as their cached values; nothing is executed.</summary>
public sealed class ClosedXmlSpreadsheetService : ISpreadsheetService
{
    public SheetData Read(Stream stream, int maxRows)
    {
        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(stream);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            throw ValidationException.For("File", "Import.FileInvalid");
        }

        using (workbook)
        {
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw ValidationException.For("File", "Import.Empty");
            var used = sheet.RangeUsed();
            if (used is null)
            {
                return new SheetData([], []);
            }

            var firstRow = used.FirstRow().RowNumber();
            var lastRow = used.LastRow().RowNumber();
            var lastCol = used.LastColumn().ColumnNumber();
            if (lastRow - firstRow > maxRows)
            {
                throw ValidationException.For("File", "Import.TooManyRows");
            }

            var headers = Enumerable.Range(1, lastCol).Select(c => Text(sheet.Cell(firstRow, c)) ?? string.Empty).ToList();
            var rows = new List<IReadOnlyList<string?>>();
            for (var r = firstRow + 1; r <= lastRow; r++)
            {
                rows.Add(Enumerable.Range(1, lastCol).Select(c => Text(sheet.Cell(r, c))).ToList());
            }

            return new SheetData(headers, rows);
        }
    }

    private static string? Text(IXLCell cell)
    {
        var value = cell.CachedValue;
        return value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Number => value.GetNumber().ToString(CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "true" : "false",
            XLDataType.DateTime => value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            XLDataType.Error => null,
            _ => value.ToString(CultureInfo.InvariantCulture).Trim(),
        };
    }

    public byte[] CreateTemplate(string sheetName, IReadOnlyList<string> headers, string? helpSheetName, IReadOnlyList<(string Column, string Help)> help)
    {
        using var workbook = new XLWorkbook();
        var rtl = headers.Any(h => h.Any(ch => ch is >= '؀' and <= 'ۿ'));
        var sheet = workbook.Worksheets.Add(sheetName);
        sheet.RightToLeft = rtl;
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = sheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F1F8");
            sheet.Column(i + 1).Width = Math.Max(14, headers[i].Length + 6);
            sheet.Column(i + 1).Style.NumberFormat.Format = "@";
        }

        sheet.SheetView.FreezeRows(1);

        if (helpSheetName is not null && help.Count > 0)
        {
            var helpSheet = workbook.Worksheets.Add(helpSheetName);
            helpSheet.RightToLeft = rtl;
            for (var i = 0; i < help.Count; i++)
            {
                helpSheet.Cell(i + 1, 1).Value = help[i].Column;
                helpSheet.Cell(i + 1, 1).Style.Font.Bold = true;
                helpSheet.Cell(i + 1, 2).Value = help[i].Help;
            }

            helpSheet.Column(1).Width = 26;
            helpSheet.Column(2).Width = 70;
        }

        using var output = new MemoryStream();
        workbook.SaveAs(output);
        return output.ToArray();
    }
}
