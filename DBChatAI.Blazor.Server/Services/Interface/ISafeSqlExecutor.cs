using System.Data;

namespace DBChatAI.Blazor.Server.Services.Interface
{
    public interface ISafeSqlExecutor
    {
        Task<DataTable> ExecuteSafeSelectAsync(string sql);
    }
}
