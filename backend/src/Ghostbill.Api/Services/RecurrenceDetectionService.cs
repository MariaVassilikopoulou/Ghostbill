using System.Text.RegularExpressions;
using Ghostbill.Api.Models;

namespace Ghostbill.Api.Services;

public class RecurrenceDetectionService
{
    public List<RecurringGroup> DetectRecurringGroups(List<Transaction> transactions)
    {// Only consider outgoing payments (subscriptions, bills, etc.)
    transactions = transactions
    .Where(t => t.Amount < 0)
    .ToList();

    var blockedKeywords = new[]
{
    "swish",
    "överföring",
    "insättning",
    "uttag"
};

transactions = transactions
    .Where(t => !blockedKeywords.Any(k => t.Description.ToLower().Contains(k)))
    .ToList();
        return transactions
            .GroupBy(t => NormalizeDescription(t.Description))
            .Select(group =>
            {
                var orderedTransactions = group.OrderBy(t => t.Date).ToList();
                var occurrenceCount = orderedTransactions.Count;
                var averageAmount = orderedTransactions.Average(t => t.Amount);

                var isFrequent = occurrenceCount >= 3;
                var isRegular = IsRegular(orderedTransactions);
                var isAmountConsistent = IsAmountConsistent(orderedTransactions, averageAmount);

                var category = (isFrequent, isRegular, isAmountConsistent) switch
                {
                    (true, true, true) => ExpenseCategory.Ghost,
                    (true, true, false) => ExpenseCategory.Regular,
                    _ => ExpenseCategory.Noise
                };

                return new RecurringGroup
                {
                    MerchantName = group.Key,
                    Transactions = orderedTransactions,
                    AverageAmount = averageAmount,
                    OccurrenceCount = occurrenceCount,
                    Category = category
                };
            })//Currently the service filters out Noise intentionally to keep the output focused. For full spending visibility we'd return all groups and let the caller filter."
            .Where(g => g.Category is ExpenseCategory.Ghost or ExpenseCategory.Regular)
            .ToList();
    }

    private static string NormalizeDescription(string description)
    {
        var normalized = description.ToLowerInvariant().Trim();
        normalized = Regex.Replace(normalized, @"[^\p{L}\s]", " ");
        normalized = Regex.Replace(normalized, @"\s+", " ").Trim();

        var words = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", words.Take(2));
    }

    private static bool IsRegular(List<Transaction> transactions)
    {
        if (transactions.Count < 3)
        {
            return false;
        }

        var gaps = new List<double>();
        for (var i = 1; i < transactions.Count; i++)
        {
            gaps.Add((transactions[i].Date - transactions[i - 1].Date).TotalDays);
        }

        bool MatchesPattern(double min, double max, double tolerance)
        {
            var matching = gaps.Count(g => g >= min - tolerance && g <= max + tolerance);
            return matching >= (int)Math.Ceiling(gaps.Count * 0.7);
        }

        return MatchesPattern(25, 35, 4) || MatchesPattern(13, 15, 2);
    }

    private static bool IsWithinRange(double value, double min, double max, double tolerance)
    {
        return value >= (min - tolerance) && value <= (max + tolerance);
    }

    private static bool IsAmountConsistent(List<Transaction> transactions, decimal averageAmount)
{
    var percentageThreshold = Math.Abs(averageAmount) * 0.05m;
    var absoluteThreshold = 20m;
    var threshold = Math.Min(percentageThreshold, absoluteThreshold);
    return transactions.All(t => Math.Abs(t.Amount - averageAmount) <= threshold);
}
}
