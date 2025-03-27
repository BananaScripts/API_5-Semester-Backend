using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LLMChatbotApi.DTO;

public class LoginRequestDTO
{
    [Required]
    [Column("user_email")]
    [EmailAddress(ErrorMessage = "Deve ser um email válido")]
    public required string Email {set;get;}

    [Required]
    [Column("user_password")]
    [StringLength(256, MinimumLength = 6, ErrorMessage = "A senha deve conter pelo menos 6 caracteres")]
    public required string Password {set;get;}

}