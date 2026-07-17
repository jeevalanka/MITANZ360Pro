namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiTemplateResult
{
    public bool Success { get; set; }

    public string TemplateType { get; set; } =
        string.Empty;

    public string Content { get; set; } =
        string.Empty;

    public string SaveLocation { get; set; } =
        string.Empty;

    public List<string> Errors { get; set; } = [];
}
