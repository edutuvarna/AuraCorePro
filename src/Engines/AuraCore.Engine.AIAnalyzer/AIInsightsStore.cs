using AuraCore.Application.Interfaces.Engines;

namespace AuraCore.Engine.AIAnalyzer;

public sealed class AIInsightsStore
{
    private AIAnalysisResult? _latest;
    public AIAnalysisResult? Latest => _latest;
    public event Action<AIAnalysisResult>? Updated;

    public void Update(AIAnalysisResult result)
    {
        _latest = result;
        Updated?.Invoke(result);
    }
}
