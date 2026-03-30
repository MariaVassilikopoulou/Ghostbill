namespace Ghostbill.Api.Models;

public class RecurringGroup
{
    public string MerchantName { get; set; } = string.Empty;
    public List<Transaction> Transactions { get; set; } = new();
    public decimal AverageAmount { get; set; }
    public decimal MonthlyAmount { get; set; }
    public decimal YearlyCost { get; set; }
    public int OccurrenceCount { get; set; }
    public ExpenseCategory Category { get; set; }
}
