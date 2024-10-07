using Avalonia;
using Avalonia.Controls;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlindCatAvalonia.SDcontrols.Scaffold.Utils;
using BlindCatAvalonia.SDcontrols.Scaffold.Args;
using Avalonia.Animation;
using Avalonia.Styling;
using Avalonia.Media;

namespace BlindCatAvalonia.SDcontrols.Scaffold;

public class ScaffoldView : Panel
{
    private readonly List<Agent> _agents = new();
    private readonly List<Control> _nav = new();
    private readonly UserControl _fadeLayout;

    public ScaffoldView()
    {
        NavigationStack = _nav;

        _fadeLayout = new UserControl()
        {
            Background = new SolidColorBrush(new Color(150, 0, 0, 0)),
        };
    }

    public IReadOnlyList<Control> NavigationStack { get; }

    protected override Size ArrangeOverride(Size finalSize)
    {
        return base.ArrangeOverride(finalSize);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        return base.MeasureOverride(availableSize);
    }

    public async Task PushAsync(Control view, bool useAnimation = true)
    {
        bool anim = (_agents.Count == 0) ? false : useAnimation;
        var currentAgent = _agents.LastOrDefault();
        var newAgent = new Agent(view, new AgentArgs
        {
            ScaffoldView = this,
            HideBackButton = _nav.Count == 0,
        });
        Children.Add(newAgent);

        if (anim)
        {
            currentAgent!.IsHitTestVisible = false;
            newAgent.IsHitTestVisible = false;

            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(250),
                Children =
                {
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0.0)
                        },
                        Cue = new Cue(0d)
                    },
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1.0)
                        },
                        Cue = new Cue(1d)
                    }
                },
            };
            await animation.RunAsync(newAgent);
            currentAgent!.IsHitTestVisible = true;
            newAgent.IsHitTestVisible = true;
        }

        _agents.Add(newAgent);
        _nav.Add(newAgent.View);

        if (currentAgent != null)
        {
            currentAgent.IsVisible = false;
        }
    }

    public async Task PopAsync(bool useAnimation = true)
    {
        int currentIndex = _agents.Count - 1;
        var precurrentAgent = (currentIndex >= 1) ? _agents[currentIndex - 1] : null;
        var currentAgent = _agents.Last();

        if (precurrentAgent == null)
            return;

        precurrentAgent.IsVisible = true;

        if (useAnimation)
        {
            currentAgent.IsHitTestVisible = false;
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(170),
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 1.0)
                        },
                        Cue = new Cue(0d)
                    },
                    new KeyFrame
                    {
                        Setters =
                        {
                            new Setter(Visual.OpacityProperty, 0.0)
                        },
                        Cue = new Cue(1d)
                    }
                },
            };
            await animation.RunAsync(currentAgent);
        }

        // dispose
        if (currentAgent.View is IDisposable dis)
        {
            dis.Dispose();
        }

        Children.Remove(currentAgent);
        _agents.Remove(currentAgent);
        _nav.Remove(currentAgent.View);
    }

    public Task PopToRootAsync()
    {
        // todo оставлять 1 элемент, у остальный вызывать on disconnect from navigation
        _nav.Clear();
        _agents.Clear();
        Children.Clear();
        return Task.CompletedTask;
    }

    public void OnBackButton(Agent _agent)
    {
        int id = _agents.IndexOf(_agent);
        if (id == -1)
        {
            throw new InvalidOperationException();
        }
        else if (id != _nav.Count - 1)
        {
            throw new InvalidOperationException();
        }

        PopAsync();
    }

    private int busyCounts;
    public IDisposable Busy()
    {
        var n = new BusyPart
        {
            DisposeAction = () =>
            {
                busyCounts--;
                if (busyCounts == 0)
                {
                    Children.Remove(_fadeLayout);
                }
            },
        };

        if (busyCounts == 0)
            Children.Add(_fadeLayout);

        busyCounts++;
        return n;
    }

    private class BusyPart : IDisposable
    {
        public required Action DisposeAction { get; set; }

        void IDisposable.Dispose()
        {
            DisposeAction();
        }
    }
}