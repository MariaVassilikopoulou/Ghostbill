using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Ghostbill.Api.Models;

namespace Ghostbill.Api.Services;

public class CsvParsingService
{
    public List<Transaction> ParseTransactions(string filePath)
    {
        try
        {
            using var reader = new StreamReader(filePath, Encoding.GetEncoding("windows-1252"));
            reader.ReadLine();

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                BadDataFound = args =>
                {
                    var row = args.Context?.Parser?.Row ?? 0;
                    Console.WriteLine(
                        $"Warning: Failed to parse row {row}. Reason: Bad CSV data.");
                },
                ReadingExceptionOccurred = args =>
                {
                    Console.WriteLine(
                        $"Warning: Failed to parse row {args.Exception.Context?.Parser?.Row ?? 0}. Reason: {args.Exception.Message}");
                    return false;
                }
            };

            using var csv = new CsvReader(reader, config);
            csv.Context.RegisterClassMap<TransactionMap>();

            return csv.GetRecords<Transaction>().ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Failed to parse CSV file. Reason: {ex.Message}");
            return new List<Transaction>();
        }
    }

    private sealed class TransactionMap : ClassMap<Transaction>
    {
        public TransactionMap()
        {
            Map(x => x.Date).Name("Transaktionsdag");
            Map(x => x.Description).Name("Beskrivning");
            Map(x => x.Amount)
                .Name("Belopp")
                .TypeConverterOption.CultureInfo(CultureInfo.InvariantCulture);
        }
    }
}
