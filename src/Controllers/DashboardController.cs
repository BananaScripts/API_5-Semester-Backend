using Microsoft.AspNetCore.Mvc;
using LLMChatbotApi.Services;
using LLMChatbotApi.Models;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
	private readonly MessageManagementService _chatService;
	private readonly ILogger<DashboardController> _logger;

	public DashboardController(MessageManagementService chatService, ILogger<DashboardController> logger)
	{
		_chatService = chatService;
		_logger = logger;
	}

	[HttpGet("stats")]
	[ProducesResponseType(StatusCodes.Status200OK)]
	public async Task<IActionResult> GetChatStats()
	{
		try
		{
			var chats = await _chatService.GetAllChatsAsync();

			var totalChats = chats.Count;
			var totalMessages = chats.Sum(c => c.messages?.Count ?? 0);
			var avgMessagesPerChat = totalChats > 0 ? (double)totalMessages / totalChats : 0;

			var uniqueUsers = chats.Select(c => c.user_id).Distinct().ToList();
			var totalUsers = uniqueUsers.Count;

			var topUsers = chats
				.GroupBy(c => c.user_id)
				.Select(g => new {
					userId = g.Key,
					chatCount = g.Count(),
					messageCount = g.Sum(c => c.messages?.Count ?? 0)
				})
				.OrderByDescending(g => g.messageCount)
				.Take(5);

			var allTexts = chats
				.SelectMany(c => c.messages ?? new List<Message>())
				.Select(m => m.Text ?? "")
				.Where(text => !string.IsNullOrWhiteSpace(text))
				.ToList();

			var stopWords = new HashSet<string> {
				// Palavras comuns em português
				"de", "a", "o", "que", "e", "do", "da", "em", "um", "para", "com", "não", "uma", "os", "no", "se", "na", "por",
				"mais", "as", "dos", "como", "mas", "foi", "ao", "ele", "das", "tem", "à", "seu", "sua", "ou", "ser", "quando",
				"já", "também", "nós", "eles", "ela", "isso", "este", "essa", "está", "tá", "sim", "bem", "bom", "dia", "tudo", "então",

				// Termos irrelevantes do sistema
				"mensagem", "recebida", "olá", "modo", "dev", "teste", "testando", "usuário", "agente", "chat",
				"resposta", "mensagens", "conversa", "responder", "usuários", "mensagens", "sistema",

				// Verbos e frases genéricas
				"poderia", "ajudar", "fazer", "tem", "ver", "preciso", "saber", "gostaria", "obrigado", "agradeço", "ok"
			};

			var wordFrequency = allTexts
				.SelectMany(text => text
					.ToLowerInvariant()
					.Replace(".", "")
					.Replace(",", "")
					.Replace("!", "")
					.Replace("?", "")
					.Replace(":", "")
					.Replace(";", "")
					.Split(' ', StringSplitOptions.RemoveEmptyEntries))
				.Where(word =>
					!stopWords.Contains(word) &&
					word.Length > 2 &&
					!word.All(char.IsDigit))
				.GroupBy(word => word)
				.ToDictionary(g => g.Key, g => g.Count());

			var topWords = wordFrequency
				.OrderByDescending(kvp => kvp.Value)
				.Take(10)
				.Select(kvp => new { word = kvp.Key, count = kvp.Value });

			var stats = new
			{
				total_chats = totalChats,
				total_messages = totalMessages,
				avg_messages_per_chat = avgMessagesPerChat,
				total_users = totalUsers,
				top_users = topUsers,
				top_words = topWords
			};

			return Ok(stats);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Erro ao gerar estatísticas de dashboard");
			return StatusCode(500, "Erro interno ao gerar estatísticas");
		}
	}
}
