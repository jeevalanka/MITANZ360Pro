using System.ComponentModel.DataAnnotations;
namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiWorkflowRequest
{
    [Required]
    public AiTaskType TaskType { get; set; }

    [Required]
    public string Prompt { get; set; } =
        string.Empty;

    public string? SystemPrompt { get; set; }

    public AiOutputMode OutputMode { get; set; } = AiOutputMode.Text;

    public string? UserId { get; set; }

    public string? CorrelationId { get; set; }

    public Dictionary<string, object>? Metadata { get; set; }

    public bool StrictJsonResponse { get; set; } = true;

    public double Temperature { get; set; } = 0.2;

    public int MaxTokens { get; set; } = 4000;
}
