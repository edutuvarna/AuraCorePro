using global::Avalonia.Controls;
using global::Avalonia.Media;
using global::Avalonia.Threading;

namespace AuraCore.UI.Avalonia.Views.Controls;

public partial class OrbitalLogo : UserControl
{
    private DispatcherTimer? _rotTimer;
    private RotateTransform? _rotation;
    private double _angle;

    private EventHandler? _tickHandler;

    public OrbitalLogo()
    {
        InitializeComponent();
        Loaded += (s, e) => StartAnimation();
        Unloaded += (s, e) => StopAnimation();
    }

    private void StartAnimation()
    {
        _rotation = OrbitalCanvas.RenderTransform as RotateTransform;
        if (_rotation is null) return;

        _rotTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _tickHandler = (s, e) =>
        {
            _angle = (_angle + 0.3) % 360;
            _rotation.Angle = _angle;
        };
        _rotTimer.Tick += _tickHandler;
        _rotTimer.Start();
    }

    private void StopAnimation()
    {
        if (_rotTimer is not null)
        {
            _rotTimer.Stop();
            if (_tickHandler is not null)
            {
                _rotTimer.Tick -= _tickHandler;
                _tickHandler = null;
            }
            _rotTimer = null;
        }
    }
}
