namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiJsonParseResult
{
    public bool Success { get; set; }

    public object? Result { get; set; }

    public List<string> Errors { get; set; } = [];
}
