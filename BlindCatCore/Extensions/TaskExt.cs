using System.Threading.Channels;

namespace BlindCatCore.Extensions;

public static class TaskExt
{
    public static async void Forget(this Task task)
    {
        await task;
    }

    public static async Task Delay(int millisecondsDelay, CancellationToken cancel)
    {
        try
        {
            await Task.Delay(millisecondsDelay, cancel);
        }
        catch when(cancel.IsCancellationRequested)
        {
        }
        catch (Exception)
        {
        }
    }

    public static async Task<T?> AwaitWithCancelation<T>(this TaskCompletionSource<T> task, CancellationToken cancellationToken)
    {
        try
        {
            using (cancellationToken.Register(() =>
            {
                task.TrySetCanceled();
            }))
            {
                return await task.Task;
            }
        }
        catch (Exception)
        {
            return default;
        }
    }

    public static async Task Run(Action func, CancellationToken cancel)
    {
        try
        {
            await Task.Run(func, cancel);
        }
        catch when (cancel.IsCancellationRequested)
        {
            return;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public static async Task<T> Run<T>(Func<T> func, CancellationToken cancel)
    {
        T result = default;
        try
        {
            result = await Task.Run(func, cancel);
            return result;
        }
        catch when (cancel.IsCancellationRequested)
        {
            if (result is IDisposable dis)
                dis.Dispose();
            return default;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
