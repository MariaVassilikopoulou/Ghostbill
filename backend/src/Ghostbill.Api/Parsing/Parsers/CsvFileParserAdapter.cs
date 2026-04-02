using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Services;

namespace Ghostbill.Api.Parsing.Parsers;

public sealed class CsvFileParserAdapter : ITransactionFileParser
{
    private readonly CsvParsingService _csvParsingService;

    public CsvFileParserAdapter(CsvParsingService csvParsingService)
    {
        _csvParsingService = csvParsingService;
    }

    public bool CanHandle(string extension)
    {
        return string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase);
    }

    public ParseResult Parse(string filePath)
    {
        return _csvParsingService.ParseTransactions(filePath);
    }
}
