using BlindCatCore.Core;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;

namespace BlindCatCore.Models;

public class LoadingStrDesc : IDisposable, INotifyPropertyChanged
{
    private bool isDisposed;
    private string? _body;

    public event EventHandler<string?>? BodyChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Disposed;

    public required Action<LoadingStrDesc> ActionDispose { private get; init; }
    public required string Token { get; init; }

    /// <summary>
    /// aka Title
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// For cancel button (if this value is not null)
    /// </summary>
    public CancellationTokenSource? Cancellation { get; init; }

    public bool IsVisible { get; set; }

    /// <summary>
    /// text under Description
    /// </summary>
    public string? Body
    {
        get => _body;
        set
        {
            _body = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Body)));
            BodyChanged?.Invoke(this, value);
        }
    }

    public override int GetHashCode()
    {
        return Token.GetHashCode();
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj is LoadingStrDesc other)
        {
            return this.Token == other.Token;
        }
        return base.Equals(obj);
    }

    public void Dispose()
    {
        if (isDisposed)
            return;

        isDisposed = true;
        Disposed?.Invoke(this, EventArgs.Empty);
        ActionDispose(this);
    }
}