using System.ComponentModel.DataAnnotations;
using LLMChatbotApi.Custom;
using LLMChatbotApi.DTO;
using LLMChatbotApi.Interfaces;
using LLMChatbotApi.Models;
using LLMChatbotApi.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LLMChatbotApi.Controllers;

[ApiController]
[Route("api/user/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserRepository userRepository, ILogger<UserController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;

    }

    private static UserResponseDTO MapToDTO(User user)
    {
        return new UserResponseDTO
        {
            Id = user.user_id,
            Name = user.user_name,
            Email = user.user_email,
            Role = user.user_role,
            CreatedAt = user.user_created_at,
            UpdatedAt = user.user_updated_at
        };
    }

    [HttpPost]
    [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Create([FromBody] UserCreateDTO request)
    {
        try
        {
            var user = new User
            {
                user_name = request.Name,
                user_email = request.Email,
                user_password = request.Password,
                user_role = request.Role
            };

            var createdUser = await _userRepository.Create(user);
            return CreatedAtAction(nameof(GetById), new { id = createdUser.user_id }, MapToDTO(createdUser));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar usuário");
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpGet("{id}")]
    [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var user = await _userRepository.GetById(id);
            return user != null ? Ok(MapToDTO(user)) : NotFound();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao buscar usuário ID: {UserId}", id);
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpPut("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(UserResponseDTO), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Update(int id, [FromBody] UserUpdateDTO request)
    {
        try
        {
            var existingUser = await _userRepository.GetById(id);
            if (existingUser == null) return NotFound();

            var isAdmin = User.IsInRole("Admin");
            if (request.Role.HasValue && request.Role.Value != existingUser.user_role && !isAdmin)
            {
                return StatusCode(StatusCodes.Status403Forbidden, "Somente administradores podem alterar roles");
            }

            existingUser.user_name = request.Name ?? existingUser.user_name;
            existingUser.user_email = request.Email ?? existingUser.user_email;
            existingUser.user_password = request.Password ?? existingUser.user_password;
            existingUser.user_role = request.Role ?? existingUser.user_role;

            if (!string.IsNullOrEmpty(request.Password))
            {
                existingUser.user_password = BCrypt.Net.BCrypt.HashPassword(request.Password);
            }

            var updatedUser = await _userRepository.Update(existingUser);
            return Ok(MapToDTO(updatedUser));
        }
        catch (ValidationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao atualizar usuário ID: {UserId}", id);
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> DeleteById(int id)
    {
        try
        {
            var user = await _userRepository.GetById(id);
            if (user == null) return NotFound();

            await _userRepository.DeleteById(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir usuário ID: {UserId}", id);
            return StatusCode(500, "Erro interno");
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(PaginatedResponse<UserResponseDTO>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        try
        {
            var (users, totalCount) = await _userRepository.GetAllPaginated(page, pageSize);

            var response = new PaginatedResponse<UserResponseDTO>
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalCount,
                TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize),
                Items = users.Select(MapToDTO).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar usuários");
            return StatusCode(500, "Erro interno");
        }
    }

}