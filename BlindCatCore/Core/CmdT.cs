using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Core;

public class Cmd<T> : Cmd
{
    private readonly Action<T>? _action;
    private readonly Func<T, Task>? _func;

    public Cmd(Action<T> act)
    {
        _action = act;
    }

    public Cmd(Func<T, Task> func)
    {
        _func = func;
    }

    protected override Task Invoke(object? parameter)
    {
        if (parameter is not T t)
            return Task.CompletedTask;

        if (_action != null)
        {
            _action(t);
            return Task.CompletedTask;
        }
        else if (_func != null)
        {
            return _func(t);
        }
        else
        {
            throw new InvalidOperationException();
        }
    }
}
