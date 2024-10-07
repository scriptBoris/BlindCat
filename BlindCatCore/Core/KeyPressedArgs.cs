using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Core;

public class KeyPressedArgs
{
    public required string Key { get; set; }
    public bool Handled { get; set; }
}