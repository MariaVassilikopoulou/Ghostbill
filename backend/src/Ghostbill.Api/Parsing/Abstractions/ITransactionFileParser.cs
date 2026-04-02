using Ghostbill.Api.Services;

namespace Ghostbill.Api.Parsing.Abstractions;

public interface ITransactionFileParser
{
    bool CanHandle(string extension);
    ParseResult Parse(string filePath);
}
