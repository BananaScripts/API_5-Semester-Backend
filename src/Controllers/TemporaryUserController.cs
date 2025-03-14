using Microsoft.AspNetCore.Mvc;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("[controller]")]
public class TemporaryUserController : ControllerBase
{
    private static readonly string[] NameExamples = new[]
    {
        "Antonio", "Miguel", "Douglas", "Bruno", "Kaue"
    };

    private readonly ILogger<TemporaryUserController> _logger;

    public TemporaryUserController(ILogger<TemporaryUserController> logger)
    {
        _logger = logger;
    }

    [HttpGet(Name = "GetUsers")]
    public IEnumerable<TemporaryUser> Get()
    {
        Random rnd = new Random();

        // Gera alguns usuarios aleatórios para exemplificar e exibir no get
        return Enumerable.Range(1, 5).Select(index => new TemporaryUser
        {
            Name = NameExamples[Random.Shared.Next(NameExamples.Length)],
            Age = rnd.Next(20, 25)
        })
        .ToArray();
    }
}
