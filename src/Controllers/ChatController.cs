using LLMChatbotApi.Enums;
using LLMChatbotApi.Models;
using LLMChatbotApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using LLMChatbotApi.DTO;
using System.Runtime.InteropServices;
using StackExchange.Redis;
using LLMChatbotApi.Repositories;
using LLMChatbotApi.Interfaces;

namespace LLMChatbotApi.Controllers;
/// <summary>
/// Controller responsável por gerenciar chats e comunicação via WebSocket.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly MessageManagementService _chatService;
    private readonly ILogger<ChatController> _logger;
    private readonly DatabaseRedisService _redisService;
    private readonly IAgentRepository _agentRepository;

    public ChatController(
        MessageManagementService chatService,
        ILogger<ChatController> logger,
        DatabaseRedisService redisS,
        IAgentRepository agentRepository)
    {
        _chatService = chatService;
        _logger = logger;
        _redisService = redisS;
        _agentRepository = agentRepository;
    }


    /// <summary>
    /// Cria um novo chat para o usuário especificado.
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <returns>Objeto do chat criado</returns>
    [HttpPost]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateChat([FromBody] string userId)
    {
        var chat = await _chatService.CreateChatAsync(userId);
        return CreatedAtAction(nameof(GetChatById), new { chatId = chat.id }, chat);
    }

    /// <summary>
    /// Retorna todos os chats de um usuário.
    /// </summary>
    /// <param name="userId">ID do usuário</param>
    /// <returns>Lista de chats</returns>
    [HttpGet("user/{userId}")]
    [ProducesResponseType(typeof(IEnumerable<Chat>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUserChats(string userId)
    {
        var chats = await _chatService.GetUserChatsAsync(userId);
        return Ok(chats);
    }

    /// <summary>
    /// Retorna um chat específico pelo seu ID.
    /// </summary>
    /// <param name="chatId">ID do chat</param>
    /// <returns>Chat correspondente</returns>
    [HttpGet("chat/{chatId}")]
    [ProducesResponseType(typeof(Chat), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetChatById(string chatId)
    {
        var chat = await _chatService.GetChatByIdAsync(chatId);
        if (chat == null) return NotFound();
        return Ok(chat);
    }

    /// <summary>
    /// Fecha um chat existente.
    /// </summary>
    /// <param name="chatId">ID do chat</param>
    [HttpPatch("chat/{chatId}/close")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CloseChat(string chatId)
    {
        var success = await _chatService.ChangeChatStatusAsync(chatId, ChatStatus.CLOSED);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Reabre um chat fechado.
    /// </summary>
    /// <param name="chatId">ID do chat</param>
    [HttpPatch("chat/{chatId}/open")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> OpenChat(string chatId)
    {
        var success = await _chatService.ChangeChatStatusAsync(chatId, ChatStatus.OPEN);
        if (!success) return NotFound();
        return NoContent();
    }

    /// <summary>
    /// Exclui um chat pelo seu ID.
    /// </summary>
    /// <param name="chatId">ID do chat</param>
    [HttpDelete("chat/{chatId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteChat(string chatId)
    {
        var success = await _chatService.DeleteChatAsync(chatId);
        if (!success) return NotFound();
        return NoContent();
    }

[HttpGet("/ws/chat/open")]
public async Task WebSocketHandler(
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

			var hasPermission = await _agentRepository.HasUserPermissionForAgent(Int32.Parse(currentRequest.UserId), Int32.Parse(currentRequest.AgentId));
			if (!hasPermission)
			{
				_logger.LogWarning("Usuário {UserId} sem permissão para agente {AgentId}", currentRequest.UserId, currentRequest.AgentId);
				var errorPayload = new { error = "user not allowed" };
				var errorMessage = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(errorPayload));
				await webSocket.SendAsync(new ArraySegment<byte>(errorMessage), WebSocketMessageType.Text, true, token);
				continue;
			}

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

					try
					{
						var redisResponse = JsonSerializer.Deserialize<JsonElement>(message!);

						var adaptedResponse = new
						{
							conversation_id = redisResponse.GetProperty("conversation_id").GetString(),
							chat_id = currentRequest.ChatId,
							user_id = currentRequest.UserId,
							agent_id = currentRequest.AgentId,
							message = redisResponse.TryGetProperty("message", out var msgProp)
								? msgProp.GetString()
								: redisResponse.GetProperty("text").GetString()
						};

						var msgBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(adaptedResponse));

						await webSocket.SendAsync(
							new ArraySegment<byte>(msgBytes),
							WebSocketMessageType.Text,
							true,
							token
						);

						if (currentRequest.ChatId != null)
						{
							var success = await _chatService.AddMessageAsync(
								currentRequest.ChatId,
								new Message("agent", adaptedResponse.message, DateTime.UtcNow)
							);
							if (!success) _logger.LogError("Erro ao adicionar a mensagem da IA no banco");
						}
					}
					catch (Exception ex)
					{
						_logger.LogError(ex, "Erro ao adaptar e enviar mensagem da IA via WebSocket");
						cts.Cancel();
					}
				});
				subscribed = true;
			}

			if (payload.Dev == true)
			{
				var simulatedResponse = $"Mensagem recebida, olá! {DateTime.UtcNow:HH:mm:ss}";
				var simulatedMessage = new
				{
					conversation_id = Guid.NewGuid().ToString(),
					chat_id = payload.ChatId,
					user_id = payload.UserId,
					agent_id = payload.AgentId,
					message = simulatedResponse
				};

				var msgBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(simulatedMessage));

				await webSocket.SendAsync(
					new ArraySegment<byte>(msgBytes),
					WebSocketMessageType.Text,
					true,
					token
				);

				if (currentRequest.ChatId != null)
				{
					var successUser = await _chatService.AddMessageAsync(
						currentRequest.ChatId,
						new Message("user", payload.Text, DateTime.UtcNow)
					);
					var successAgent = await _chatService.AddMessageAsync(
						currentRequest.ChatId,
						new Message("agent", simulatedResponse, DateTime.UtcNow)
					);

					if (!successUser || !successAgent)
						_logger.LogError("Erro ao salvar mensagens de dev no banco");
				}

				continue;
			}

			var wsMessage = new
			{
				conversation_id = Guid.NewGuid().ToString(),
				chat_id = payload.ChatId,
				user_id = payload.UserId,
				agent_id = payload.AgentId,
				message = payload.Text
			};

			_logger.LogInformation("Mensagem sendo enviada ao Redis: {Message}", JsonSerializer.Serialize(wsMessage));

			if (currentRequest.ChatId != null)
			{
				var success = await _chatService.AddMessageAsync(
					currentRequest.ChatId,
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


}