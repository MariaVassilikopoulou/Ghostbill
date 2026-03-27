using Ghostbill.Api.Models;
using Ghostbill.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghostbill.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<ActionResult<AnalysisResult>> Analyze(IFormFile csvFile)
    {
        if (csvFile is null)
        {
            return BadRequest();
        }

        var tempFilePath = Path.GetTempFileName();

        try
        {
            await using (var stream = System.IO.File.Create(tempFilePath))
            {
                await csvFile.CopyToAsync(stream);
            }

            var csvParsingService = new CsvParsingService();
            var recurrenceDetectionService = new RecurrenceDetectionService();

            var transactions = csvParsingService.ParseTransactions(tempFilePath);
            var recurringGroups = recurrenceDetectionService.DetectRecurringGroups(transactions);

            var ghosts = recurringGroups
                .Where(g => g.Category == ExpenseCategory.Ghost)
                .ToList();

            var regulars = recurringGroups
                .Where(g => g.Category == ExpenseCategory.Regular)
                .ToList();

            var result = new AnalysisResult
            {
                Ghosts = ghosts,
                Regulars = regulars,
                TotalTransactionsAnalyzed = transactions.Count,
                TotalMonthlyGhostCost = ghosts.Sum(g => Math.Abs(g.AverageAmount))
            };

            return Ok(result);
        }
        finally
        {
            if (System.IO.File.Exists(tempFilePath))
            {
                System.IO.File.Delete(tempFilePath);
            }
        }
    }
}
