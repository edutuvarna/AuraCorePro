namespace AuraCore.Application.Interfaces.Engines;

public interface IAIRecommenderEngine
{
    Task<IReadOnlyList<Recommendation>> GetRecommendationsAsync(CancellationToken ct = default);
    Task DismissAsync(string recommendationId, CancellationToken ct = default);
}

public sealed record Recommendation(
    string Id,
    string ModuleId,
    string Title,
    string Description,
    double Confidence);
