using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

// Add services to the container
builder.Services.AddControllers().AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters
            .Add(new JsonStringEnumConverter()));
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