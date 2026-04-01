namespace Ghostbill.Api.Models;

public class AnalysisResult
{
    public List<RecurringGroup> Ghosts { get; set; } = new();
    public List<RecurringGroup> Regulars { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
    public int SkippedRows { get; set; }
    public int TotalTransactionsAnalyzed { get; set; }
    public decimal TotalMonthlyGhostCost { get; set; }
}
