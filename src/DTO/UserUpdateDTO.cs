using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;

namespace LLMChatbotApi.DTO;

public class UserUpdateDTO
{
    [Column("user_name")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve conter pelo menos 3 caracteres")]
    public string? Name { get; set; }

    [Column("user_email")]
    [EmailAddress(ErrorMessage = "Deve ser um email válido")]
    public string? Email {set;get;}

    [Column("user_password")]
    [StringLength(256, MinimumLength = 6, ErrorMessage = "A senha deve conter pelo menos 6 caracteres")]
    public string? Password {set;get;}

    [Column("user_role")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "A role deve ser um número entre 0(YapperUser), 1(Curador) ou 2(Admin)")]
    public UserRole? Role {set;get;}
}