using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlindCatCore.Core;

namespace BlindCatAvalonia.Core;

public interface IErrorListener
{
    void SetError(AppResponse? message);
}