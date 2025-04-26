using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using LLMChatbotApi.DTO;
using LLMChatbotApi.Enums;

namespace LLMChatbotApi.Models;

[Table("agent")]
public class Agent
{
    [Key]
    [Column("agent_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int agent_id { get; set; }

    [Required]
    [Column("agent_name")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "O nome do agente deve ter entre 3 e 100 caracteres")]
    public required string agent_name { get; set; }

    [Column("agent_description")]
    public string? agent_description { get; set; }

    [Required]
    [Column("agent_config")]
    public required string agent_config { get; set; }

    [NotMapped]
    public AgentConfigDTO Config
    {
        get => JsonSerializer.Deserialize<AgentConfigDTO>(agent_config) ?? new AgentConfigDTO();
        set => agent_config = JsonSerializer.Serialize(value);
    }

    [Required]
    [Column("agent_status")]
    [EnumDataType(typeof(AgentStatus))]
    public AgentStatus agent_status { get; set; } = AgentStatus.Pendente;

    [Required]
    [Column("created_by_user")]
    public int created_by_user { get; set; }

    [Column("agent_created_at")]
    public DateTime agent_created_at { get; set; } = DateTime.UtcNow;

    [Column("agent_updated_at")]
    public DateTime agent_updated_at { get; set; } = DateTime.UtcNow;
}