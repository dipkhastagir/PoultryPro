using Microsoft.Data.SqlClient;
using System.Data;

public class SqlConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public IDbConnection Create() => new SqlConnection(connectionString);
}