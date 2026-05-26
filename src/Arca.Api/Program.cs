using Arca.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure();

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
