using BlindCatCore.Core;
using BlindCatCore.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using static BlindCatCore.Services.IViewModelResolver;

namespace BlindCatMaui.Services;

public class ViewModelResolver : IViewModelResolver
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ViewModelResolver> _logger;

    public ViewModelResolver(IServiceProvider serviceProvider, ILogger<ViewModelResolver> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public BaseVm Resolve(object navigationKey)
    {
        var typeKey = navigationKey.GetType();
        var pair = IViewModelResolver._types[typeKey];

        var ctor = pair.ViewModelType.GetConstructors().First();
        var ctorParams = ctor.GetParameters();
        var parameters = new object[ctorParams.Length];

        // fetch services
        for (int i = 0; i < ctorParams.Length; i++)
        {
            var item = ctorParams[i];

            // Skip first arg, because first is NavKey arg
            if (i == 0)
            {
                parameters[0] = navigationKey;
                continue;
            }

            object? dependency = _serviceProvider.GetService(item.ParameterType);
            if (dependency == null)
            {
                _logger.LogError($"Not found dependency service: {item.ParameterType.Name}");
                throw new InvalidOperationException($"Not found dependency service: {item.ParameterType.Name}");
            }

            parameters[i] = dependency;
        }

        var vm = (BaseVm)RuntimeHelpers.GetUninitializedObject(pair.ViewModelType);
        vm.ViewType = pair.ViewType;
        vm.ViewModelResolver = this;
        vm.ViewPlatforms = _serviceProvider.GetRequiredService<IViewPlatforms>();
        vm.NavigationService = _serviceProvider.GetRequiredService<INavigationService>();

        // call ctor
        ctor.Invoke(vm, parameters);
        return vm;
    }
}

public static class ViewModelResolverReg
{
    public static IServiceCollection RegisterNav<TVm, TView>(this IServiceCollection serviceDescriptors)
    {
        IViewModelResolver.Reg<TVm, TView>(serviceDescriptors);
        return serviceDescriptors;
    }
}
