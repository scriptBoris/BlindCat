using BlindCatCore.Core;
using System.Windows.Input;

namespace BlindCatCore.Models;

public class MPButtonContext : BaseNotify
{
    public string? KeyCombo { get; set; }
    public required string Name { get; set; }
    public required ICommand Command { get; set; }
}