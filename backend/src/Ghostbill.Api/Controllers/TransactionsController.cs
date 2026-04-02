using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Resolution;
using Ghostbill.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Ghostbill.Api.Controllers;

[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private const long MaxFileSizeBytes = 5 * 1024 * 1024;
    private readonly ParserResolutionService _parserResolutionService;
    private readonly RecurrenceDetectionService _recurrenceDetectionService;

    public TransactionsController(
        ParserResolutionService parserResolutionService,
        RecurrenceDetectionService recurrenceDetectionService)
    {
        _parserResolutionService = parserResolutionService;
        _recurrenceDetectionService = recurrenceDetectionService;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult<AnalysisResult>> Analyze(IFormFile csvFile)
    {
        if (csvFile is null || csvFile.Length == 0)
        {
            return BadRequest(CreateError("Missing or empty file.", "INVALID_FILE"));
        }

        if (csvFile.Length > MaxFileSizeBytes)
        {
            return BadRequest(CreateError("File exceeds the 5 MB limit.", "INVALID_FILE"));
        }

        var extension = Path.GetExtension(csvFile.FileName);
        if (!_parserResolutionService.TryResolve(extension, out var parser) || parser is null)
        {
            return BadRequest(CreateError("Unsupported file format.", "UNSUPPORTED_FORMAT"));
        }

        var tempFilePath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + extension);

        try
        {
            await using (var stream = System.IO.File.Create(tempFilePath))
            {
                await csvFile.CopyToAsync(stream);
            }

            ParseResult parseResult;
            try
            {
                parseResult = parser.Parse(tempFilePath);
            }
            catch (Exception ex)
            {
                return BadRequest(CreateError("File parsing failed.", "PARSE_ERROR", ex.Message));
            }

            var transactions = parseResult.Transactions ?? new List<Transaction>();

            if (transactions.Count == 0)
            {
                return Ok(new AnalysisResult
                {
                    Ghosts = new List<RecurringGroup>(),
                    Regulars = new List<RecurringGroup>(),
                    Transactions = transactions,
                    SkippedRows = parseResult.SkippedRows,
                    TotalTransactionsAnalyzed = 0,
                    TotalMonthlyGhostCost = 0m
                });
            }

            var recurringGroups = _recurrenceDetectionService.DetectRecurringGroups(transactions);

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
                Transactions = transactions,
                SkippedRows = parseResult.SkippedRows,
                TotalTransactionsAnalyzed = transactions.Count,
                TotalMonthlyGhostCost = ghosts.Sum(g => Math.Abs(g.AverageAmount))
            };

            return Ok(result);
        }
        finally
{
    if (System.IO.File.Exists(tempFilePath))
    {
        int attempts = 0;
        const int maxAttempts = 3;
        const int delayMs = 50;

        while (attempts < maxAttempts)
        {
            try
            {
                System.IO.File.Delete(tempFilePath);
                break; 
            }
            catch (IOException)
            {
                attempts++;
                if (attempts >= maxAttempts) break; 
                Thread.Sleep(delayMs); 
            }
        }
    }
}
    }


    private static object CreateError(string message, string code, string? details = null)
    {
        return details is null
            ? new { message, code }
            : new { message, code, details };
    }
}
