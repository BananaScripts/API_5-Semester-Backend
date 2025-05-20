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

    public AgentController(IAgentRepository agentRepository, ILogger<AgentController> logger)
    {
        _agentRepository = agentRepository;
        _logger = logger;
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
    [ProducesResponseType(typeof(AgentResponseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(AgentResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(AgentResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(PaginatedResponse<AgentResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(AgentResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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
    [ProducesResponseType(typeof(List<AgentResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

    [HttpPost("{agentId}/permissions")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> AddPermission(int agentId, [FromBody] List<int> userIds)
    {
        if (userIds == null || userIds.Count == 0)
            return BadRequest("Nenhum usuário fornecido.");

        try
        {
            await _agentRepository.AddUsersToAgentPermission(agentId, userIds);
            return Ok("Permissões adicionadas.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar permissões para o agente {AgentId}", agentId);
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpGet("{agentId}/permissions")]
    [Authorize(Roles = "Admin,Curador")]
    public async Task<IActionResult> GetPermissionsByAgent(int agentId)
    {
        if (agentId <= 0)
            return BadRequest("Id de agente inválido.");

        try
        {
            var userIds = await _agentRepository.GetUsersWithPermission(agentId);
            return Ok(userIds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar permissões do agente {AgentId}", agentId);
            return StatusCode(500, "Erro interno");
        }
    }


}