using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using ContactRelay.Options;

namespace ContactRelay.Data;

public sealed class SqlConnectionFactory(IOptions<SqlOptions> options) : ISqlConnectionFactory
{
    private readonly SqlOptions _options = options.Value;

    public SqlConnection CreateConnection()
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            throw new InvalidOperationException("Sql:ConnectionString is required.");
        }

        return new SqlConnection(_options.ConnectionString);
    }
}
