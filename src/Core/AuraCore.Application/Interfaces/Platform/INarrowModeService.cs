using System.ComponentModel;

namespace AuraCore.Application.Interfaces.Platform;

/// <summary>
/// Cross-cutting responsive-layout state. Views + VMs bind to
/// <see cref="IsNarrow"/> / <see cref="IsVeryNarrow"/> to react to
/// window resizing. Thresholds are fixed at 1000 px and 900 px per
/// Phase 5.3 spec §3.1 (matches Phase 2/3 historical behavior).
/// </summary>
public interface INarrowModeService : INotifyPropertyChanged
{
    /// <summary>True when current width &lt; 1000 px.</summary>
    bool IsNarrow { get; }

    /// <summary>True when current width &lt; 900 px. Implies <see cref="IsNarrow"/>.</summary>
    bool IsVeryNarrow { get; }

    /// <summary>Current tracked width in DIPs. Useful for diagnostic binding.</summary>
    double CurrentWidth { get; }

#if DEBUG
    /// <summary>
    /// DEBUG-only override for dev/test ergonomics. Values: <c>null</c>
    /// = track actual width; <c>true</c> = force IsNarrow+IsVeryNarrow;
    /// <c>false</c> = force wide. Release builds omit this property
    /// entirely so it cannot be toggled in prod.
    /// </summary>
    bool? ForceNarrowOverride { get; set; }
#endif
}
