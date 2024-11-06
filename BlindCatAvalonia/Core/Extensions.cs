using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using BlindCatAvalonia.SDcontrols;
using BlindCatAvalonia.SDcontrols.Scaffold;
using BlindCatCore.Core;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Core;

public static class Extensions
{
    public static BaseVm ResolveVm(this object view)
    {
        var v = (Control)view;
        var vm = (BaseVm)v.DataContext!;
        return vm;
    }

    public static T DI<T>(this object view)
    {
        return App.ServiceProvider.GetRequiredService<T>();
    }

    public static async Task AwaitLoading(this Control self)
    {
        if (self.IsLoaded)
            return;

        var tsc = new TaskCompletionSource();
        void Load(object? invoker, RoutedEventArgs e)
        {
            tsc.TrySetResult();
        }
        self.Loaded += Load;
        await tsc.Task;
        self.Loaded -= Load;
    }

    public static void StopAndCout(this Stopwatch stopwatch, string label)
    {
        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds == 0)
            Debug.WriteLine($"{label} ({stopwatch.ElapsedMilliseconds}ms) ({stopwatch.ElapsedTicks}ticks)");
        else
            Debug.WriteLine($"{label} ({stopwatch.ElapsedMilliseconds}ms)");
    }
}