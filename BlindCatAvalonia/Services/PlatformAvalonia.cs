using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BlindCatAvalonia.Core;
using BlindCatAvalonia.Views.PopupsDesktop;
using BlindCatCore.Core;
using BlindCatCore.Models;
using BlindCatCore.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Services;

public abstract class PlatformAvalonia: IViewPlatforms
{
    protected readonly List<LoadingToken> _loadings = new();

    public bool AppLoading => _loadings.Count > 0;
    public IEnumerable<LoadingToken> CurrentLoadings => _loadings;
    public abstract IClipboard Clipboard { get; }

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

    public void UseGlobalLoading(object viewHost, IDisposableNotify token)
    {
        var view = (Control)viewHost;
        var w = view.GetVisualRoot() as IWindowBusy;

        var tokenSource = (LoadingToken)token;
        tokenSource.Disposed += Disposed;

        void Disposed(object? invoker, EventArgs args)
        {
            tokenSource.Disposed -= Disposed;
            _loadings.Remove(tokenSource);
        }

        if (!_loadings.Contains(tokenSource))
        {
            _loadings.Add(tokenSource);
            w?.MakeLoading(tokenSource);
        }
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
    public abstract Task<string?> SaveTo(string? defaultFileName, string? defaultDirectory);

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