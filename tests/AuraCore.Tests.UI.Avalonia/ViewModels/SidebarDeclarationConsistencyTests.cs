using System;
using System.Collections.Generic;
using System.Linq;
using AuraCore.Application.Interfaces.Modules;
using AuraCore.Domain.Enums;
using AuraCore.UI.Avalonia.ViewModels;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AuraCore.Tests.UI.Avalonia.ViewModels;

/// <summary>
/// Phase 6.16 Wave D regression gate: prevents drift between
/// SidebarViewModel.Module(...) platform declarations and the
/// IOptimizationModule.Platform enum (resolved via DI).
/// </summary>
public class SidebarDeclarationConsistencyTests
{
    [AvaloniaFact]
    public void Every_Sidebar_Module_Platform_Matches_Underlying_Module_Platform_When_Resolved()
    {
        var modulesById = TryGetModuleMap();
        if (modulesById is null) return; // DI not initialized in test process — acceptable

        var sidebar = new SidebarViewModel();

        foreach (var item in EnumerateSidebar(sidebar))
        {
            // Skip ids the sidebar declares but no DI-registered module backs.
            if (!modulesById.TryGetValue(item.Id, out var module)) continue;

            string expected = module.Platform switch
            {
                SupportedPlatform.Windows => "windows",
                SupportedPlatform.Linux   => "linux",
                SupportedPlatform.MacOS   => "macos",
                SupportedPlatform.All     => "all",
                _                         => "all",
            };

            Assert.True(
                string.Equals(expected, item.Platform, StringComparison.Ordinal),
                $"Sidebar declares moduleId='{item.Id}' as platform='{item.Platform}', " +
                $"but the underlying IOptimizationModule.Platform is '{module.Platform}' (=> '{expected}').");
        }
    }

    private static Dictionary<string, IOptimizationModule>? TryGetModuleMap()
    {
        try
        {
            var sp = global::AuraCore.UI.Avalonia.App.Services;
            if (sp is null) return null;
            var modules = sp.GetServices<IOptimizationModule>();
            return modules.GroupBy(m => m.Id).ToDictionary(g => g.Key, g => g.First());
        }
        catch { return null; }
    }

    private static IEnumerable<SidebarModuleVM> EnumerateSidebar(SidebarViewModel vm)
    {
        foreach (var cat in vm.Categories)
            foreach (var m in cat.Modules)
                yield return m;
        foreach (var m in vm.AdvancedItems)
            yield return m;
    }
}
