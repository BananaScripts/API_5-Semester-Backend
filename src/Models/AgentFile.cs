using System.ComponentModel.DataAnnotations.Schema;

public class AgentFile
{
    [Column("file_id")]
    public int FileId { get; set; }
    [Column("agent_id")]
    public int AgentId { get; set; }
    [Column("file_name")]
    public string FileName { get; set; } = string.Empty;
    [Column("file_path")]
    public string FilePath { get; set; } = string.Empty;
    [Column("uploaded_by_user")]
    public int UploadedByUser { get; set; }
    [Column("uploaded_at")]
    public DateTime UploadedAt { get; set; }
}
