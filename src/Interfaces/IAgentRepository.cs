using LLMChatbotApi.Enums;
using LLMChatbotApi.Models;
using Microsoft.AspNetCore.Mvc;

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
    Task AddUsersToAgentPermission(int agentId, List<int> userIds);
    Task<bool> HasUserPermissionForAgent(int userId, int agentId);
    Task<List<User>> GetUsersWithPermission(int agentId);

}