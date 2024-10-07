using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatCore.Enums;

public enum EncryptionMethods
{
    Unknown = -1,
    None = 0,
    dotnet,
    CENC,
}