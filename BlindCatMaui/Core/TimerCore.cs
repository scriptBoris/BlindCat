using BlindCatCore.Core;

namespace BlindCatMaui.Core;

public class TimerCore : ITimerCore
{
    private readonly IDispatcherTimer _platformTimer;

    public event EventHandler Tick
    {
        add
        {
            _platformTimer.Tick += value;
        }
        remove
        {
            _platformTimer.Tick -= value;
        }
    }

    public TimerCore(IDispatcherTimer t)
    {
        _platformTimer = t;
    }

    public TimeSpan Interval
    {
        get => _platformTimer.Interval;
        set => _platformTimer.Interval = value;
    }

    public bool IsRepeating
    {
        get => _platformTimer.IsRepeating;
        set => _platformTimer.IsRepeating = value;
    }

    public bool IsRunning => _platformTimer.IsRunning;

    public void Start()
    {
        _platformTimer.Start();
    }

    public void Stop()
    {
        _platformTimer.Stop();
    }
}