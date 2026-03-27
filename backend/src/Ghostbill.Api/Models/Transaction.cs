namespace Ghostbill.Api.Models;

public class Transaction
{
    public DateTime Date { get; set; }

    public string Description { get; set; } = string.Empty;

    public decimal Amount { get; set; }
}
