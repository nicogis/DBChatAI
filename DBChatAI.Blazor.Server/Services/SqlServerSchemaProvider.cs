using DBChatAI.Blazor.Server.Services.Interface;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace DBChatAI.Blazor.Server.Services
{
    public class SqlServerSchemaProvider : IDbSchemaProvider
    {
        private readonly string _connectionString;
        private readonly string[] _excludeExactTableNames;
        private readonly string[] _excludeLikeTableNames;
        private string _cachedSummary;

        public SqlServerSchemaProvider(IConfiguration configuration)
        {
            _connectionString = NormalizeConnectionString(configuration.GetConnectionString("ConnectionString")
                                  ?? throw new InvalidOperationException("Missing ConnectionString."));

            _excludeExactTableNames = configuration
                .GetSection("DBAIChat:Tables:SchemaFilter:ExcludeExact")
                .Get<string[]>() ?? [];

            _excludeLikeTableNames = configuration
                .GetSection("DBAIChat:Tables:SchemaFilter:ExcludeLike")
                .Get<string[]>() ?? [];
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

        public string GetSchemaSummary()
        {
            if (!string.IsNullOrEmpty(_cachedSummary))
                return _cachedSummary;

            using var conn = new SqlConnection(_connectionString);
            conn.Open();

            var tables = LoadTables(conn);
            var columns = LoadColumns(conn);
            var fks = LoadForeignKeys(conn);

            var sb = new StringBuilder();

            foreach (var tbl in tables.OrderBy(t => t.Schema).ThenBy(t => t.Name))
            {
                sb.AppendLine($"TABLE: {tbl.Schema}.{tbl.Name}");

                if (!string.IsNullOrWhiteSpace(tbl.Description))
                {
                    sb.AppendLine($"  Description: {tbl.Description}");
                }

                // columns
                var tableColumns = columns
                    .Where(c => c.TableName == tbl.Name && c.SchemaName == tbl.Schema)
                    .OrderBy(c => c.OrdinalPosition)
                    .ToList();

                foreach (var col in tableColumns)
                {
                    var flags = new List<string>();
                    if (col.IsPrimaryKey) flags.Add("PK");
                    if (col.IsIdentity) flags.Add("IDENTITY");
                    if (!col.IsNullable) flags.Add("NOT NULL");
                    else flags.Add("NULL");

                    var flagStr = flags.Count > 0 ? $" [{string.Join(", ", flags)}]" : "";

                    sb.Append($"  - {col.ColumnName} ({col.DataType}");

                    if (col.MaxLength.HasValue && col.MaxLength.Value > 0 && col.MaxLength.Value < 8000)
                    {
                        sb.Append($"({col.MaxLength})");
                    }

                    sb.Append($"){flagStr}");

                    if (!string.IsNullOrWhiteSpace(col.Description))
                    {
                        sb.Append($" – {col.Description}");
                    }

                    sb.AppendLine();
                }

                // foreign keys starting from this table
                var tableFks = fks
                    .Where(f => f.FromSchema == tbl.Schema && f.FromTable == tbl.Name)
                    .ToList();

                if (tableFks.Any())
                {
                    sb.AppendLine("  Relationships:");
                    foreach (var fk in tableFks)
                    {
                        sb.AppendLine($"    - {fk.FromColumn} → {fk.ToSchema}.{fk.ToTable}.{fk.ToColumn}");
                    }
                }

                sb.AppendLine();
            }

            _cachedSummary = sb.ToString();
            return _cachedSummary;
        }

        #region Internal DTOs

        private class TableInfo
        {
            public string Schema { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
        }

        private class ColumnInfo
        {
            public string SchemaName { get; set; }
            public string TableName { get; set; }
            public string ColumnName { get; set; }
            public string DataType { get; set; }
            public int? MaxLength { get; set; }
            public bool IsNullable { get; set; }
            public bool IsIdentity { get; set; }
            public bool IsPrimaryKey { get; set; }
            public int OrdinalPosition { get; set; }
            public string Description { get; set; }
        }

        private class ForeignKeyInfo
        {
            public string FromSchema { get; set; }
            public string FromTable { get; set; }
            public string FromColumn { get; set; }
            public string ToSchema { get; set; }
            public string ToTable { get; set; }
            public string ToColumn { get; set; }
        }

        #endregion

        #region Read schema from SQL Server

        private List<TableInfo> LoadTables(SqlConnection conn)
        {
            var sb = new StringBuilder(@"
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    CAST(ep.value AS nvarchar(4000)) AS TableDescription
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
LEFT JOIN sys.extended_properties ep
    ON ep.major_id = t.object_id
    AND ep.minor_id = 0
    AND ep.name = 'MS_Description'
WHERE t.is_ms_shipped = 0
");

            // Esclusioni con nome esatto
            for (var i = 0; i < _excludeExactTableNames.Length; i++)
            {
                sb.AppendLine($"AND t.name <> @ExExact{i}");
            }

            // Esclusioni con LIKE
            for (var i = 0; i < _excludeLikeTableNames.Length; i++)
            {
                sb.AppendLine($"AND t.name NOT LIKE @ExLike{i}");
            }

            sb.AppendLine("ORDER BY s.name, t.name;");

            var list = new List<TableInfo>();

            using var cmd = new SqlCommand(sb.ToString(), conn);

            for (var i = 0; i < _excludeExactTableNames.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@ExExact{i}", _excludeExactTableNames[i]);
            }

            for (var i = 0; i < _excludeLikeTableNames.Length; i++)
            {
                cmd.Parameters.AddWithValue($"@ExLike{i}", _excludeLikeTableNames[i]);
            }

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TableInfo
                {
                    Schema = reader.GetString(0),
                    Name = reader.GetString(1),
                    Description = reader.IsDBNull(2) ? null : reader.GetString(2)
                });
            }

            return list;
        }

        private List<ColumnInfo> LoadColumns(SqlConnection conn)
        {
            const string sql = @"
SELECT
    s.name AS SchemaName,
    t.name AS TableName,
    c.name AS ColumnName,
    ty.name AS DataType,
    c.max_length,
    c.is_nullable,
    c.is_identity,
    ISNULL(pk.is_primary_key, 0) AS IsPrimaryKey,
    c.column_id AS OrdinalPosition,
    CAST(ep.value AS nvarchar(4000)) AS ColumnDescription
FROM sys.tables t
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.columns c ON c.object_id = t.object_id
JOIN sys.types ty ON ty.user_type_id = c.user_type_id
LEFT JOIN sys.extended_properties ep
    ON ep.major_id = t.object_id
    AND ep.minor_id = c.column_id
    AND ep.name = 'MS_Description'
LEFT JOIN (
    SELECT i.object_id, ic.column_id, i.is_primary_key
    FROM sys.indexes i
    JOIN sys.index_columns ic
        ON ic.object_id = i.object_id AND ic.index_id = i.index_id
    WHERE i.is_primary_key = 1
) pk
    ON pk.object_id = t.object_id AND pk.column_id = c.column_id
WHERE t.is_ms_shipped = 0
ORDER BY s.name, t.name, c.column_id;
";

            var list = new List<ColumnInfo>();

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ColumnInfo
                {
                    SchemaName = reader.GetString(0),
                    TableName = reader.GetString(1),
                    ColumnName = reader.GetString(2),
                    DataType = reader.GetString(3),
                    MaxLength = reader.IsDBNull(4) ? (int?)null : reader.GetInt16(4),
                    IsNullable = reader.GetBoolean(5),
                    IsIdentity = reader.GetBoolean(6),
                    IsPrimaryKey = reader.GetBoolean(7),
                    OrdinalPosition = reader.GetInt32(8),
                    Description = reader.IsDBNull(9) ? null : reader.GetString(9)
                });
            }

            return list;
        }

        private List<ForeignKeyInfo> LoadForeignKeys(SqlConnection conn)
        {
            const string sql = @"
SELECT
    schP.name AS FromSchema,
    OBJECT_NAME(fk.parent_object_id) AS FromTable,
    colP.name AS FromColumn,
    schR.name AS ToSchema,
    OBJECT_NAME(fk.referenced_object_id) AS ToTable,
    colR.name AS ToColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc
    ON fkc.constraint_object_id = fk.object_id
JOIN sys.tables tP
    ON tP.object_id = fk.parent_object_id
JOIN sys.schemas schP
    ON schP.schema_id = tP.schema_id
JOIN sys.columns colP
    ON colP.object_id = fk.parent_object_id AND colP.column_id = fkc.parent_column_id
JOIN sys.tables tR
    ON tR.object_id = fk.referenced_object_id
JOIN sys.schemas schR
    ON schR.schema_id = tR.schema_id
JOIN sys.columns colR
    ON colR.object_id = fk.referenced_object_id AND colR.column_id = fkc.referenced_column_id
WHERE fk.is_ms_shipped = 0
ORDER BY FromSchema, FromTable, FromColumn;
";

            var list = new List<ForeignKeyInfo>();

            using var cmd = new SqlCommand(sql, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ForeignKeyInfo
                {
                    FromSchema = reader.GetString(0),
                    FromTable = reader.GetString(1),
                    FromColumn = reader.GetString(2),
                    ToSchema = reader.GetString(3),
                    ToTable = reader.GetString(4),
                    ToColumn = reader.GetString(5)
                });
            }

            return list;
        }

        #endregion
    }
}
