namespace DBChatAI.Blazor.Server.Helpers
{
    using System.Text.RegularExpressions;



    public enum UserCommandType
    {
        None,
        NextPage,
        PreviousPage,
        GoToPage,
        FirstPage,
        ChangePageSize,
        Sort,
        Filter,
        ClearSorting,
        ClearFilters,
        ClearAll
    }

    public class UserCommand
    {
        public UserCommandType Type { get; set; } = UserCommandType.None;

        // Paging
        public int? PageNumber { get; set; }      // e.g. page 3
        public int? PageSize { get; set; }        // e.g. next 20, page size 50

        // Sorting
        public string SortColumn { get; set; }
        public string SortDirection { get; set; } = "ASC";

        // Filtering
        public string FilterColumn { get; set; }
        public string FilterOperator { get; set; } = "=";
        public string FilterValue { get; set; }
    }

    public static class SqlUserCommandHelper
    {

        public static UserCommand DetectUserCommand(string text)
        {
            var cmd = new UserCommand();

            if (string.IsNullOrWhiteSpace(text))
                return cmd;

            var lower = text.ToLowerInvariant();

            // ============================================================
            // RESET / CLEAR ALL
            // ============================================================
            if (lower.Contains("reset all") ||
                lower.Contains("reset query") ||
                lower.Contains("start over") ||
                lower.Contains("clear all"))
            {
                cmd.Type = UserCommandType.ClearAll;
                return cmd;
            }

            // ============================================================
            // CLEAR SORTING
            // ============================================================
            if (lower.Contains("clear sorting") ||
                lower.Contains("reset sorting") ||
                lower.Contains("clear sort") ||
                lower.Contains("reset sort"))
            {
                cmd.Type = UserCommandType.ClearSorting;
                return cmd;
            }

            // ============================================================
            // CLEAR FILTERS
            // ============================================================
            if (lower.Contains("clear filters") ||
                lower.Contains("reset filters") ||
                lower.Contains("remove filters"))
            {
                cmd.Type = UserCommandType.ClearFilters;
                return cmd;
            }

            // ============================================================
            // FIRST PAGE
            // ============================================================
            if (lower.Contains("first page") ||
                lower.Contains("go to first page") ||
                lower.Contains("start page") ||
                lower.Contains("back to start"))
            {
                cmd.Type = UserCommandType.FirstPage;
                return cmd;
            }

            // ============================================================
            // NEXT PAGE WITH EXPLICIT NUMBER: "next 20"
            // ============================================================
            var nextWithNumberRegex = new Regex(@"\bnext\s+(\d+)", RegexOptions.IgnoreCase);
            var nextMatch = nextWithNumberRegex.Match(text);
            if (nextMatch.Success && int.TryParse(nextMatch.Groups[1].Value, out int nextCount))
            {
                cmd.Type = UserCommandType.NextPage;
                cmd.PageSize = nextCount;
                return cmd;
            }

            // GENERIC NEXT PAGE
            if (lower.Contains("next page") ||
                lower.Contains("next") ||
                lower.Contains("show more") ||
                lower.Contains("more"))
            {
                cmd.Type = UserCommandType.NextPage;
                return cmd;
            }

            // ============================================================
            // PREVIOUS PAGE WITH EXPLICIT NUMBER: "previous 10"
            // ============================================================
            var prevWithNumberRegex = new Regex(@"\b(previous|prev)\s+(\d+)", RegexOptions.IgnoreCase);
            var prevMatch = prevWithNumberRegex.Match(text);
            if (prevMatch.Success && int.TryParse(prevMatch.Groups[2].Value, out int prevCount))
            {
                cmd.Type = UserCommandType.PreviousPage;
                cmd.PageSize = prevCount;
                return cmd;
            }

            // GENERIC PREVIOUS PAGE
            if (lower.Contains("previous") ||
                lower.Contains("prev") ||
                lower.Contains("back one page") ||
                lower.Contains("previous page"))
            {
                cmd.Type = UserCommandType.PreviousPage;
                return cmd;
            }

            // ============================================================
            // GO TO PAGE N  → "page 3"
            // ============================================================
            var pageRegex = new Regex(@"\bpage\s+(\d+)", RegexOptions.IgnoreCase);
            var mPage = pageRegex.Match(text);
            if (mPage.Success && int.TryParse(mPage.Groups[1].Value, out int pageNo))
            {
                cmd.Type = UserCommandType.GoToPage;
                cmd.PageNumber = pageNo;
                return cmd;
            }

            // ============================================================
            // CHANGE PAGE SIZE → "page size 50", "per page 20"
            // ============================================================
            var sizeRegex = new Regex(@"(page size|per page|rows per page|show)\s+(\d+)",
                RegexOptions.IgnoreCase);

            var mSize = sizeRegex.Match(text);
            if (mSize.Success && int.TryParse(mSize.Groups[2].Value, out int pageSize))
            {
                cmd.Type = UserCommandType.ChangePageSize;
                cmd.PageSize = pageSize;
                return cmd;
            }

            // ============================================================
            // SORTING (Advanced natural-language parser)
            // ============================================================
            if (lower.Contains("order by") ||
                lower.Contains("sort by") ||
                lower.StartsWith("sort "))
            {
                cmd.Type = UserCommandType.Sort;
                cmd.SortDirection = lower.Contains(" desc") ? "DESC" : "ASC";

                // Words to ignore between "sort" and the real column
                string[] noiseWords = { "them", "it", "results", "records", "rows", "list", "the" };

                string cleaned = text;
                foreach (var w in noiseWords)
                    cleaned = Regex.Replace(cleaned, @"\b" + w + @"\b", "", RegexOptions.IgnoreCase).Trim();

                // 1) ORDER BY Case
                var orderByMatch = Regex.Match(cleaned, @"order by\s+([A-Za-z0-9_\[\]\s]+)",
                    RegexOptions.IgnoreCase);
                if (orderByMatch.Success)
                {
                    cmd.SortColumn = NormalizeColumnName(orderByMatch.Groups[1].Value);
                    return cmd;
                }

                // 2) SORT BY Case
                var sortByMatch = Regex.Match(cleaned, @"sort by\s+([A-Za-z0-9_\[\]\s]+)",
                    RegexOptions.IgnoreCase);
                if (sortByMatch.Success)
                {
                    cmd.SortColumn = NormalizeColumnName(sortByMatch.Groups[1].Value);
                    return cmd;
                }

                // 3) SORT <Column> Case
                var sortMatch = Regex.Match(cleaned, @"sort\s+([A-Za-z0-9_\[\]\s]+)",
                    RegexOptions.IgnoreCase);
                if (sortMatch.Success)
                {
                    cmd.SortColumn = NormalizeColumnName(sortMatch.Groups[1].Value);
                    return cmd;
                }
            }

            // ============================================================
            // FILTER
            // ============================================================
            var filterRegex = new Regex(
                @"(filter by|filter)\s+([A-Za-z0-9_]+)\s*(=|>|<|>=|<=)\s*('?[^']+'?|\d+)",
                RegexOptions.IgnoreCase);

            var fmatch = filterRegex.Match(text);
            if (fmatch.Success)
            {
                cmd.Type = UserCommandType.Filter;
                cmd.FilterColumn = fmatch.Groups[2].Value;
                cmd.FilterOperator = fmatch.Groups[3].Value;
                cmd.FilterValue = fmatch.Groups[4].Value.Trim();
                return cmd;
            }

            return cmd; // no command detected
        }

        // ============================================================
        // Column Name Normalizer
        // - Removes double spaces
        // - Optional: remove spaces to make "Last Name" => "LastName"
        // ============================================================
        private static string NormalizeColumnName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            name = name.Trim();

            // Remove leading "by " if present (e.g. "by Birthdate" -> "Birthdate")
            if (name.StartsWith("by ", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(3).Trim();
            }

            // Collapse multiple spaces to single
            name = Regex.Replace(name, @"\s+", " ");

            // OPTIONAL:
            // Uncomment to transform "Last Name" -> "LastName"
            // name = name.Replace(" ", "");

            return name;
        }
    }


}





