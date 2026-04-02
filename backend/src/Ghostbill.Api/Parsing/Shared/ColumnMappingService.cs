namespace Ghostbill.Api.Parsing.Shared;

public sealed class ColumnMappingService
{
    private static readonly string[] DateKeywords =
    [
        "date", "datum", "bokforingsdag", "transaction", "posting", "transaktions", "bokf",
        "transaktionsdatum", "transaktionsdag", "valutadag", "bokföringsdag"
    ];

    private static readonly string[] DescriptionKeywords =
    [
        "description", "beskrivning", "text", "name", "merchant", "recipient", "payee", "transaktion",
        "avsandare", "referens", "transactiontext"
    ];

    private static readonly string[] AmountKeywords =
    [
        "amount", "belopp", "debit", "withdrawal", "sum", "kostnad", "utgift", "balance", "saldo",
        "total", "debit", "kredit", "bokfort", "bokfört"
    ];

    private readonly HeaderDetectionService _headerDetectionService;

    public ColumnMappingService(HeaderDetectionService headerDetectionService)
    {
        _headerDetectionService = headerDetectionService;
    }

    public bool TryMapColumns(string[] headers, out ColumnMappingIndices mapping)
    {
        var normalizedHeaders = headers
            .Select(_headerDetectionService.NormalizeHeader)
            .ToList();

        var dateIndex = FindBestColumnMatch(normalizedHeaders, DateKeywords);
        var descriptionIndex = FindBestColumnMatch(normalizedHeaders, DescriptionKeywords);
        var amountIndex = FindBestColumnMatch(normalizedHeaders, AmountKeywords);

        if (dateIndex == -1 || descriptionIndex == -1 || amountIndex == -1)
        {
            mapping = default;
            return false;
        }

        mapping = new ColumnMappingIndices(dateIndex, descriptionIndex, amountIndex);
        return true;
    }

    private int FindBestColumnMatch(List<string> normalizedHeaders, string[] keywords)
    {
        var normalizedKeywords = keywords
            .Select(_headerDetectionService.NormalizeHeader)
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Distinct()
            .ToArray();

        var bestIndex = -1;
        var bestScore = int.MinValue;

        for (var i = 0; i < normalizedHeaders.Count; i++)
        {
            var header = normalizedHeaders[i];
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            var score = 0;
            foreach (var keyword in normalizedKeywords)
            {
                if (string.Equals(header, keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 300);
                    continue;
                }

                if (header.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score = Math.Max(score, 200 + keyword.Length);
                    continue;
                }

                if (keyword.Contains(header, StringComparison.OrdinalIgnoreCase) && header.Length >= 4)
                {
                    score = Math.Max(score, 120 + header.Length);
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        return bestScore > 0 ? bestIndex : -1;
    }
}

public readonly record struct ColumnMappingIndices(int DateIndex, int DescriptionIndex, int AmountIndex);
