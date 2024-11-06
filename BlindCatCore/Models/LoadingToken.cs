using BlindCatCore.Core;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace BlindCatCore.Models;

public class LoadingToken : IDisposableNotify, INotifyPropertyChanged
{
    private bool isDisposed;

    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Disposed;
    public event EventHandler? Disposing;

    public required string Token { get; init; }

    /// <summary>
    /// aka Title
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// text under Title
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// For cancel button (if this value is not null)
    /// </summary>
    public CancellationTokenSource? Cancellation { get; init; }

    public override int GetHashCode()
    {
        return Token.GetHashCode();
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is LoadingToken other)
        {
            return this.Token == other.Token;
        }
        return base.Equals(obj);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        Disposing?.Invoke(this, EventArgs.Empty);
        isDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        GC.SuppressFinalize(this);
    }
}