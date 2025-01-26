using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BlindCatAvalonia.SDcontrols;
using BlindCatCore.Models;

namespace BlindCatAvalonia.Core;

public interface IWindowBusy
{
    IDisposable MakeFade();
    void MakeLoading(LoadingToken loadingDescription);
}