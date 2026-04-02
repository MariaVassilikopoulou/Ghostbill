using ClosedXML.Excel;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Shared;
using Ghostbill.Api.Services;

namespace Ghostbill.Api.Parsing.Parsers;

public sealed class ExcelParsingService : ITransactionFileParser
{
    private readonly HeaderDetectionService _headerDetectionService;
    private readonly ColumnMappingService _columnMappingService;
    private readonly RowMaterializationService _rowMaterializationService;

    public ExcelParsingService(
        HeaderDetectionService headerDetectionService,
        ColumnMappingService columnMappingService,
        RowMaterializationService rowMaterializationService)
    {
        _headerDetectionService = headerDetectionService;
        _columnMappingService = columnMappingService;
        _rowMaterializationService = rowMaterializationService;
    }

    public bool CanHandle(string extension)
    {
        return string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase);
    }

    public ParseResult Parse(string filePath)
{
    List<string[]> rows;
    using (var workbook = new XLWorkbook(filePath))
    {
        var worksheet = workbook.Worksheets.FirstOrDefault();
        if (worksheet == null) return new ParseResult();

        rows = ReadRowsFromWorksheet(worksheet); // fully materialized
    } // workbook disposed here, handle released

    if (rows.Count == 0) return new ParseResult();

    var headerIndex = _headerDetectionService.FindHeaderIndex(rows);
    if (headerIndex < 0)
        throw new NotSupportedException("No valid header found");

    var headers = rows[headerIndex];
    if (!_columnMappingService.TryMapColumns(headers, out var mapping))
        throw new NotSupportedException(
            "Unsupported CSV format: required columns missing (date/description/amount)");

    return _rowMaterializationService.MaterializeRows(rows, headerIndex, mapping);
}
    private static List<string[]> ReadRowsFromWorksheet(IXLWorksheet worksheet)
    {
        var usedRange = worksheet.RangeUsed();
        if (usedRange is null)
        {
            return [];
        }

        var firstRow = usedRange.RangeAddress.FirstAddress.RowNumber;
        var lastRow = usedRange.RangeAddress.LastAddress.RowNumber;
        var firstColumn = usedRange.RangeAddress.FirstAddress.ColumnNumber;
        var lastColumn = usedRange.RangeAddress.LastAddress.ColumnNumber;

        var rows = new List<string[]>(lastRow - firstRow + 1);
        for (var rowNumber = firstRow; rowNumber <= lastRow; rowNumber++)
        {
            var values = new string[lastColumn - firstColumn + 1];

            for (var columnNumber = firstColumn; columnNumber <= lastColumn; columnNumber++)
            {
                var cell = worksheet.Cell(rowNumber, columnNumber);

                values[columnNumber - firstColumn] = cell.DataType switch
                {
                    XLDataType.DateTime when cell.TryGetValue<DateTime>(out var dateTimeValue) =>
                        dateTimeValue.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),

                    XLDataType.Number when cell.TryGetValue<double>(out var numericValue) =>
                        numericValue.ToString(System.Globalization.CultureInfo.InvariantCulture),

                    _ => cell.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                };
            }

            rows.Add(values);
        }

        return rows;
    }
}
