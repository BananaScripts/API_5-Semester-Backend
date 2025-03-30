using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using LLMChatbotApi.Enums;

public class UserResponseDTO
{
    [Key]
    [Required]
    [Column("user_id")]
    public int Id {set;get;}

    [Required]
    [Column("user_name")]
    public required string Name { get; set; }

    [Required]
    [Column("user_email")]
    public required string Email {set;get;}

    [Required]
    [Column("user_role")]
    public required UserRole Role {set;get;}

    [Required]
    [Column("user_created_at")]
    public DateTime? CreatedAt {set;get;}

    [Required]
    [Column("user_updated_at")]
    public DateTime? UpdatedAt {set;get;}
}