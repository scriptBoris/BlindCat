namespace BlindCatCore.Core;

public interface ITimerCore
{
    /// <summary>
    /// Occurs when the timer interval has elapsed.
    /// </summary>
    event EventHandler Tick;

    /// <summary>
    /// Gets or sets the amount of time between timer ticks.
    /// </summary>
    TimeSpan Interval { get; set; }

    /// <summary>
    /// Gets or sets whether the timer should repeat.
    /// </summary>
    /// <remarks>When set the <see langword="false"/>, the timer will run only once.</remarks>
    bool IsRepeating { get; set; }

    /// <summary>
    /// Gets a value that indicates whether the timer is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the timer.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops the timer.
    /// </summary>
    void Stop();
}