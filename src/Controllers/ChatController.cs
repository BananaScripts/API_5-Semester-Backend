using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using LLMChatbotApi.DTO;
using System.Runtime.InteropServices;
using StackExchange.Redis;
using LLMChatbotApi.Services;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
	private readonly MessageManagementService _chatService;
	private readonly ILogger<ChatController> _logger;
	private readonly DatabaseRedisService _redisService;
	public ChatController(MessageManagementService chatService, ILogger<ChatController> logger, DatabaseRedisService redisS)
	{
		_chatService = chatService;
		_logger = logger;
		_redisService = redisS;
	}

	[HttpPost]
	public async Task<IActionResult> CreateChat([FromBody] string userId)
	{
		var chat = await _chatService.CreateChatAsync(userId);
		return CreatedAtAction(nameof(GetChatById), new { chatId = chat.id }, chat);
	}

	[HttpGet("user/{userId}")]
	public async Task<IActionResult> GetUserChats(string userId)
	{
		var chats = await _chatService.GetUserChatsAsync(userId);
		return Ok(chats);
	}

	[HttpGet("{chatId}")]
	public async Task<IActionResult> GetChatById(string chatId)
	{
		var chat = await _chatService.GetChatByIdAsync(chatId);
		if (chat == null) return NotFound();
		return Ok(chat);
	}

	[HttpPost("{chatId}/messages")]
	public async Task<IActionResult> AddMessage(string chatId, [FromBody] Message message)
	{
		var success = await _chatService.AddMessageAsync(chatId, message);
		if (!success) return NotFound();
		return NoContent();
	}

	[HttpPatch("{chatId}/close")]
	public async Task<IActionResult> CloseChat(string chatId)
	{
		var success = await _chatService.ChangeChatStatusAsync(chatId, ChatStatus.CLOSED);
		if (!success) return NotFound();
		return NoContent();
	}
	[HttpDelete("{chatId}")]
	public async Task<IActionResult> DeleteChat(string chatId)
	{
		var success = await _chatService.DeleteChatAsync(chatId);
		if (!success) return NotFound();
		return NoContent();
	}
	[HttpPatch("{chatId}/open")]
	public async Task<IActionResult> OpenChat(string chatId)
	{
		var success = await _chatService.ChangeChatStatusAsync(chatId, ChatStatus.OPEN);
		if (!success) return NotFound();
		return NoContent();
	}


[HttpGet("/ws/chat/open/{userId}")]
[Authorize(Roles = "Admin,Curador")]
public async Task WebSocketHandler(
    [FromRoute] string userId,
    [FromServices] DatabaseRedisService redisService)
{
    if (!HttpContext.WebSockets.IsWebSocketRequest)
    {
        HttpContext.Response.StatusCode = 400;
        _logger.LogError("Tentativa de conexão inválida com websocket");
        return;
    }

    using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
    var buffer = new byte[1024 * 4];
    var pubsub = redisService.GetDatabase().Multiplexer.GetSubscriber();
    using var cts = new CancellationTokenSource();
    var token = cts.Token;

    WSRequestDTO? currentRequest = null;
    bool subscribed = false;
    RedisChannel? userChannel = null;

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexão encerrada", token);

                if (currentRequest?.ChatId != null)
                {
                    var success = await _chatService.ChangeChatStatusAsync(currentRequest.ChatId, ChatStatus.CLOSED);
                    if (!success) _logger.LogError("Erro ao fechar o chat");
                }

                break;
            }

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            WSRequestDTO? payload;
            try
            {
                payload = JsonSerializer.Deserialize<WSRequestDTO>(json);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao desserializar JSON recebido via WebSocket");
                continue;
            }

            if (payload == null)
            {
                _logger.LogWarning("Payload WebSocket nulo");
                continue;
            }

            currentRequest = payload;

            if (!subscribed)
            {
                userChannel = RedisChannel.Literal($"user:{currentRequest.UserId}:responses");
                await pubsub.SubscribeAsync((RedisChannel)userChannel, async (_, message) =>
                {
                    if (webSocket.State != WebSocketState.Open)
                    {
                        _logger.LogWarning("WebSocket fechado. Não é possível enviar mensagem.");
                        return;
                    }

                    if (currentRequest.ChatId != null)
                    {
                        var success = await _chatService.ChangeChatStatusAsync(currentRequest.ChatId, ChatStatus.OPEN);
                        if (!success) _logger.LogError("Erro ao abrir o chat");
                    }

                    _logger.LogInformation("Mensagem recebida do Redis: {Message}", message);
                    var msgBytes = Encoding.UTF8.GetBytes(message);
                    try
                    {
                        await webSocket.SendAsync(
                            new ArraySegment<byte>(msgBytes),
                            WebSocketMessageType.Text,
                            true,
                            token
                        );
                        _logger.LogInformation("Resposta enviada ao WebSocket.");

                        // #TODO Deixar a parte do banco consistente
                        if (currentRequest.ChatId != null)
                        {
                            var success = await _chatService.AddMessageAsync(
                                currentRequest.ChatId,
								// #TODO Sender (no caso aqui "agent") com nome inconsistente no banco
                                new Message("agent", message, DateTime.UtcNow)
                            );
                            if (!success) _logger.LogError("Erro ao adicionar a mensagem da IA no banco");
                        }

                    }
                    catch (WebSocketException ex)
                    {
                        _logger.LogError(ex, "Erro ao enviar mensagem via WebSocket");
                        cts.Cancel();
                    }
                });
                subscribed = true;
            }

            var wsMessage = new
            {
                conversation_id = Guid.NewGuid().ToString(),
                user_id = payload.UserId,
                agent_id = payload.AgentId,
                message = payload.Text
            };

            _logger.LogInformation("Mensagem sendo enviada ao Redis: {Message}", JsonSerializer.Serialize(wsMessage));

            // #TODO Deixar a parte do banco consistente
            if (currentRequest.ChatId != null)
            {
                var success = await _chatService.AddMessageAsync(
                    currentRequest.ChatId,
					// #TODO Sender (no caso aqui "user") com nome inconsistente no banco
                    new Message("user", wsMessage.message, DateTime.UtcNow)
                );
                if (!success) _logger.LogError("Erro ao adicionar a mensagem do usuário no banco");
            }

            await pubsub.PublishAsync(
                RedisChannel.Literal("chat_messages"),
                JsonSerializer.Serialize(wsMessage)
            );
        }
    }
    finally
    {
        if (userChannel.HasValue)
            await pubsub.UnsubscribeAsync(userChannel.Value);
        cts.Cancel();
    }
}
