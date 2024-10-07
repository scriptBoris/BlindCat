using System.ComponentModel;

namespace BlindCatCore.Core;

public abstract class BaseVm<T> : BaseVm
{
    private T? preparedResult;
    private TaskCompletionSource<T?> taskCompletionSource = new();

    public Task SetResultAndPop(T result)
    {
        preparedResult = result;
        return Close();
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public Task<T?> GetResult_Internal()
    {
        return taskCompletionSource.Task;
    }

    public override void OnDisconnectedFromNavigation()
    {
        base.OnDisconnectedFromNavigation();
        taskCompletionSource.TrySetResult(preparedResult);
    }

    public override void SetResult_Backsroom(object? result)
    {
        base.SetResult_Backsroom(result);

        if (result is T t) 
            taskCompletionSource.TrySetResult(t);
        else
            taskCompletionSource.TrySetResult(default);
    }
}

public static class BaseVmExt
{
    public static async Task<T?> GetResult<T>(this BaseVm<T>? vm)
    {
        if (vm == null)
            return default;

        var result = await vm.GetResult_Internal();
        return result;
    }
}