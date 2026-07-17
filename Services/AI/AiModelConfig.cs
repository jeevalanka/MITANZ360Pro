namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiModelConfig
{
    public AiProviderType Provider { get; set; }

    public string Model { get; set; } =
        string.Empty;

    public decimal EstimatedCostPer1KTokens { get; set; }

    public bool SupportsJsonMode { get; set; }

    public bool SupportsVision { get; set; }

    public bool IsPreferred { get; set; }
}
