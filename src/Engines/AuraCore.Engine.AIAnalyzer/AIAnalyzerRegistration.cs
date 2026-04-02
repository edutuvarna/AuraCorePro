using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.Engine.AIAnalyzer;

public static class AIAnalyzerRegistration
{
    public static IServiceCollection AddAIAnalyzer(this IServiceCollection services)
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var aiDir = Path.Combine(appData, "AuraCorePro", "ai");
        Directory.CreateDirectory(aiDir);
        var dbPath = Path.Combine(aiDir, "metrics.db");
        var profilePath = Path.Combine(aiDir, "profile.db");
        services.AddSingleton<IAIAnalyzerEngine>(sp => new AIAnalyzerEngine(dbPath, profilePath));
        return services;
    }
}
