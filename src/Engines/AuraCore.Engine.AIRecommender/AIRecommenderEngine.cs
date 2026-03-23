using Microsoft.Extensions.DependencyInjection;
using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.Engine.AIRecommender;

public sealed class AIRecommenderEngine : IAIRecommenderEngine
{
    public Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Recommendation>>(Array.Empty<Recommendation>());
    public Task DismissAsync(string recommendationId, CancellationToken ct = default) => Task.CompletedTask;
}

public static class AIRecommenderRegistration
{
    public static IServiceCollection AddAIRecommender(this IServiceCollection services)
        => services.AddSingleton<IAIRecommenderEngine, AIRecommenderEngine>();
}
