using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;
using Microsoft.VisualBasic;

namespace LLMChatbotApi.Models;

[Table("user")]
public class User
{
    [Key]
    [Column("user_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int user_id{set;get;}

    [Required]
    [Column("user_name")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome deve conter pelo menos 3 caracteres")]
    public required string user_name {set;get;}
    
    [Required]
    [Column("user_email")]
    [EmailAddress(ErrorMessage = "Deve ser um email válido")]
    public required string user_email {set;get;}

    [Required]
    [Column("user_password")]
    [StringLength(256, MinimumLength = 6, ErrorMessage = "A senha deve conter pelo menos 6 caracteres")]
    public required string user_password {set;get;}

    [Required]
    [Column("user_role")]
    [EnumDataType(typeof(UserRole), ErrorMessage = "A role deve ser um número entre 0(YapperUser), 1(Curador) ou 2(Admin)")]
    public required UserRole user_role {set;get;}

    [Column("user_created_at")]
    public DateTime? user_created_at {set;get;}

    [Column("user_updated_at")]
    public DateTime? user_updated_at {set;get;}
};