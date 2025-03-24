using Microsoft.AspNetCore.Mvc;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("/")]
public class TesteController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok("Olá");
}
