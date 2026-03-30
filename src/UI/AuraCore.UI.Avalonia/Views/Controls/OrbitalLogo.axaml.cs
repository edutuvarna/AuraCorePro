using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Threading;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class OrbitalLogo : UserControl
{
    private DispatcherTimer? _rotTimer;
    private RotateTransform? _rotation;
    private double _angle;

    public OrbitalLogo()
    {
        InitializeComponent();
        Loaded += (s, e) => StartAnimation();
        Unloaded += (s, e) => _rotTimer?.Stop();
    }

    private void StartAnimation()
    {
        _rotation = OrbitalCanvas.RenderTransform as RotateTransform;
        if (_rotation is null) return;

        _rotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _rotTimer.Tick += (s, e) =>
        {
            _angle = (_angle + 0.3) % 360;
            _rotation.Angle = _angle;
        };
        _rotTimer.Start();
    }
}
