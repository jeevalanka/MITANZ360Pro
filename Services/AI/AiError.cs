namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiError
{
    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Details { get; set; }
}