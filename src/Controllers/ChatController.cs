using LLMChatbotApi.Enums;
using LLMChatbotApi.Models;
using LLMChatbotApi.Services;
using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using StackExchange.Redis;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
	private readonly MessageManagementService _chatService;

	public ChatController(MessageManagementService chatService)
	{
		_chatService = chatService;
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
	[HttpGet("/ws/chat/{userId}")]
	[Authorize(Roles = "Admin,Curador")]
	public async Task GetWebSocket(
		string userId,
		[FromQuery] int agentId,
		[FromServices] DatabaseRedisService redisService)
	{
		if (!HttpContext.WebSockets.IsWebSocketRequest)
		{
			HttpContext.Response.StatusCode = 400;
			return;
		}

		using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
		var buffer = new byte[1024 * 4];
		var pubsub = redisService.GetDatabase().Multiplexer.GetSubscriber();
		using var cts = new CancellationTokenSource();
		var token = cts.Token;

		// Escuta respostas e envia via WebSocket
		var listener = Task.Run(async () =>
		{
			await pubsub.SubscribeAsync(RedisChannel.Pattern($"user:{userId}:responses"), async (channel, message) =>
			{
				if (webSocket.State != WebSocketState.Open) return;

				var msgBytes = Encoding.UTF8.GetBytes(message!);
				try
				{
					await webSocket.SendAsync(
						new ArraySegment<byte>(msgBytes),
						WebSocketMessageType.Text,
						true,
						token
					);
				}
				catch (WebSocketException) { }
			});
		}, token);

		while (webSocket.State == WebSocketState.Open)
		{
			var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
			if (result.MessageType == WebSocketMessageType.Close)
			{
				await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Conexão encerrada", token);
				break;
			}

			var userMessage = Encoding.UTF8.GetString(buffer, 0, result.Count);

			var payload = new
			{
				conversation_id = Guid.NewGuid().ToString(),
				user_id = userId,
				agent_id = agentId,
				message = userMessage
			};

			var payloadJson = JsonSerializer.Serialize(payload);
			await pubsub.PublishAsync(RedisChannel.Literal("chat_messages"), payloadJson);
		}

		cts.Cancel();
	}
}