namespace DBChatAI.Blazor.Server.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text.RegularExpressions;

   

        public static class SqlColumnResolver
        {

            private class ColumnInfo
            {
                public string Expression { get; set; }   // e.g. "e.BirthDate" or "BirthDate AS Birth"
                public string Name { get; set; }         // e.g. "BirthDate" or alias
            }

            public static string ResolveColumnFromSelect(string sql, string userColumnName)
            {
                if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(userColumnName))
                    return userColumnName;

                var selectPart = ExtractSelectList(sql);
                if (string.IsNullOrWhiteSpace(selectPart))
                    return userColumnName;

                var columns = ParseColumns(selectPart);
                if (!columns.Any())
                    return userColumnName;

                string normUser = Normalize(userColumnName);

                // 1) exact match on normalized name
                var exact = columns.FirstOrDefault(c => Normalize(c.Name) == normUser);
                if (exact != null)
                    return exact.Name;

                // 2) contains / partial match
                var partial = columns.FirstOrDefault(c =>
                    Normalize(c.Name).Contains(normUser) ||
                    normUser.Contains(Normalize(c.Name)));

                if (partial != null)
                    return partial.Name;

                // 3) fallback: return original user text (will probably error, but at least consistent)
                return userColumnName;
            }

            private static string ExtractSelectList(string sql)
            {
                // Very simple parser: take text between SELECT and FROM
                var lower = sql.ToLowerInvariant();
                int idxSelect = lower.IndexOf("select ");
                int idxFrom = lower.IndexOf(" from ");

                if (idxSelect < 0 || idxFrom < 0 || idxFrom <= idxSelect)
                    return null;

                var list = sql.Substring(idxSelect + "select".Length, idxFrom - (idxSelect + "select".Length));
                return list;
            }

            private static List<ColumnInfo> ParseColumns(string selectList)
            {
                var result = new List<ColumnInfo>();
                if (string.IsNullOrWhiteSpace(selectList))
                    return result;

                // naïve split on commas (good enough for simple SELECTs)
                var parts = selectList.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();

                foreach (var part in parts)
                {
                    // Check for "AS Alias"
                    var asMatch = Regex.Match(part, @"(.+?)\s+as\s+([A-Za-z0-9_\[\]]+)", RegexOptions.IgnoreCase);
                    if (asMatch.Success)
                    {
                        var expr = asMatch.Groups[1].Value.Trim();
                        var alias = asMatch.Groups[2].Value.Trim();
                        result.Add(new ColumnInfo { Expression = expr, Name = alias });
                        continue;
                    }

                    // If no AS, take last token after dot as column name
                    var noAs = part;
                    // Remove possible "DISTINCT"
                    noAs = Regex.Replace(noAs, @"^\s*distinct\s+", "", RegexOptions.IgnoreCase).Trim();

                    var tokens = noAs.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    var lastToken = tokens.LastOrDefault();
                    if (lastToken != null)
                    {
                        // Remove table alias prefix: e.BirthDate -> BirthDate
                        if (lastToken.Contains("."))
                        {
                            lastToken = lastToken.Split('.').Last();
                        }
                        // Remove brackets [BirthDate]
                        lastToken = lastToken.Trim('[', ']');

                        result.Add(new ColumnInfo { Expression = part, Name = lastToken });
                    }
                }

                return result;
            }

            private static string Normalize(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                    return string.Empty;

                name = name.ToLowerInvariant();
                // remove spaces, underscores and brackets
                name = name.Replace(" ", "")
                           .Replace("_", "")
                           .Replace("[", "")
                           .Replace("]", "");
                return name;
            }
        }
    

}
