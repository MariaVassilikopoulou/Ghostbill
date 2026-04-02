using Ghostbill.Api.Parsing.Abstractions;

namespace Ghostbill.Api.Parsing.Resolution;

public sealed class ParserResolutionService
{
    private readonly IReadOnlyList<ITransactionFileParser> _parsers;

    public ParserResolutionService(IEnumerable<ITransactionFileParser> parsers)
    {
        _parsers = parsers.ToList();
    }

    public bool TryResolve(string extension, out ITransactionFileParser? parser)
    {
        var normalizedExtension = NormalizeExtension(extension);
        var matches = _parsers
            .Where(p => p.CanHandle(normalizedExtension))
            .ToList();

        if (matches.Count == 0)
        {
            parser = null;
            return false;
        }

        if (matches.Count > 1)
        {
            throw new InvalidOperationException(
                $"Parser configuration error: multiple parsers match extension '{normalizedExtension}'.");
        }

        parser = matches[0];
        return true;
    }

    public void ValidateConfiguration(IEnumerable<string> extensions)
    {
        foreach (var extension in extensions)
        {
            var normalizedExtension = NormalizeExtension(extension);
            var matches = _parsers
                .Count(p => p.CanHandle(normalizedExtension));

            if (matches != 1)
            {
                throw new InvalidOperationException(
                    $"Parser configuration error for extension '{normalizedExtension}': expected exactly one match, found {matches}.");
            }
        }
    }

    private static string NormalizeExtension(string extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var trimmed = extension.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal)
            ? trimmed.ToLowerInvariant()
            : "." + trimmed.ToLowerInvariant();
    }
}
