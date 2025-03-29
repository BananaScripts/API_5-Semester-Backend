using LLMChatbotApi.Models;

namespace LLMChatbotApi.Interfaces;

public interface IUserRepository
{
    Task<User> Create(User user);
    Task<User> Update(User user);
    Task DeleteById(int userId);
    Task DeleteByEmail(string userEmail);
    Task<User?> GetById(int userId);
    Task<User?> GetByEmail(string userEmail);
    Task<(List<User> Users, int TotalCount)> GetAllPaginated(int page, int pageSize);
}