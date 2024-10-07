using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlindCatAvalonia.SDcontrols.Scaffold.Utils;

namespace BlindCatAvalonia.SDcontrols.Scaffold.Args;

public class AgentArgs
{
    public required ScaffoldView ScaffoldView { get; init; }
    public Agent Agent { get; set; } = null!;
    public bool HideBackButton { get; init; }
}