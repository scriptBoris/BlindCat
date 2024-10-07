using BlindCatCore.Services;

namespace BlindCatCore.Core;

public class SuggestionController : IDisposable
{
    private readonly ITimerCore _timer;
    private TaskCompletionSource<bool> tsc = new();
    private string _old = "";

    public SuggestionController(IViewPlatforms viewPlatforms)
    {
        _timer = viewPlatforms.MakeTimer();
        _timer.Tick += TimerTick;
        _timer.IsRepeating = false;
        _timer.Interval = TimeSpan.FromMilliseconds(1000);
    }

    private void TimerTick(object? sender, EventArgs e)
    {
        tsc.SetResult(true);
    }

    public async Task<string?> Output(string input)
    {
        if (_timer.IsRunning)
        {
            _timer.Stop();
            tsc.SetResult(false);
        }

        tsc = new();
        _timer.Start();
        bool res = await tsc.Task;
        if (res)
        {
            string resultNew = input.Trim();

            if (string.IsNullOrWhiteSpace(resultNew))
                resultNew = "";

            if (string.Equals(_old, resultNew, StringComparison.OrdinalIgnoreCase))
                return null;

            _old = resultNew;
            return resultNew;
        }
        else
        {
            return null;
        }
    }

    public void Dispose()
    {
        _timer.Tick -= TimerTick;
        tsc.TrySetResult(false);
    }
}