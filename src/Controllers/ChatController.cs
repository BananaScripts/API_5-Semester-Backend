using LLMChatbotApi.Enums;
using LLMChatbotApi.Models;
using LLMChatbotApi.Services;
using Microsoft.AspNetCore.Mvc;

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
}