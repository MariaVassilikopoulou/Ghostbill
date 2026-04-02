using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Ghostbill.Api.Controllers;
using Ghostbill.Api.Models;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Parsers;
using Ghostbill.Api.Parsing.Resolution;
using Ghostbill.Api.Parsing.Shared;
using Ghostbill.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Ghostbill.Api.Tests;

public sealed class TransactionsControllerParityTests
{
    [Fact]
    public async Task Analyze_WithIdenticalCsvAndXlsxData_ReturnsIdenticalAnalysisResult()
    {
        using var harness = CreateHarness();

        var csvBytes = BuildCsvBytes(
            [
                ["Date", "Description", "Amount"],
                ["2026-01-01", "Spotify", "-109.00"],
                ["2026-02-01", "Spotify", "-109.00"],
                ["2026-03-01", "Spotify", "-109.00"],
                ["2026-01-05", "ICA", "-350.50"],
                ["2026-01-25", "Salary", "30000.00"]
            ]);

        var xlsxBytes = BuildXlsxBytes(
            [
                ["Date", "Description", "Amount"],
                ["2026-01-01", "Spotify", "-109.00"],
                ["2026-02-01", "Spotify", "-109.00"],
                ["2026-03-01", "Spotify", "-109.00"],
                ["2026-01-05", "ICA", "-350.50"],
                ["2026-01-25", "Salary", "30000.00"]
            ]);

        var csvResult = await harness.Controller.Analyze(CreateFormFile(csvBytes, "data.csv", "text/csv"));
        var xlsxResult = await harness.Controller.Analyze(CreateFormFile(xlsxBytes, "data.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        var csvOk = Assert.IsType<OkObjectResult>(csvResult.Result);
        var xlsxOk = Assert.IsType<OkObjectResult>(xlsxResult.Result);

        var csvAnalysis = Assert.IsType<AnalysisResult>(csvOk.Value);
        var xlsxAnalysis = Assert.IsType<AnalysisResult>(xlsxOk.Value);

        AssertAnalysisResultEqual(csvAnalysis, xlsxAnalysis);
    }

    [Fact]
    public async Task Analyze_WithMalformedXlsx_ReturnsParseErrorCode()
    {
        using var harness = CreateHarness();

        var malformedBytes = Encoding.UTF8.GetBytes("this-is-not-an-xlsx-file");
        var response = await harness.Controller.Analyze(
            CreateFormFile(malformedBytes, "broken.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("PARSE_ERROR", ReadErrorCode(badRequest.Value));
    }

    [Fact]
    public async Task Analyze_WithEmptyFile_ReturnsInvalidFileCode()
    {
        using var harness = CreateHarness();

        var response = await harness.Controller.Analyze(
            CreateFormFile([], "empty.csv", "text/csv"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("INVALID_FILE", ReadErrorCode(badRequest.Value));
    }

    [Fact]
    public async Task Analyze_WithUnsupportedExtension_ReturnsUnsupportedFormatCode()
    {
        using var harness = CreateHarness();

        var bytes = Encoding.UTF8.GetBytes("some,data,to,trigger,parser\n");
        var response = await harness.Controller.Analyze(
            CreateFormFile(bytes, "data.txt", "text/plain"));

        var badRequest = Assert.IsType<BadRequestObjectResult>(response.Result);
        Assert.Equal("UNSUPPORTED_FORMAT", ReadErrorCode(badRequest.Value));
    }

    private static ControllerHarness CreateHarness()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var services = new ServiceCollection();
        services.AddScoped<CsvParsingService>();
        services.AddScoped<RecurrenceDetectionService>();
        services.AddScoped<HeaderDetectionService>();
        services.AddScoped<ColumnMappingService>();
        services.AddScoped<ValueParsingService>();
        services.AddScoped<RowMaterializationService>();
        services.AddScoped<ITransactionFileParser, CsvFileParserAdapter>();
        services.AddScoped<ITransactionFileParser, ExcelParsingService>();
        services.AddScoped<ParserResolutionService>();

        var provider = services.BuildServiceProvider();
        var scope = provider.CreateScope();

        var resolver = scope.ServiceProvider.GetRequiredService<ParserResolutionService>();
        resolver.ValidateConfiguration([".csv", ".xlsx"]);

        var recurrence = scope.ServiceProvider.GetRequiredService<RecurrenceDetectionService>();
        var controller = new TransactionsController(resolver, recurrence);

        return new ControllerHarness(provider, scope, controller);
    }

    private static IFormFile CreateFormFile(byte[] content, string fileName, string contentType)
    {
        var stream = new MemoryStream(content);
        return new FormFile(stream, 0, content.Length, "csvFile", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }

    private static byte[] BuildCsvBytes(IReadOnlyList<string[]> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(",", row.Select(EscapeCsv)));
        }

        return Encoding.UTF8.GetBytes(builder.ToString());
    }

    private static byte[] BuildXlsxBytes(IReadOnlyList<string[]> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Sheet1");

        for (var row = 0; row < rows.Count; row++)
        {
            for (var col = 0; col < rows[row].Length; col++)
            {
                worksheet.Cell(row + 1, col + 1).Value = rows[row][col];
            }
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    private static void AssertAnalysisResultEqual(AnalysisResult expected, AnalysisResult actual)
    {
        Assert.Equal(expected.SkippedRows, actual.SkippedRows);
        Assert.Equal(expected.TotalTransactionsAnalyzed, actual.TotalTransactionsAnalyzed);
        Assert.Equal(expected.TotalMonthlyGhostCost, actual.TotalMonthlyGhostCost);

        AssertTransactionsEqual(expected.Transactions, actual.Transactions);
        AssertGroupsEqual(expected.Ghosts, actual.Ghosts);
        AssertGroupsEqual(expected.Regulars, actual.Regulars);
    }

    private static void AssertTransactionsEqual(List<Transaction> expected, List<Transaction> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].Date, actual[i].Date);
            Assert.Equal(expected[i].Description, actual[i].Description);
            Assert.Equal(expected[i].Amount, actual[i].Amount);
        }
    }

    private static void AssertGroupsEqual(List<RecurringGroup> expected, List<RecurringGroup> actual)
    {
        Assert.Equal(expected.Count, actual.Count);
        for (var i = 0; i < expected.Count; i++)
        {
            Assert.Equal(expected[i].MerchantName, actual[i].MerchantName);
            Assert.Equal(expected[i].AverageAmount, actual[i].AverageAmount);
            Assert.Equal(expected[i].MonthlyAmount, actual[i].MonthlyAmount);
            Assert.Equal(expected[i].YearlyCost, actual[i].YearlyCost);
            Assert.Equal(expected[i].OccurrenceCount, actual[i].OccurrenceCount);
            Assert.Equal(expected[i].Category, actual[i].Category);
            AssertTransactionsEqual(expected[i].Transactions, actual[i].Transactions);
        }
    }

    private static string ReadErrorCode(object? value)
    {
        var json = JsonSerializer.Serialize(value);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("code").GetString() ?? string.Empty;
    }

    private sealed class ControllerHarness : IDisposable
    {
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;

        public ControllerHarness(ServiceProvider provider, IServiceScope scope, TransactionsController controller)
        {
            _provider = provider;
            _scope = scope;
            Controller = controller;
        }

        public TransactionsController Controller { get; }

        public void Dispose()
        {
            _scope.Dispose();
            _provider.Dispose();
        }
    }
}
