namespace AuraCore.Engine.AIAnalyzer;

public sealed class MetricBuffer
{
    private readonly MetricSample[] _buffer;
    private readonly int _capacity;
    private readonly object _lock = new();
    private int _head;
    private int _count;

    public MetricBuffer(int capacity = 900)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _buffer = new MetricSample[capacity];
    }

    public int Count { get { lock (_lock) return _count; } }

    public void Push(MetricSample sample)
    {
        lock (_lock)
        {
            _buffer[_head] = sample;
            _head = (_head + 1) % _capacity;
            if (_count < _capacity) _count++;
        }
    }

    public IReadOnlyList<MetricSample> GetSnapshot()
    {
        lock (_lock)
        {
            if (_count == 0) return Array.Empty<MetricSample>();
            var result = new MetricSample[_count];
            int start = _count < _capacity ? 0 : _head;
            for (int i = 0; i < _count; i++)
                result[i] = _buffer[(start + i) % _capacity];
            return result;
        }
    }

    public IReadOnlyList<float> GetCpuSeries()
    {
        var snap = GetSnapshot();
        var result = new float[snap.Count];
        for (int i = 0; i < snap.Count; i++) result[i] = snap[i].CpuPercent;
        return result;
    }

    public IReadOnlyList<float> GetRamSeries()
    {
        var snap = GetSnapshot();
        var result = new float[snap.Count];
        for (int i = 0; i < snap.Count; i++) result[i] = snap[i].RamPercent;
        return result;
    }
}
