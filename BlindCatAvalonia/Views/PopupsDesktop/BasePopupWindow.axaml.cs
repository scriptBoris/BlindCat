using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.SDcontrols;
using BlindCatCore.Core;
using BlindCatCore.Models;
using System;
using System.Linq;

namespace BlindCatAvalonia.Views.PopupsDesktop;

public partial class BasePopupWindow : Window
{
    [Obsolete("DESIGNER")]
    public BasePopupWindow()
    {
        InitializeComponent();
        container.Children.Add(new TextBlock
        {
            Text = " огда вы нажимаете и удерживаете левую кнопку мыши на панели, событие \"захвата\" указател€ происходит.\n\n¬ этом случае указатель \"закрепл€етс€\" за элементом, который получил событие нажати€, и он перестает отправл€ть событи€ PointerEnter, PointerLeave, PointerMoved другим элементам, даже если мышь движетс€ по ним.",
        });
    }

    public BasePopupWindow(Control view, BaseVm vm)
    {
        InitializeComponent();

        vm.LoadingPushed += Vm_LoadingPushed;
        vm.LoadingPoped += Vm_LoadingPoped;

        Title = Scaffolt.GetTitle(view);
        container.Children.Add(view);
    }

    private void Vm_LoadingPoped(BaseVm invoker, LoadingToken token)
    {
        loading.PopToken(token);
    }

    private void Vm_LoadingPushed(BaseVm vm, BlindCatCore.Models.LoadingToken token)
    {
        loading.PushToken(token);
    }
}