using Microsoft.Data.SqlClient;

namespace ContactRelay.Data;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}
