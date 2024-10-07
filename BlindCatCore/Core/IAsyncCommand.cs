using System.Windows.Input;

namespace BlindCatCore.Core;

public interface IAsyncCommand : ICommand
{
    bool IsRunning { get; }
    Task ExecuteAsync(object? param);
}