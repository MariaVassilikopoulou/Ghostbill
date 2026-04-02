using Ghostbill.Api.Models;
using Ghostbill.Api.Services;

namespace Ghostbill.Api.Parsing.Shared;

public sealed class RowMaterializationService
{
    private readonly ValueParsingService _valueParsingService;

    public RowMaterializationService(ValueParsingService valueParsingService)
    {
        _valueParsingService = valueParsingService;
    }

    public ParseResult MaterializeRows(List<string[]> rows, int headerIndex, ColumnMappingIndices mapping)
    {
        var transactions = new List<Transaction>();
        var skippedRows = 0;
        var skippedReasons = new List<string>();

        for (var i = headerIndex + 1; i < rows.Count; i++)
        {
            var row = rows[i];
            if (row.Length == 0 || row.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var maxIndex = Math.Max(mapping.DateIndex, Math.Max(mapping.DescriptionIndex, mapping.AmountIndex));
            if (row.Length <= maxIndex)
            {
                skippedRows++;
                skippedReasons.Add($"Skipped line {i + 1}: column mismatch");
                continue;
            }

            var description = row[mapping.DescriptionIndex]?.Trim() ?? string.Empty;
            var dateRaw = row[mapping.DateIndex]?.Trim() ?? string.Empty;
            var amountRaw = row[mapping.AmountIndex]?.Trim() ?? string.Empty;

            if (!_valueParsingService.TryParseDate(dateRaw, out var date))
            {
                skippedRows++;
                skippedReasons.Add($"Skipped line {i + 1}: invalid date '{dateRaw}'");
                continue;
            }

            if (!_valueParsingService.TryParseAmount(amountRaw, out var amount))
            {
                skippedRows++;
                skippedReasons.Add($"Skipped line {i + 1}: invalid amount '{amountRaw}'");
                continue;
            }

            if (amount >= 0)
            {
                continue;
            }

            transactions.Add(new Transaction
            {
                Description = description,
                Amount = amount,
                Date = date
            });
        }

        return new ParseResult
        {
            Transactions = transactions,
            SkippedRows = skippedRows,
            SkippedReasons = skippedReasons
        };
    }
}
