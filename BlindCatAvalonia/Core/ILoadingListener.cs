using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlindCatAvalonia.Core;

public interface ILoadingListener
{
    void LoadingStart(bool flag);
}