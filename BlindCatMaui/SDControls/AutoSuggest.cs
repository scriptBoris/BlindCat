using zoft.MauiExtensions.Controls;
using System.Windows.Input;
using System.Diagnostics;
#if WINDOWS
using AutoCompleteEntrys = BlindCatMaui.SDControls.Elements.AutoSuggestWinUI;
#else
using AutoCompleteEntrys = zoft.MauiExtensions.Controls.AutoCompleteEntry;
#endif
namespace BlindCatMaui.SDControls;

public class AutoSuggest : AutoCompleteEntrys
{
}