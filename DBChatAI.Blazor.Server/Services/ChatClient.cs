using DBChatAI.Blazor.Server.Services.Interface;
using DBChatAI.Module.BusinessObjects.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace DBChatAI.Blazor.Server.Services
{
    public sealed class AzureAiChatOptions
    {
        public int MaxHistoryMessages { get; set; } = 3;

        public int MaxMessageLength { get; set; } = 800;
    }

    public class AzureAiChatService : IAiChatService
    {
        private readonly IChatClient _chatClient;
        private readonly AzureAiChatOptions _options;

        public AzureAiChatService(IChatClient chatClient, IConfiguration configuration)
        {
            _chatClient = chatClient;

            var options = new AzureAiChatOptions();
            configuration.GetSection("AIChat").Bind(options);
            _options = options;
        }

        public async Task<AiChatResponse> AskAsync(
            string question,
            IEnumerable<AiMessage> history,
            string dbSchemaSummary)
        {
            // 1) System prompt: rules + DB schema
            var systemPrompt =
                "You are an assistant for a SQL Server database.\n\n" +
                "Database schema:\n" +
                dbSchemaSummary + "\n\n" +
                "IMPORTANT rules (FOLLOW THEM TO THE LETTER):\n" +
                "- ALWAYS answer in English.\n" +
                "- When appropriate, propose a read-only SQL query (only SELECT) that retrieves the requested data.\n" +
                "- NEVER use INSERT, UPDATE, DELETE, DROP, ALTER, TRUNCATE, CREATE.\n" +
                "- NEVER use tables that are not present in the schema.\n" +
                "- DO NOT include the actual results of the SQL queries, only the query.\n" +
                "- DO NOT use markdown code blocks (no ```).\n" +
                "- DO NOT add any text outside of the JSON object.\n" +
                "- You MUST ALWAYS return ONLY one valid JSON object, in the following format:\n" +
                "{\n" +
                "  \"answer\": \"natural language text, in ONE single line, without line breaks (use periods to separate sentences)\",\n" +
                "  \"sql\": \"SELECT query, or empty string if not needed\"\n" +
                "}\n" +
                "- If a column is of type geography or geometry, avoid selecting it directly.\n" +
                "- If the user asks for spatial information, use a textual representation, for example:\n" +
                "      CONVERT(nvarchar(4000), GeographyColumnName)\n" +
                "  or:\n" +
                "      GeographyColumnName.STAsText()\n" +
                "IMPORTANT (RULE ABOUT NAMES AND STRINGS):\r\n" +
                "- When the user provides a name (company, customer, person, place, partial text),\r\n" +
                "  you MUST ALWAYS use a LIKE condition with wildcards:\r\n" +
                "      LOWER(Field) LIKE LOWER('%value%')\r\n" +
                "- NEVER use '=' to compare text strings.\r\n" +
                "- Example: if the user says \"acme\", generate:\r\n" +
                "      WHERE LOWER(CompanyName) LIKE LOWER('%acme%')\r\n";

            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, systemPrompt)
            };

            var cleanedHistory = history
                .Where(m => m.Role == AiMessageRole.User)
                .Where(m => !string.IsNullOrWhiteSpace(m.Content) && m.Content.Length < _options.MaxMessageLength)
                .OrderBy(m => m.CreatedOn)
                .TakeLast(_options.MaxHistoryMessages)
                .ToList();

            foreach (var m in cleanedHistory)
            {
                messages.Add(new ChatMessage(ChatRole.User, m.Content));
            }

            var lastSql = history
                .Where(m => !string.IsNullOrWhiteSpace(m.SqlQuery))
                .OrderByDescending(m => m.CreatedOn)
                .Select(m => m.SqlQuery)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(lastSql))
            {
                messages.Add(new ChatMessage(
                    ChatRole.User,
                    "This is the previous query you generated:\n" + lastSql));
            }

            messages.Add(new ChatMessage(ChatRole.User, question));

            ChatResponse result;
            try
            {
                result = await _chatClient.GetResponseAsync(messages);
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Network error while calling the AI service: {ex.Message}", ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException(
                    "Timeout while calling the AI service. The model might be overloaded.", ex);
            }
            catch (Exception ex) when (ex.Message.Contains("content", StringComparison.OrdinalIgnoreCase) &&
                                       ex.Message.Contains("filter", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The request was blocked by the Azure OpenAI content filter. " +
                    "Change the question (avoid overly detailed or sensitive data) and try again.", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Error while calling the AI service: {ex.Message}", ex);
            }

            var raw = result.Messages != null && result.Messages.Count > 0
                ? (result.Messages[0].Text ?? string.Empty).Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(raw))
            {
                throw new InvalidOperationException(
                    "The AI service returned an empty or invalid response.");
            }

            raw = StripCodeFences(raw);

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(raw);
            }
            catch (JsonException ex)
            {
                throw new InvalidOperationException(
                    $"AI response is not valid JSON. Content:\n{raw}", ex);
            }

            var root = doc.RootElement;

            string answer = root.TryGetProperty("answer", out var answerProp)
                ? answerProp.GetString() ?? string.Empty
                : string.Empty;

            string sql = root.TryGetProperty("sql", out var sqlProp)
                ? sqlProp.GetString() ?? string.Empty
                : string.Empty;

            return new AiChatResponse
            {
                AssistantMessage = answer,
                ProposedSql = sql
            };
        }

        private static string StripCodeFences(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return raw;

            if (!raw.Contains("```"))
                return raw;

            int firstBrace = raw.IndexOf('{');
            int lastBrace = raw.LastIndexOf('}');

            if (firstBrace >= 0 && lastBrace > firstBrace)
            {
                return raw.Substring(firstBrace, lastBrace - firstBrace + 1).Trim();
            }

            return raw.Replace("```json", string.Empty)
                      .Replace("```", string.Empty)
                      .Trim();
        }
    }
}
