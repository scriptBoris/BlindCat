using System;

namespace BlindCatAvalonia.Core;

public interface IDeferredDisposing : IDisposable
{
    bool IsReadyDispose { get; }
}