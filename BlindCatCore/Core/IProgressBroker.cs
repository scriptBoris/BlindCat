namespace BlindCatCore.Core;

public interface IProgressBroker<T> where T : notnull
{
    event EventHandler<ProgressBrokerProgress>? OnChanged;
    void OnItemCompleted(T itemCompleted);
    void OnItemFailed(AppResponse res, T itemFailed);
}

public class ProgressBrokerProgress
{
    public bool IsSuccess { get; set; }
    public AppResponseError? AppResponseError { get; set; }
}

public class ProgressBroker<T> : IProgressBroker<T> where T : notnull
{
    public event EventHandler<ProgressBrokerProgress>? OnChanged;

    public ProgressBroker()
    {
    }

    public ProgressBroker(Action<ProgressBrokerProgress, T> act)
    {
        ActionSucscripber = act;
    }

    private Action<ProgressBrokerProgress, T>? ActionSucscripber { get; }

    public void OnItemCompleted(T itemCompleted)
    {
        var prog = new ProgressBrokerProgress
        {
            IsSuccess = true,
        };
        ActionSucscripber?.Invoke(prog, itemCompleted);
        OnChanged?.Invoke(itemCompleted, prog);
    }

    public void OnItemFailed(AppResponse res, T itemFailed)
    {
        var prog = new ProgressBrokerProgress
        {
            AppResponseError = res.AsError,
            IsSuccess = false,
        };
        ActionSucscripber?.Invoke(prog, itemFailed);
        OnChanged?.Invoke(itemFailed, prog);
    }
}