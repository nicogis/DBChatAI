using DBChatAI.Module.BusinessObjects.AI;

namespace DBChatAI.Blazor.Server.Services.Interface
{
    public interface IAiChatService
    {
        Task<AiChatResponse> AskAsync(string question,
                                      IEnumerable<AiMessage> history,
                                      string dbSchemaSummary);
    }
}
