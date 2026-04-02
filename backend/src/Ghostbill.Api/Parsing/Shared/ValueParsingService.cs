using System.Globalization;

namespace Ghostbill.Api.Parsing.Shared;

public sealed class ValueParsingService
{
    private static readonly string[] SupportedDateFormats =
    [
        "yyyy-MM-dd",
        "yyyy/MM/dd",
        "dd/MM/yyyy",
        "d/M/yyyy",
        "MM/dd/yyyy",
        "M/d/yyyy",
        "dd-MM-yyyy",
        "d-M-yyyy",
        "yyyyMMdd",
        "dd.MM.yyyy",
        "d.MM.yyyy"
    ];

    public bool TryParseDate(string value, out DateTime date)
    {
        if (DateTime.TryParseExact(
                value,
                SupportedDateFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out date))
        {
            return true;
        }

        if (DateTime.TryParseExact(
                value,
                SupportedDateFormats,
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

    public bool TryParseAmount(string value, out decimal amount)
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
}
