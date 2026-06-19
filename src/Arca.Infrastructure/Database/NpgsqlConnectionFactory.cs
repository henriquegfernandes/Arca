using System.Data;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Arca.Infrastructure.Database;

public sealed class NpgsqlConnectionFactory(IConfiguration configuration) : IDbConnectionFactory
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");

    public IDbConnection CreateConnection() => new NpgsqlConnection(_connectionString);
}
