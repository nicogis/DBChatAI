namespace DBChatAI.Blazor.Server.Helpers
{
    using System.Text.RegularExpressions;
    using System;
    public static class SqlPatchHelper
    {

        // ============================================================
        // Paging helpers
        // ============================================================

        /// <summary>
        /// Extract OFFSET / FETCH values from a SQL statement.
        /// If not present, returns Offset = 0, Fetch = defaultFetch, HasPaging = false.
        /// </summary>
        public static (int Offset, int Fetch, bool HasPaging) ExtractOffsetFetch(
            string sql,
            int defaultFetch = 100)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return (0, defaultFetch, false);

            var regex = new Regex(
                @"OFFSET\s+(\d+)\s+ROWS\s+FETCH\s+NEXT\s+(\d+)\s+ROWS\s+ONLY",
                RegexOptions.IgnoreCase);

            var m = regex.Match(sql);
            if (m.Success &&
                int.TryParse(m.Groups[1].Value, out int off) &&
                int.TryParse(m.Groups[2].Value, out int fetch))
            {
                return (off, fetch, true);
            }

            return (0, defaultFetch, false);
        }

        /// <summary>
        /// Remove only the OFFSET/FETCH clause from the SQL.
        /// </summary>
        public static string RemoveOffsetFetch(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            var regex = new Regex(
                @"OFFSET\s+\d+\s+ROWS\s+FETCH\s+NEXT\s+\d+\s+ROWS\s+ONLY",
                RegexOptions.IgnoreCase);

            return regex.Replace(sql, "").Trim().TrimEnd(';');
        }

        /// <summary>
        /// Remove ORDER BY and any paging (OFFSET/FETCH) from the SQL.
        /// </summary>
        public static string RemoveOrderByAndPaging(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            var noPaging = RemoveOffsetFetch(sql);
            var lower = noPaging.ToLowerInvariant();
            var idxOrder = lower.LastIndexOf(" order by ");

            if (idxOrder >= 0)
                return noPaging.Substring(0, idxOrder).Trim().TrimEnd(';');

            return noPaging.Trim().TrimEnd(';');
        }

        /// <summary>
        /// Remove WHERE clause but keep ORDER BY and paging (if present).
        /// </summary>
        public static string RemoveWhereKeepOrderAndPaging(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            var lower = sql.ToLowerInvariant();
            int idxWhere = lower.IndexOf(" where ");
            if (idxWhere < 0)
                return sql; // nothing to remove

            int idxOrder = lower.LastIndexOf(" order by ");
            int idxOffset = lower.LastIndexOf(" offset ");

            string orderAndPaging = "";
            int tailIndex = sql.Length;

            if (idxOrder >= 0)
                tailIndex = Math.Min(tailIndex, idxOrder);
            if (idxOffset >= 0)
                tailIndex = Math.Min(tailIndex, idxOffset);

            var baseSql = sql.Substring(0, idxWhere).Trim();

            if (tailIndex < sql.Length)
            {
                orderAndPaging = sql.Substring(tailIndex).Trim();
            }

            return $"{baseSql} {orderAndPaging}".Trim().TrimEnd(';');
        }

        /// <summary>
        /// Remove WHERE, ORDER BY and paging from the SQL.
        /// </summary>
        public static string RemoveAllFiltersAndSorting(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            var withoutWhere = RemoveWhereKeepOrderAndPaging(sql);
            return RemoveOrderByAndPaging(withoutWhere);
        }

        /// <summary>
        /// Ensure there is an ORDER BY clause before applying OFFSET/FETCH.
        /// If none is present, appends 'ORDER BY (SELECT NULL)'.
        /// </summary>
        private static string EnsureOrderBy(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            var lower = sql.ToLowerInvariant();
            if (lower.Contains(" order by "))
                return sql;

            // Fallback: you can replace (SELECT NULL) with a concrete column if you prefer
            return $"{sql} ORDER BY (SELECT NULL)";
        }

        /// <summary>
        /// Apply OFFSET/FETCH paging to the SQL. Ensures there is an ORDER BY.
        /// </summary>
        public static string ApplyPaging(string sql, int offset, int fetch)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            sql = RemoveOffsetFetch(sql);
            sql = EnsureOrderBy(sql);

            return $"{sql} OFFSET {offset} ROWS FETCH NEXT {fetch} ROWS ONLY";
        }

        /// <summary>
        /// Apply paging for the first page (offset 0).
        /// </summary>
        public static string ApplyFirstPage(string sql, int fetch)
        {
            return ApplyPaging(sql, 0, fetch);
        }

        /// <summary>
        /// Change the page size, trying to keep the same page index.
        /// If no paging is found, start with page 0 and the new page size.
        /// </summary>
        public static string ApplyPageSize(string sql, int newFetch)
        {
            if (string.IsNullOrWhiteSpace(sql))
                return sql;

            var (oldOffset, oldFetch, hasPaging) =
                ExtractOffsetFetch(sql, defaultFetch: newFetch);

            if (!hasPaging)
            {
                return ApplyPaging(sql, 0, newFetch);
            }

            int currentPageIndex = oldFetch > 0 ? oldOffset / oldFetch : 0;
            int newOffset = currentPageIndex * newFetch;

            return ApplyPaging(sql, newOffset, newFetch);
        }

        // ============================================================
        // Sorting
        // ============================================================

        /// <summary>
        /// Apply ORDER BY. If keepOffset/keepFetch are provided, re-apply paging.
        /// If they are null, no paging is added.
        /// </summary>
        public static string ApplySort(string sql, string column, string direction, int? keepOffset, int? keepFetch)
        {
            if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(column))
                return sql;

            // Remove existing ORDER BY + paging
            string baseSql = RemoveOrderByAndPaging(sql);

            // New ORDER BY
            var sorted = $"{baseSql} ORDER BY {column} {direction}";

            // ONLY add paging if both offset and fetch are provided (hasPaging == true)
            if (keepOffset.HasValue && keepFetch.HasValue)
            {
                sorted += $" OFFSET {keepOffset.Value} ROWS FETCH NEXT {keepFetch.Value} ROWS ONLY";
            }

            return sorted;
        }


        // ============================================================
        // Filtering
        // ============================================================

        /// <summary>
        /// Apply a simple filter of the form "column op value"
        /// (e.g. column > 10, Name = 'ACME').
        /// Keeps ORDER BY if present, but does not touch paging.
        /// </summary>
        public static string ApplyFilter(string sql, string column, string op, string value)
        {
            if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(column))
                return sql;

            // Normalize string values - if not numeric and not already quoted, quote them
            if (!int.TryParse(value, out _) && !value.StartsWith("'"))
            {
                value = $"'{value.Trim('\'')}'";
            }

            string lower = sql.ToLowerInvariant();
            int idxOrder = lower.LastIndexOf(" order by ");

            string baseSql = sql;
            string orderPart = "";

            if (idxOrder >= 0)
            {
                baseSql = sql.Substring(0, idxOrder).Trim();
                orderPart = sql.Substring(idxOrder).Trim();
            }

            string condition = $"{column} {op} {value}";

            if (baseSql.ToLowerInvariant().Contains(" where "))
                baseSql += $" AND {condition}";
            else
                baseSql += $" WHERE {condition}";

            return $"{baseSql} {orderPart}".Trim();
        }
    }
}
