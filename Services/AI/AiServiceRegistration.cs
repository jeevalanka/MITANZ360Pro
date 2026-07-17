using Microsoft.Extensions.DependencyInjection;

namespace MITANZ360Pro.Web.Services.AI;

public static class AiServiceRegistration
{
    public static IServiceCollection AddMitanzAiEngine(
        this IServiceCollection services)
    {
        services.AddHttpClient();

        services.AddSingleton<AiRouterService>();

        services.AddScoped<AiGatewayService>();
        services.AddScoped<AiWorkflowEngine>();

        services.AddScoped<AiJsonParserService>();
        services.AddScoped<AiHtmlTemplateService>();
        services.AddScoped<AiStorageService>();

        services.AddScoped<IAiProvider, OpenAIService>();
        services.AddScoped<IAiProvider, OpenRouterService>();
        services.AddScoped<IAiProvider, AzureOpenAIService>();

        return services;
    }
}
