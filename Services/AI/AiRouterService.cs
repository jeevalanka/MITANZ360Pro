using Microsoft.Extensions.Logging;

namespace MITANZ360Pro.Web.Services.AI;

public sealed class AiRouterService
{
    private readonly ILogger<AiRouterService> _logger;

    public AiRouterService(
        ILogger<AiRouterService> logger)
    {
        _logger = logger;
    }

    public AiModelConfig ResolveModel(
        AiTaskType taskType)
    {
        _logger.LogInformation(
            "Resolving AI model for task type: {TaskType}",
            taskType);

        return taskType switch
        {
            // =========================
            // CHAT
            // =========================
            AiTaskType.Chat =>
                CreateAzureOpenAiChatModel(),

            // =========================
            // CODING
            // =========================
            AiTaskType.Coding =>
                CreateAzureOpenAiChatModel(),

            // =========================
            // ASSESSMENT
            // =========================
            AiTaskType.Assessment =>
                CreateAzureOpenAiChatModel(),

            // =========================
            // REPORTS
            // =========================
            AiTaskType.ReportGeneration =>
                CreateAzureOpenAiChatModel(),

            // =========================
            // FILE ANALYSIS
            // =========================
            AiTaskType.FileAnalysis =>
                CreateAzureOpenAiChatModel(),

            // =========================
            // METADATA VALIDATION
            // =========================
            AiTaskType.MetadataValidation =>
                CreateAzureOpenAiChatModel(),

            // =========================
            // OCR / VISION
            // =========================
            AiTaskType.OCR =>
                CreateOpenRouterVisionModel(),

            // =========================
            // GOVERNANCE
            // =========================
            AiTaskType.Governance =>
                CreateAzureOpenAiChatModel(),

            // =========================
            // DEFAULT
            // =========================
            _ =>
                CreateAzureOpenAiChatModel()
        };
    }

    private static AiModelConfig CreateAzureOpenAiChatModel()
    {
        return new AiModelConfig
        {
            Provider = AiProviderType.AzureOpenAI,

            Model = "gpt-4o-mini",

            SupportsJsonMode = true,

            SupportsVision = false,

            EstimatedCostPer1KTokens = 0.0015m,

            IsPreferred = true
        };
    }

    private static AiModelConfig CreateOpenRouterVisionModel()
    {
        return new AiModelConfig
        {
            Provider = AiProviderType.OpenRouter,

            Model = "google/gemini-2.5-flash",

            SupportsJsonMode = true,

            SupportsVision = true,

            EstimatedCostPer1KTokens = 0.0008m,

            IsPreferred = true
        };
    }
}