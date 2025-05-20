namespace LLMChatbotApi.DTO;

public class AgentFileDTO
{
    public int Id { get; set; }
    public int AgentId { get; set; }
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public DateTime UploadedAt { get; set; }
    public int UploadedByUser { get; set; }
}
