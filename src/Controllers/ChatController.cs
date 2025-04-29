using Microsoft.AspNetCore.Mvc;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using StackExchange.Redis;
using Microsoft.Extensions.Logging;
using LLMChatbotApi.Services;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly MessageManagementService _chatService;
    private readonly DatabaseRedisService _redisService;
    private readonly ILogger<ChatController> _logger;

    public ChatController(
        MessageManagementService chatService,
        DatabaseRedisService redisService,
        ILogger<ChatController> logger)
    {
        _chatService = chatService;
        _redisService = redisService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> CreateChat([FromBody] string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest("User ID is required");

        try
        {
            var chat = await _chatService.CreateChatAsync(userId);
            return CreatedAtAction(nameof(GetChatById), new { chatId = chat.id }, chat);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating chat");
            return StatusCode(500, "Internal server error");
        }
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
        return chat == null ? NotFound() : Ok(chat);
    }

    [HttpPost("{chatId}/messages")]
    public async Task<IActionResult> AddMessage(
        string chatId,
        [FromQuery] int agentId,
        [FromBody] Message message)
    {
        var chat = await _chatService.GetChatByIdAsync(chatId);
        if (chat == null) return NotFound();

        var modelMessage = new LLMChatbotApi.Models.Message
        {
            Sender = message.Sender,
            Text = message.Text
        };
        var saved = await _chatService.AddMessageAsync(chatId, modelMessage);
        if (!saved) return NotFound();

        var conversationId = Guid.NewGuid().ToString();
        var pubsub = _redisService.GetDatabase().Multiplexer.GetSubscriber();
        var tcs = new TaskCompletionSource<string>();

        var responseChannel = RedisChannel.Literal($"user:{chat.user_id}:responses");
        await pubsub.SubscribeAsync(responseChannel, (_, msg) =>
        {
            try
            {
                var response = JsonSerializer.Deserialize<RedisResponse>(msg);
                if (response?.conversation_id == conversationId)
                {
                    tcs.TrySetResult(response.text);
                    _logger.LogInformation("Received response: {text}", response.text);
                }
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
                _logger.LogError(ex, "Response processing error");
            }
        });

        var requestPayload = new
        {
            conversation_id = conversationId,
            user_id = chat.user_id,
            agent_id = agentId,
            message = message.Text
        };
        
        await pubsub.PublishAsync(RedisChannel.Literal("chat_messages"), JsonSerializer.Serialize(requestPayload));

        var timeout = Task.Delay(TimeSpan.FromSeconds(15));
        var completedTask = await Task.WhenAny(tcs.Task, timeout);
        
        await pubsub.UnsubscribeAsync(responseChannel);

        if (completedTask == timeout)
            return StatusCode(504, "LLM response timeout");

        var aiText = await tcs.Task;
        await _chatService.AddMessageAsync(chatId, new LLMChatbotApi.Models.Message { Sender = "assistant", Text = aiText });

        return Ok(new { conversation_id = conversationId, text = aiText });
    }

    [HttpPatch("{chatId}/close")]
    public async Task<IActionResult> CloseChat(string chatId)
    {
        var success = await _chatService.ChangeChatStatusAsync(chatId, LLMChatbotApi.Enums.ChatStatus.CLOSED);
        return success ? NoContent() : NotFound();
    }

    [HttpDelete("{chatId}")]
    public async Task<IActionResult> DeleteChat(string chatId)
    {
        var success = await _chatService.DeleteChatAsync(chatId);
        return success ? NoContent() : NotFound();
    }

    [HttpPatch("{chatId}/open")]
    public async Task<IActionResult> OpenChat(string chatId)
    {
        var success = await _chatService.ChangeChatStatusAsync(chatId, LLMChatbotApi.Enums.ChatStatus.OPEN);
        return success ? NoContent() : NotFound();
    }

    [HttpGet("/ws/chat/{userId}")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task WebSocketHandler(
        string userId,
        [FromQuery] int agentId)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = 400;
            return;
        }

        using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        var pubsub = _redisService.GetDatabase().Multiplexer.GetSubscriber();
        var cts = new CancellationTokenSource();

        var responseChannel = RedisChannel.Literal($"user:{userId}:responses");
        await pubsub.SubscribeAsync(responseChannel, async (_, msg) =>
        {
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.SendAsync(
                        Encoding.UTF8.GetBytes(msg),
                        WebSocketMessageType.Text,
                        true,
                        cts.Token
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WebSocket send error");
                cts.Cancel();
            }
        });

        var buffer = new byte[1024 * 4];
        try
        {
            while (webSocket.State == WebSocketState.Open && !cts.Token.IsCancellationRequested)
            {
                var result = await webSocket.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await webSocket.CloseAsync(
                        WebSocketCloseStatus.NormalClosure,
                        "Client closed",
                        cts.Token);
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

                await pubsub.PublishAsync(
                    RedisChannel.Literal("chat_messages"),
                    JsonSerializer.Serialize(payload)
                );
            }
        }
        finally
        {
            await pubsub.UnsubscribeAsync(responseChannel);
            cts.Cancel();
        }
    }

    private class RedisResponse
    {
        public string conversation_id { get; set; }
        public string text { get; set; }
    }
}

public class Message
{
    public string Sender { get; set; }
    public string Text { get; set; }
}

public enum ChatStatus
{
    OPEN,
    CLOSED
}