using System.Globalization;
using System.Text;
using Ghostbill.Api.Models;

namespace Ghostbill.Api.Services;

public class ParseResult
{
    public List<Transaction> Transactions { get; set; } = new();
    public int SkippedRows { get; set; }
}

public class CsvParsingService
{
    public ParseResult ParseTransactions(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.GetEncoding("windows-1252")).ToList();
        if (lines.Count == 0)
        {
            return new ParseResult();
        }

        var headerIndex = lines.FindIndex(line =>
            line.Contains("Beskrivning", StringComparison.OrdinalIgnoreCase) ||
            line.Contains("Description", StringComparison.OrdinalIgnoreCase));

        if (headerIndex == -1)
        {
            throw new NotSupportedException("Unsupported CSV format");
        }

        var headerLine = lines[headerIndex];

        var delimiter = headerLine.Count(c => c == ';') > headerLine.Count(c => c == ',')
            ? ';'
            : ',';

        var isSwedbank =
            headerLine.Contains("Beskrivning", StringComparison.OrdinalIgnoreCase) &&
            headerLine.Contains("Belopp", StringComparison.OrdinalIgnoreCase);

        var isGeneric =
            headerLine.Contains("Description", StringComparison.OrdinalIgnoreCase) &&
            headerLine.Contains("Amount", StringComparison.OrdinalIgnoreCase);

        if (isSwedbank)
        {
            return ParseSwedbank(lines, headerIndex, delimiter);
        }

        if (isGeneric)
        {
            return ParseGeneric(lines, headerIndex, delimiter);
        }

        throw new NotSupportedException("Unsupported CSV format");
    }

    public ParseResult ParseSwedbank(List<string> lines, int headerIndex, char delimiter)
    {
        var headerColumns = SplitCsvLine(lines[headerIndex], delimiter);

        var descriptionIndex = FindColumnIndex(headerColumns, ["Beskrivning", "Text"]);
        var amountIndex = FindColumnIndex(headerColumns, ["Belopp"]);
        var dateIndex = FindColumnIndex(headerColumns, ["Bokföringsdag", "Datum"]);

        if (descriptionIndex == -1 || amountIndex == -1 || dateIndex == -1)
        {
            throw new NotSupportedException("Unsupported CSV format");
        }

        return ParseRows(lines, headerIndex, delimiter, descriptionIndex, amountIndex, dateIndex);
    }

    public ParseResult ParseGeneric(List<string> lines, int headerIndex, char delimiter)
    {
        var headerColumns = SplitCsvLine(lines[headerIndex], delimiter);

        var descriptionIndex = FindColumnIndex(headerColumns, ["Description", "Name", "Text"]);
        var amountIndex = FindColumnIndex(headerColumns, ["Amount", "Debit", "Withdrawal"]);
        var dateIndex = FindColumnIndex(headerColumns, ["Date", "Transaction Date", "Posting Date"]);

        if (descriptionIndex == -1 || amountIndex == -1 || dateIndex == -1)
        {
            throw new NotSupportedException("Unsupported CSV format");
        }

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
        var transactions = new List<Transaction>();

        for (var i = headerIndex + 1; i < lines.Count; i++)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var columns = SplitCsvLine(line, delimiter);
            var maxIndex = Math.Max(descriptionIndex, Math.Max(amountIndex, dateIndex));
            if (columns.Count <= maxIndex)
            {
                skippedRows++;
                continue;
            }

            var description = columns[descriptionIndex].Trim();
            var amountRaw = columns[amountIndex].Trim();
            var dateRaw = columns[dateIndex].Trim();

            if (!TryParseDate(dateRaw, out var date))
            {
                skippedRows++;
                 Console.WriteLine($"Warning: Skipped row {i + 1} — invalid date '{dateRaw}'");
                continue;
            }

            if (!TryParseAmount(amountRaw, out var amount))
            {
                skippedRows++;
                 Console.WriteLine($"Warning: Skipped row {i + 1} — invalid date '{dateRaw}'");
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

        return new ParseResult
        {
            Transactions = transactions,
            SkippedRows = skippedRows
        };
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

    private static int FindColumnIndex(List<string> headers, string[] possibleNames)
    {
        return headers.FindIndex(header =>
            possibleNames.Any(name => header.Contains(name, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date))
        {
            return true;
        }

        return DateTime.TryParse(value, CultureInfo.CurrentCulture, DateTimeStyles.None, out date);
    }

    private static bool TryParseAmount(string value, out decimal amount)
    {
        var normalized = value.Replace(" ", string.Empty);

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out amount))
        {
            return true;
        }

        return decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.GetCultureInfo("sv-SE"), out amount);
    }
}
