using BlindCatCore.Services;

namespace BlindCatMaui.Services;

public class AppEnv : IAppEnv
{
    public string? AppLaunchedArgs { get; set; }
}
