using LLMChatbotApi.Enums;
using LLMChatbotApi.Models;

namespace LLMChatbotApi.Interfaces;

public interface IAgentRepository
{
    Task<Agent> Create(Agent agent);
    Task<Agent> Update(Agent agent);
    Task Delete(int agentId);
    Task<Agent?> GetById(int agentId);
    Task<List<Agent>> GetByCreator(int userId);
    Task<(List<Agent> Agents, int TotalCount)> GetAllPaginated(int page, int pageSize);
    Task<bool> UpdateStatus(int agentId, AgentStatus newStatus);
    Task SaveAgentFileAsync(int agentId, string fileName, string filePath, int userId);
    Task<List<AgentFile>> GetAgentFilesAsync(int agentId);
    Task<AgentFile?> GetAgentFileByIdAsync(int fileId);
    Task<bool> DeleteAgentFileAsync(int fileId);
}