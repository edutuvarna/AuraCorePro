using Xunit;
using AuraCore.Application;
using AuraCore.Application.Interfaces.Simulation;
using AuraCore.Domain.Enums;
using AuraCore.Simulation;
namespace AuraCore.Tests.Simulation;

public class SimulationTests
{
    [Fact]
    public async Task Simulate_Empty()
    {
        var e = new SimulationEngine(Array.Empty<AuraCore.Application.Interfaces.Modules.IOptimizationModule>());
        var r = await e.SimulateAsync(new List<string>(), new ScanOptions());
        Assert.Equal(RiskLevel.None, r.HighestRisk);
    }
}
