using LLMChatbotApi.Custom;
using LLMChatbotApi.DTO;
using LLMChatbotApi.Enums;
using LLMChatbotApi.Interfaces;
using LLMChatbotApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using LLMChatbotApi.Extensions;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("api/agent/[controller]")]
public class AgentController : ControllerBase
{
    private readonly IAgentRepository _agentRepository;
    private readonly ILogger<AgentController> _logger;
    private readonly IWebHostEnvironment _env;

    public AgentController(IAgentRepository agentRepository, ILogger<AgentController> logger, IWebHostEnvironment env)
    {
        _agentRepository = agentRepository;
        _logger = logger;
        _env = env;
    }

    private static AgentResponseDTO MapToDTO(Agent agent)
    {
        return new AgentResponseDTO
        {
            AgentId = agent.agent_id,
            Name = agent.agent_name,
            Description = agent.agent_description,
            Config = JsonSerializer.Deserialize<AgentConfigDTO>(agent.agent_config),
            Status = agent.agent_status,
            CreatedByUserId = agent.created_by_user,
            CreatedAt = agent.agent_created_at,
            UpdatedAt = agent.agent_updated_at
        };
    }

    [HttpPost]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> Create([FromBody] AgentCreateDTO request)
    {
        try
        {
            if (request == null)
                return BadRequest("Request body is null.");

            var agent = new Agent
            {
                agent_name = request.Name,
                agent_description = request.Description,
                agent_config = JsonSerializer.Serialize(request.Config),
                agent_status = AgentStatus.Pendente,
                created_by_user = User.GetUserId()
            };

            var createdAgent = await _agentRepository.Create(agent);
            return CreatedAtAction(nameof(GetById), new { id = createdAgent.agent_id }, MapToDTO(createdAgent));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar agente");
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var agent = await _agentRepository.GetById(id);
            return agent != null ? Ok(MapToDTO(agent)) : NotFound("Agent not found.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar agente ID: {AgentId}", id);
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> Update(int id, [FromBody] AgentUpdateDTO request)
    {
        try
        {
            if (request == null)
                return BadRequest("Request body is null.");

            var existingAgent = await _agentRepository.GetById(id);
            if (existingAgent == null) return NotFound("Agent not found.");

            if (!User.IsInRole("Admin") && existingAgent.created_by_user != User.GetUserId())
                return Forbid();

            existingAgent.agent_name = request.Name ?? existingAgent.agent_name;
            existingAgent.agent_description = request.Description ?? existingAgent.agent_description;

            if (request.Config != null)
                existingAgent.agent_config = JsonSerializer.Serialize(request.Config);

            if (request.Status.HasValue && User.IsInRole("Admin"))
                existingAgent.agent_status = request.Status.Value;

            var updatedAgent = await _agentRepository.Update(existingAgent);
            return Ok(MapToDTO(updatedAgent));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar agente ID: {AgentId}", id);
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var agent = await _agentRepository.GetById(id);
            if (agent == null) return NotFound("Agent not found.");

            await _agentRepository.Delete(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir agente ID: {AgentId}", id);
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var (agents, totalCount) = await _agentRepository.GetAllPaginated(page, pageSize);

            var response = new PaginatedResponse<AgentResponseDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = agents.Select(MapToDTO).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar agentes");
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpPatch("{id}/status")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> ChangeStatus(int id, [FromBody] AgentStatusChangeDTO request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var updated = await _agentRepository.UpdateStatus(id, request.NewStatus);
        if (!updated)
            return NotFound("Agent not found.");

        var agent = await _agentRepository.GetById(id);
        if (agent == null)
            return NotFound("Agent not found after update.");

        return Ok(MapToDTO(agent));
    }

    [HttpGet("creator/{userId}")]
    public async Task<IActionResult> GetByCreator(int userId)
    {
        try
        {
            var agents = await _agentRepository.GetByCreator(userId);
            return Ok(agents.Select(MapToDTO));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar agentes do criador {UserId}", userId);
            return StatusCode(500, "Erro interno");
        }
    }
    
    [HttpPost("{id}/upload")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> UploadFiles(int id, List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
            return BadRequest("Nenhum arquivo enviado.");

        var uploadsPath = Path.Combine(_env.ContentRootPath, "Uploads", "Agents", id.ToString());
        Directory.CreateDirectory(uploadsPath);

        foreach (var file in files)
        {
            var safeFileName = Path.GetFileName(file.FileName);
            var filePath = Path.Combine(uploadsPath, safeFileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            await _agentRepository.SaveAgentFileAsync(id, safeFileName, filePath, User.GetUserId());
        }

        return Ok(new { Message = "Arquivos enviados com sucesso." });
    }

    [HttpGet("{id}/files")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> GetFiles(int id)
    {
        var files = await _agentRepository.GetAgentFilesAsync(id);
        return Ok(files);
    }

    [HttpGet("file/{fileId}")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> DownloadFile(int fileId)
    {
        var file = await _agentRepository.GetAgentFileByIdAsync(fileId);
        if (file == null || !System.IO.File.Exists(file.FilePath))
            return NotFound("Arquivo não encontrado.");

        var memory = new MemoryStream();
        using (var stream = new FileStream(file.FilePath, FileMode.Open))
        {
            await stream.CopyToAsync(memory);
        }

        memory.Position = 0;
        return File(memory, "application/octet-stream", file.FileName);
    }

    [HttpDelete("file/{fileId}")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> DeleteFile(int fileId)
    {
        var file = await _agentRepository.GetAgentFileByIdAsync(fileId);
        if (file == null)
            return NotFound("Arquivo não encontrado.");

        if (System.IO.File.Exists(file.FilePath))
            System.IO.File.Delete(file.FilePath);

        var deleted = await _agentRepository.DeleteAgentFileAsync(fileId);
        return deleted ? NoContent() : StatusCode(500, "Erro ao excluir arquivo.");
    }
}
