using LLMChatbotApi.Services;
using LLMChatbotApi.Models;
using MongoDB.Driver;
using LLMChatbotApi.Enums;
using LLMChatbotApi.exceptions;

namespace LLMChatbotApi.Services;

public class MessageManagementService
{
    private readonly IMongoCollection<Chat> _chatsCollection;
    private readonly ILogger<MessageManagementService> _logger;

    public MessageManagementService(DatabaseMongoDBService databaseService, ILogger<MessageManagementService> logger)
    {
        _chatsCollection = databaseService.GetDatabase().GetCollection<Chat>("chats");
        _logger = logger;
    }

    public async Task<Chat> CreateChatAsync(string userId)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Criando chat com id de usuario '{0}", userId));
            var chat = new Chat { user_id = userId };
            await _chatsCollection.InsertOneAsync(chat);
            return chat;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha na criação de Chat: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }

    }
    public async Task<List<Chat>> GetUserChatsAsync(string userId)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Recuperando chats do usuário '{0}'", userId));

            return await _chatsCollection
                .Find(chat => chat.user_id == userId)
                .SortByDescending(chat => chat.UpdatedAt) // Ordena pelo mais recente
                .ToListAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha ao obter chats de usuario: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }

    }
    public async Task<Chat?> GetChatByIdAsync(string chatId)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Recuperando informações do chat '{0}'", chatId));
            return await _chatsCollection
                .Find(chat => chat.id == chatId)
                .FirstOrDefaultAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha ao obter chat via ID: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }

    public async Task<Chat?> GetChatByIdSortByUpdateAsync(string chatId)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Recuperando informações do chat '{0}'", chatId));
            return await _chatsCollection
                .Find(chat => chat.id == chatId)
                .SortByDescending(chat => chat.UpdatedAt)
                .FirstOrDefaultAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha ao obter Chat via ID: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }
    public async Task<List<Message>?> GetMessageFromChatAsync(string chatId)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Recuperando mensagens do chat '{0}'", chatId));
            var chat = await _chatsCollection
                .Find(chat => chat.id == chatId)
                .SortByDescending(chat => chat.UpdatedAt)
                .FirstOrDefaultAsync();

            return chat?.messages;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha ao obter mensagens do chat: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }

    public async Task<bool> AddMessageAsync(string chatId, Message message)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Enviando mensagem para o banco \n  Texto: '{0}'\n  Sender: '{1}'", message.Text, message.Sender));
            var update = Builders<Chat>.Update
                .Push(chat => chat.messages, message)
                .Set(chat => chat.UpdatedAt, DateTime.UtcNow);

            var result = await _chatsCollection.UpdateOneAsync(
                chat => chat.id == chatId,
                update
            );
            return result.ModifiedCount > 0;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha ao adicionar mensagem ao chat: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }
    public async Task<bool> ChangeChatStatusAsync(string chatId, ChatStatus newStatus)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Atualizando status do chat para '{0}'", newStatus));
            var update = Builders<Chat>.Update
                .Set(chat => chat.status, newStatus)
                .Set(chat => chat.UpdatedAt, DateTime.UtcNow);

            var result = await _chatsCollection.UpdateOneAsync(
                chat => chat.id == chatId,
                update
            );
            return result.ModifiedCount > 0;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha ao alterar status do Chat: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }
    public async Task<bool> DeleteChatAsync(string chatId)
    {
        try
        {
            _logger.Log(LogLevel.Information, string.Format("Deletando chat '{0}'", chatId));
            var result = await _chatsCollection.DeleteOneAsync(chat => chat.id == chatId);
            return result.DeletedCount > 0;
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Falha ao deletar Chat: {Message}", ex.Message);
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }

    public async Task<List<Chat>> GetAllChatsAsync()
    {
        try
        {
            return await _chatsCollection.Find(_ => true).ToListAsync();
        }
        catch (MongoException ex)
        {
            _logger.LogError(ex, "Erro ao buscar todos os chats");
            throw new DatabaseConnectionException("Erro MongoDB", ex);
        }
    }

}