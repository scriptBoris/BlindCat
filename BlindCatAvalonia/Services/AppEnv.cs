﻿using BlindCatCore.Services;

namespace BlindCatAvalonia.Services;

public class AppEnv : IAppEnv
{
    public string? AppLaunchedArgs { get; set; }
}