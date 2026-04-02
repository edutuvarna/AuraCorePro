namespace AuraCore.Engine.AIAnalyzer.Models;

public sealed record AnomalyResult(bool IsAnomaly, double Score, double ExpectedValue);
public sealed record ForecastResult(int DaysUntilFull, double Confidence, string Trend);
public sealed record LeakResult(string ProcessName, double GrowthRateMbPerMin, double Score);
