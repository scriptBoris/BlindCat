using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Styling;

namespace BlindCatAvalonia.Core;

public class Template<T> : Avalonia.Controls.ITemplate<T> where T : Control?
{
    private readonly Func<T> _make;

    public Template(Func<T> make)
    {
        _make = make;
    }

    public T Build()
    {
        return _make();
    }

    object? ITemplate.Build()
    {
        return Build();
    }
}
