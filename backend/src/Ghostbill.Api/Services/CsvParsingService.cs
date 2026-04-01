using System.Globalization;
using System.Text;
using Ghostbill.Api.Models;

namespace Ghostbill.Api.Services;

public class ParseResult
{
    public List<Transaction> Transactions { get; set; } = new();
    public int SkippedRows { get; set; }
    public List<string> SkippedReasons { get; set; } = new();
}

public class CsvParsingService
{
    public ParseResult ParseTransactions(string filePath)
    {
        var lines = ReadAllLinesWithEncoding(filePath);
        if (lines.Count == 0)
        {
            return new ParseResult();
        }

        var delimiter = DetectDelimiter(lines);
        var headerIndex = FindHeaderIndex(lines, delimiter);
        var headerColumns = SplitCsvLine(lines[headerIndex], delimiter);
        Console.WriteLine($"Header line: {headerIndex + 1}");
        Console.WriteLine($"Header: [{string.Join(", ", headerColumns.Take(10))}]");

        var (dateIndex, descriptionIndex, amountIndex) = MapColumns(headerColumns);

        Console.WriteLine($"Mapped: date={dateIndex}, desc={descriptionIndex}, amount={amountIndex}");

        return ParseRows(lines, headerIndex, delimiter, descriptionIndex, amountIndex, dateIndex);
    }

    private static ParseResult ParseRows(
        List<string> lines,
        int headerIndex,
        char delimiter,
        int descriptionIndex,
        int amountIndex,
        int dateIndex)
    {
        var skippedRows = 0;
        var skippedReasons = new List<string>();
        var transactions = new List<Transaction>();
        var processedRows = 0;

        for (var i = headerIndex + 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            processedRows++;
            var startLine = i + 1;
            var record = new StringBuilder(lines[i]);

            while (!HasBalancedQuotes(record.ToString()) && i + 1 < lines.Count)
            {
                i++;
                record.Append('\n');
                record.Append(lines[i]);
            }

            if (!HasBalancedQuotes(record.ToString()))
            {
                skippedRows++;
                var reason = $"Skipped line {startLine}: unbalanced quotes | raw='{record}'";
                skippedReasons.Add(reason);
                Console.WriteLine(reason);
                continue;
            }

            var line = record.ToString();
            var columns = SplitCsvLine(line, delimiter);
            var maxIndex = Math.Max(descriptionIndex, Math.Max(amountIndex, dateIndex));
            if (columns.Count <= maxIndex)
            {
                skippedRows++;
                var reason =
                    $"Skipped line {startLine}: column mismatch (expected at least {maxIndex + 1}, got {columns.Count}) | raw='{line}'";
                skippedReasons.Add(reason);
                Console.WriteLine(reason);
                continue;
            }

            var description = columns[descriptionIndex].Trim();
            var amountRaw = columns[amountIndex].Trim();
            var dateRaw = columns[dateIndex].Trim();

            if (!TryParseDate(dateRaw, out var date))
            {
                skippedRows++;
                var reason = $"Skipped line {startLine}: invalid date '{dateRaw}' | raw='{line}'";
                skippedReasons.Add(reason);
                Console.WriteLine(reason);
                continue;
            }

            if (!TryParseAmount(amountRaw, out var amount))
            {
                skippedRows++;
                var reason = $"Skipped line {startLine}: invalid amount '{amountRaw}' | raw='{line}'";
                skippedReasons.Add(reason);
                Console.WriteLine(reason);
                continue;
            }

            if (amount >= 0)
            {
                continue;
            }

            transactions.Add(new Transaction
            {
                Description = description.Trim(),
                Amount = amount,
                Date = date
            });
        }

        Console.WriteLine($"Processed {processedRows} rows, skipped {skippedRows}");

        return new ParseResult
        {
            Transactions = transactions,
            SkippedRows = skippedRows,
            SkippedReasons = skippedReasons
        };
    }

    private static List<string> ReadAllLinesWithEncoding(string filePath)
    {
        var bytes = File.ReadAllBytes(filePath);
        if (bytes.Length == 0)
        {
            return new List<string>();
        }

        var encodings = new Encoding[]
        {
            new UTF8Encoding(true, true),
            new UTF8Encoding(false, true),
            Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
        };

        foreach (var encoding in encodings)
        {
            try
            {
                var content = encoding.GetString(bytes);
                var lines = SplitLines(content);
                if (lines.Count > 0)
                {
                    lines[0] = lines[0].TrimStart('\uFEFF');
                }

                return lines;
            }
            catch (DecoderFallbackException)
            {
                // Try next encoding candidate.
            }
        }

        throw new NotSupportedException("Unable to decode CSV file");
    }

    private static List<string> SplitLines(string content)
    {
        var lines = new List<string>();
        using var reader = new StringReader(content);

        while (reader.ReadLine() is { } line)
        {
            lines.Add(line);
        }

        return lines;
    }

    private static int FindHeaderIndex(List<string> lines, char delimiter)
    {
        var dateKeywords = new[]
        {
            "date", "datum", "bokforingsdag", "transaction", "posting", "transaktions", "bokf",
            "transaktionsdatum", "transaktionsdag", "valutadag", "bokföringsdag"
        };
        var descriptionKeywords = new[]
        {
            "description", "beskrivning", "text", "name", "merchant", "recipient", "payee", "transaktion",
            "avsandare", "referens", "transactiontext"
        };
        var amountKeywords = new[]
        {
            "amount", "belopp", "debit", "withdrawal", "sum", "kostnad", "utgift", "balance", "saldo",
            "total", "kredit", "bokfort", "bokfört"
        };
        var normalizedDateKeywords = dateKeywords.Select(NormalizeHeader).Distinct().ToArray();
        var normalizedDescriptionKeywords = descriptionKeywords.Select(NormalizeHeader).Distinct().ToArray();
        var normalizedAmountKeywords = amountKeywords.Select(NormalizeHeader).Distinct().ToArray();
        var allKeywords = normalizedDateKeywords
            .Concat(normalizedDescriptionKeywords)
            .Concat(normalizedAmountKeywords)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        for (var i = 0; i < Math.Min(10, lines.Count); i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = SplitCsvLine(line, delimiter);
            if (columns.Count < 2)
            {
                continue;
            }

            var normalized = columns
                .Select(NormalizeHeader)
                .Where(s => !string.IsNullOrWhiteSpace(s))
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

        throw new NotSupportedException("No valid header found");
    }

    private static char DetectDelimiter(List<string> lines)
    {
        var sampleLines = lines
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(15)
            .ToList();

        if (sampleLines.Count == 0)
        {
            return ';';
        }

        var candidates = new[] { ';', ',', '\t', '|' };
        char bestDelimiter = ';';
        var bestScore = int.MinValue;
        var bestOccurrences = -1;
        var bestConsistentColumns = 0;

        foreach (var candidate in candidates)
        {
            var columnCounts = sampleLines
                .Select(line => SplitCsvLine(line, candidate).Count)
                .ToList();

            var countsAboveOne = columnCounts.Where(count => count > 1).ToList();
            var consistencyScore = 0;
            var consistentColumns = 0;
            if (countsAboveOne.Count > 0)
            {
                var bestGroup = countsAboveOne
                    .GroupBy(count => count)
                    .OrderByDescending(group => group.Count())
                    .ThenByDescending(group => group.Key)
                    .First();

                consistencyScore = bestGroup.Count();
                consistentColumns = bestGroup.Key;
            }

            var occurrences = sampleLines.Sum(line => line.Count(ch => ch == candidate));
            var score = (consistencyScore * 1000) + occurrences;

            if (score > bestScore ||
                (score == bestScore && occurrences > bestOccurrences) ||
                (score == bestScore && occurrences == bestOccurrences && Array.IndexOf(candidates, candidate) < Array.IndexOf(candidates, bestDelimiter)))
            {
                bestDelimiter = candidate;
                bestScore = score;
                bestOccurrences = occurrences;
                bestConsistentColumns = consistentColumns;
            }
        }

        Console.WriteLine($"Delimiter: '{bestDelimiter}' ({bestConsistentColumns} cols consistent)");
        return bestDelimiter;
    }

    private static (int DateIndex, int DescriptionIndex, int AmountIndex) MapColumns(List<string> headerColumns)
    {
        Console.WriteLine($"Raw headers: [{string.Join(", ", headerColumns)}]");

        var normalizedHeaders = headerColumns
            .Select(NormalizeHeader)
            .ToList();

        var dateKeywords = new[]
        {
            "date", "datum", "bokforingsdag", "transaction", "posting", "transaktions", "bokf",
            "transaktionsdatum", "transaktionsdag", "valutadag", "bokföringsdag"
        };
        var descriptionKeywords = new[]
        {
            "description", "beskrivning", "text", "name", "merchant", "recipient", "payee", "transaktion",
            "avsandare", "referens", "transactiontext"
        };
        var amountKeywords = new[]
        {
            "amount", "belopp", "debit", "withdrawal", "sum", "kostnad", "utgift", "balance", "saldo",
            "total", "debit", "kredit", "bokfort", "bokfört"
        };

        var dateIndex = FindBestColumnMatch(normalizedHeaders, dateKeywords);
        var descriptionIndex = FindBestColumnMatch(normalizedHeaders, descriptionKeywords);
        var amountIndex = FindBestColumnMatch(normalizedHeaders, amountKeywords);

        if (dateIndex == -1 || descriptionIndex == -1 || amountIndex == -1)
        {
            throw new NotSupportedException("Unsupported CSV format: required columns missing (date/description/amount)");
        }

        return (dateIndex, descriptionIndex, amountIndex);
    }

    private static int FindBestColumnMatch(List<string> normalizedHeaders, string[] keywords)
    {
        var normalizedKeywords = keywords
            .Select(NormalizeHeader)
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

    private static string NormalizeHeader(string header)
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

        var builder = new StringBuilder(normalized.Length);
        var previousWasSpace = false;

        foreach (var ch in normalized)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousWasSpace = false;
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                if (!previousWasSpace)
                {
                    builder.Append(' ');
                    previousWasSpace = true;
                }
            }
        }

        return builder.ToString().Trim().Replace(" ", string.Empty);
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];

            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == delimiter && !inQuotes)
            {
                values.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        values.Add(current.ToString().Trim());
        return values;
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        var formats = new[]
        {
            "yyyy-MM-dd", "yyyy/MM/dd", "dd/MM/yyyy", "d/M/yyyy", "MM/dd/yyyy", "M/d/yyyy", "dd-MM-yyyy",
            "d-M-yyyy", "yyyyMMdd", "dd.MM.yyyy", "d.MM.yyyy"
        };

        if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
        {
            return true;
        }

        if (DateTime.TryParseExact(
                value,
                formats,
                CultureInfo.CurrentCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
        {
            return true;
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        amount = 0m;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("\u00A0", string.Empty)
            .Replace("kr", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("sek", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("$", string.Empty)
            .Replace("€", string.Empty)
            .Replace("£", string.Empty);

        if (normalized.EndsWith("-", StringComparison.Ordinal))
        {
            normalized = "-" + normalized[..^1];
        }

        if (normalized.StartsWith("(") && normalized.EndsWith(")"))
        {
            normalized = "-" + normalized[1..^1];
        }

        normalized = new string(normalized
            .Where(ch => char.IsDigit(ch) || ch is '.' or ',' or '-' or '+')
            .ToArray());

        var candidates = new List<string> { normalized };

        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            var lastComma = normalized.LastIndexOf(',');
            var lastDot = normalized.LastIndexOf('.');
            var decimalSeparator = lastComma > lastDot ? ',' : '.';
            var thousandSeparator = decimalSeparator == ',' ? '.' : ',';

            var invariantCandidate = normalized.Replace(thousandSeparator.ToString(), string.Empty);
            if (decimalSeparator == ',')
            {
                invariantCandidate = invariantCandidate.Replace(',', '.');
            }

            candidates.Add(invariantCandidate);
        }
        else if (normalized.Contains(','))
        {
            candidates.Add(normalized.Replace(',', '.'));
        }

        foreach (var candidate in candidates.Distinct())
        {
            if (decimal.TryParse(candidate, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
            {
                return true;
            }

            if (decimal.TryParse(candidate, NumberStyles.Any, CultureInfo.GetCultureInfo("sv-SE"), out amount))
            {
                return true;
            }

            if (decimal.TryParse(candidate, NumberStyles.Any, CultureInfo.CurrentCulture, out amount))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasBalancedQuotes(string input)
    {
        var inQuotes = false;

        for (var i = 0; i < input.Length; i++)
        {
            if (input[i] != '"')
            {
                continue;
            }

            if (inQuotes && i + 1 < input.Length && input[i + 1] == '"')
            {
                i++;
                continue;
            }

            inQuotes = !inQuotes;
        }

        return !inQuotes;
    }
}
