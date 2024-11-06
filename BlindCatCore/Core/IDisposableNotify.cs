using System.Windows.Input;

namespace BlindCatCore.Core;

public interface IDisposableNotify : IDisposable
{
    event EventHandler? Disposing;
    event EventHandler? Disposed;
}