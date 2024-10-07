using BlindCatAvalonia.SDcontrols;
using BlindCatCore.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Core;

public interface IWindowBusy
{
    IDisposable MakeFade();
    void MakeLoading(LoadingStrDesc loadingDescription);
}