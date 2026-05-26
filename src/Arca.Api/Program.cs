using Arca.Infrastructure.Database;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddHealthChecks().AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!);
builder.Services.AddScoped<IDbConnectionFactory, NpgsqlConnectionFactory>();

var app = builder.Build();
app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
