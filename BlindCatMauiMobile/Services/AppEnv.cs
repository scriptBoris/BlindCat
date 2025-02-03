using BlindCatCore.Services;

namespace BlindCatMauiMobile.Services;

public class AppEnv : IAppEnv
{
    public string? AppLaunchedArgs { get; set; }
}