using ActiproSoftware.Properties.Shared;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.Views;
using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Tools;

public abstract class AvaloniaPlatform : IViewPlatforms
{
    protected readonly List<LoadingStrDesc> _loadings = new();

    public bool AppLoading => _loadings.Count > 0;
    public IEnumerable<LoadingStrDesc> CurrentLoadings => _loadings;

    public object BuildView(Type? viewType, BaseVm baseVm)
    {
        if (viewType == null)
            return new ContentControl
            {
                Background = Brushes.DarkRed,
                Content = new TextBlock
                {
                    Text = $"For VM {baseVm.GetType().Name} ViewType is NULL",
                    Foreground = Brushes.White,
                },
            };

        try
        {
            foreach (var ctor in viewType.GetConstructors())
            {
                var parameters = ctor.GetParameters();
                if (parameters.Length != 1)
                    break;

                var parg = parameters[0];
                if (parg.ParameterType != baseVm.GetType())
                    break;

                var lwview = (Control)RuntimeHelpers.GetUninitializedObject(viewType);
                ctor.Invoke(lwview, [baseVm]);
                lwview.DataContext = baseVm;
                return lwview;
            }

            var inst = Activator.CreateInstance(viewType)!;
            var view = (Control)inst;
            view.DataContext = baseVm;
            return view;
        }
        catch (Exception ex)
        {
            return new ContentControl
            {
                Background = Brushes.DarkRed,
                Content = new TextBlock
                {
                    Text = $"Fail create View for VM {baseVm.GetType().Name}\n" +
                        $"{ex.Message}",
                },
            };
        }
    }

    public void Destroy(object view)
    {
    }

    public void InvokeInMainThread(Action act)
    {
        Dispatcher.UIThread.Post(act);
    }

    public ITimerCore MakeTimer()
    {
        return new PlatformTimer();
    }

    public LoadingStrDesc UseGlobalLoading(object viewHost, string token, string? description, CancellationTokenSource? cancel)
    {
        var view = (Control)viewHost;
        var w = view.GetVisualRoot() as IWindowBusy;

        var loadDesc = new LoadingStrDesc
        {
            Token = token,
            ActionDispose = (self) =>
            {
                _loadings.Remove(self);
            },
            Cancellation = cancel,
            Description = description,
        };

        if (!_loadings.Contains(loadDesc))
        {
            _loadings.Add(loadDesc);
            w?.MakeLoading(loadDesc);
        }

        return loadDesc;
    }

    public abstract Task<string?> SelectDirectory(object? hostView);
    public abstract Task<IFileResult?> SelectMediaFile(object? hostView);
    public abstract Task ShowDialog(string title, string body, string OK, object? hostView);
    public abstract Task<bool> ShowDialog(string title, string body, string OK, string cancel, object? hostView);
    public abstract Task<string?> ShowDialogPromt(string title, string message, string OK, string cancel, string placeholder, string initValue, object? hostView);
    public abstract Task<string?> ShowDialogPromtPassword(string title, string message, string OK, string cancel, string placeholder, object? hostView);
    public abstract Task<string?> ShowDialogSheet(string title, string cancel, string[] items, object? hostView);
    public abstract Task<int?> ShowDialogSheetId(string title, string cancel, string[] items, object? hostView);
    public abstract AppResponse ShowFileOnExplorer(string filePath);

    private class PlatformTimer : ITimerCore
    {
        private readonly DispatcherTimer _timer;
        public event EventHandler? Tick;

        public PlatformTimer()
        {
            _timer = new();
            _timer.Tick += OnTick;
        }

        public bool IsRepeating { get; set; }
        public bool IsRunning => _timer.IsEnabled;
        public TimeSpan Interval
        {
            get => _timer.Interval;
            set => _timer.Interval = value;
        }

        private void OnTick(object? sender, EventArgs e)
        {
            Tick?.Invoke(this, e);
            if (!IsRepeating)
            {
                Stop();
            }
        }

        public void Start()
        {
            _timer.Start();
        }

        public void Stop()
        {
            _timer.Stop();
        }
    }
}