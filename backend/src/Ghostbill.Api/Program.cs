using System.Text;
using System.Text.Json.Serialization;
using Ghostbill.Api.Parsing.Abstractions;
using Ghostbill.Api.Parsing.Parsers;
using Ghostbill.Api.Parsing.Resolution;
using Ghostbill.Api.Parsing.Shared;
using Ghostbill.Api.Services;

var builder = WebApplication.CreateBuilder(args);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Add services to the container
builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters
            .Add(new JsonStringEnumConverter()));

builder.Services.AddScoped<CsvParsingService>();
builder.Services.AddScoped<RecurrenceDetectionService>();
builder.Services.AddScoped<HeaderDetectionService>();
builder.Services.AddScoped<ColumnMappingService>();
builder.Services.AddScoped<ValueParsingService>();
builder.Services.AddScoped<RowMaterializationService>();
builder.Services.AddScoped<ITransactionFileParser, CsvFileParserAdapter>();
builder.Services.AddScoped<ITransactionFileParser, ExcelParsingService>();
builder.Services.AddScoped<ParserResolutionService>();
// Swagger (for testing your API easily)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
 builder.Services.AddCors(options =>
      options.AddDefaultPolicy(policy =>
          policy.WithOrigins("http://localhost:5173")
                .AllowAnyHeader()
                .AllowAnyMethod()));
var app = builder.Build();
app.UseCors();

using (var scope = app.Services.CreateScope())
{
    var parserResolutionService = scope.ServiceProvider.GetRequiredService<ParserResolutionService>();
    parserResolutionService.ValidateConfiguration([".csv", ".xlsx"]);
}
// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

 //app.UseHttpsRedirection();

// Map controllers (this enables your endpoints)
app.MapControllers();

app.Run();
