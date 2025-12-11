using DBChatAI.Blazor.Server.Services.Interface;
using Microsoft.Data.SqlClient;
using System.Data;

namespace DBChatAI.Blazor.Server.Services
{
   
    public class SafeSqlExecutor : ISafeSqlExecutor
    {
        private readonly string _connectionString;

        public SafeSqlExecutor(string connectionString)
        {
            _connectionString = NormalizeConnectionString(connectionString);
        }

        // Remove XPO-specific keywords (like XpoProvider) so SqlConnection can accept the string.
        private static string NormalizeConnectionString(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString)) return connectionString;

            var parts = connectionString
                .Split(';')
                .Select(p => p?.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .Where(p => !p.StartsWith("XpoProvider=", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return string.Join(";", parts);
        }

        public async Task<DataTable> ExecuteSafeSelectAsync(string sql)
        {
            var table = new DataTable();

            using var conn = new SqlConnection(_connectionString);
            using var cmd = new SqlCommand(sql, conn);

            await conn.OpenAsync();
            using var reader = await cmd.ExecuteReaderAsync();

            
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                Type fieldType = null;
                string dataTypeName = null;

                try
                {
                    fieldType = reader.GetFieldType(i);          
                    dataTypeName = reader.GetDataTypeName(i);    
                }
                catch
                {
                    
                }

                
                Type columnType = fieldType ?? typeof(string);

                
                if (!string.IsNullOrEmpty(dataTypeName) &&
                    (dataTypeName.Equals("geography", StringComparison.OrdinalIgnoreCase) ||
                     dataTypeName.Equals("geometry", StringComparison.OrdinalIgnoreCase)))
                {
                    columnType = typeof(string);
                }

                table.Columns.Add(name, columnType);
            }

            
            while (await reader.ReadAsync())
            {
                var values = new object[reader.FieldCount];

                for (int i = 0; i < reader.FieldCount; i++)
                {
                    if (reader.IsDBNull(i))
                    {
                        values[i] = null;
                        continue;
                    }

                    var dataTypeName = reader.GetDataTypeName(i);
                    object value = reader.GetValue(i);

                    
                    if (!string.IsNullOrEmpty(dataTypeName) &&
                        (dataTypeName.Equals("geography", StringComparison.OrdinalIgnoreCase) ||
                         dataTypeName.Equals("geometry", StringComparison.OrdinalIgnoreCase)))
                    {

                        values[i] = value?.ToString();
                    }
                    else
                    {
                        values[i] = value;
                    }
                }

                table.Rows.Add(values);
            }

            return table;
        }



        //public async Task<DataTable> ExecuteSafeSelectAsync(string sql)
        //{
        //    ValidateSql(sql);

        //    using var conn = new SqlConnection(_connectionString);
        //    await conn.OpenAsync();

        //    using var cmd = new SqlCommand(sql, conn);
        //    var dt = new DataTable();
        //    using var reader = await cmd.ExecuteReaderAsync();
        //    dt.Load(reader);
        //    return dt;
        //}

        private void ValidateSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new InvalidOperationException("Empty query.");

            var normalized = sql.Trim().ToUpperInvariant();

            if (!normalized.StartsWith("SELECT"))
                throw new InvalidOperationException("Only SELECT queries are allowed.");

            string[] forbidden = { "INSERT ", "UPDATE ", "DELETE ",
                               "DROP ", "ALTER ", "TRUNCATE ", "CREATE " };

            foreach (var f in forbidden)
            {
                if (normalized.Contains(f))
                    throw new InvalidOperationException($"Query not allowed (contains {f.Trim()}).");
            }

            // Optional: check that it does not use disallowed tables
        }
    }

}
