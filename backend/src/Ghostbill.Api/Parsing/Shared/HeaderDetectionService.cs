namespace Ghostbill.Api.Parsing.Shared;

public sealed class HeaderDetectionService
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
        "total", "kredit", "bokfort", "bokfört"
    ];

    public int FindHeaderIndex(List<string[]> rows, int maxScanLines = 10)
    {
        if (rows.Count == 0)
        {
            return -1;
        }

        var normalizedDateKeywords = DateKeywords.Select(NormalizeHeader).Distinct().ToArray();
        var normalizedDescriptionKeywords = DescriptionKeywords.Select(NormalizeHeader).Distinct().ToArray();
        var normalizedAmountKeywords = AmountKeywords.Select(NormalizeHeader).Distinct().ToArray();
        var allKeywords = normalizedDateKeywords
            .Concat(normalizedDescriptionKeywords)
            .Concat(normalizedAmountKeywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var scanLimit = Math.Min(maxScanLines, rows.Count);
        for (var i = 0; i < scanLimit; i++)
        {
            var row = rows[i];
            if (row.Length < 2)
            {
                continue;
            }

            var normalized = row
                .Select(NormalizeHeader)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .ToList();

            var hitCount = normalized.Count(header =>
                allKeywords.Any(keyword =>
                    header.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
                    keyword.Contains(header, StringComparison.OrdinalIgnoreCase)));

            var categoryHits = 0;
            if (normalized.Any(header => normalizedDateKeywords.Any(keyword => header.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
            {
                categoryHits++;
            }

            if (normalized.Any(header => normalizedDescriptionKeywords.Any(keyword => header.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
            {
                categoryHits++;
            }

            if (normalized.Any(header => normalizedAmountKeywords.Any(keyword => header.Contains(keyword, StringComparison.OrdinalIgnoreCase))))
            {
                categoryHits++;
            }

            if (hitCount >= 2 || categoryHits >= 3)
            {
                return i;
            }
        }

        return -1;
    }

    public string NormalizeHeader(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return string.Empty;
        }

        var normalized = header.Trim().ToLowerInvariant();
        normalized = normalized
            .Replace('å', 'a')
            .Replace('ä', 'a')
            .Replace('ö', 'o')
            .Replace('Å', 'a')
            .Replace('Ä', 'a')
            .Replace('Ö', 'o');

        var chars = normalized
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : ' ')
            .ToArray();

        return string.Join(' ', new string(chars)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", string.Empty);
    }
}
