using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using BlindCatAvalonia.SDcontrols;
using BlindCatCore.Core;
using BlindCatCore.Models;
using System;
using System.Linq;
using Scaffolt = BlindCatAvalonia.SDcontrols.Scaffold.GlobalXmlns.Scaffold;

namespace BlindCatAvalonia.Views.PopupsDesktop;

public partial class BasePopupWindow : Window
{
    [Obsolete("DESIGNER")]
    public BasePopupWindow()
    {
        InitializeComponent();
        container.Children.Add(new TextBlock
        {
            Text = "����� �� ��������� � ����������� ����� ������ ���� �� ������, ������� \"�������\" ��������� ����������.\n\n� ���� ������ ��������� \"������������\" �� ���������, ������� ������� ������� �������, � �� ��������� ���������� ������� PointerEnter, PointerLeave, PointerMoved ������ ���������, ���� ���� ���� �������� �� ���.",
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