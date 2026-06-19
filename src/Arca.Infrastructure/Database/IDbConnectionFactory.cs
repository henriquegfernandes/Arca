using System.Data;

namespace Arca.Infrastructure.Database;

public interface IDbConnectionFactory
{
    IDbConnection CreateConnection();
}
