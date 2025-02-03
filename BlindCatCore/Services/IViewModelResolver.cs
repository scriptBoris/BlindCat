using System.Runtime.CompilerServices;
using BlindCatCore.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BlindCatCore.Services;

public interface IViewModelResolver
{
    public static readonly Dictionary<Type, Item> _types = [];

    BaseVm Resolve(object navigationKey);
    T Resolve<T>(IKey<T> navigationKey) where T : BaseVm;

    public static void Reg<TVm, TView>(IServiceCollection serviceDescriptors)
    {
        var viewModelType = typeof(TVm);
        var viewType = typeof(TView);
        var baseViewModelType = viewModelType.BaseType;
        if (baseViewModelType == null)
            throw new ArgumentException($"{viewModelType.Name} должен быть унаследован от BaseViewModel<>.");

        var ctor = viewModelType.GetConstructors()[0];
        var firstArg = ctor.GetParameters().FirstOrDefault();
        if (firstArg == null)
            throw new ArgumentException($"У {viewModelType.Name} должен быть конструктор " +
                $"с первыми аргументом \"Key\".");

        if (firstArg.ParameterType.Name != "Key")
            throw new ArgumentException($"У {viewModelType.Name} должен быть конструктор " +
                $"с первыми аргументом \"Key\".");

        //if (!baseViewModelType.IsGenericType)
        //{
        //    throw new ArgumentException($"{viewModelType.Name} должен быть унаследован от BaseViewModel<>.");
        //}

        var navigationViewModelType = firstArg.ParameterType;//baseViewModelType.GetGenericArguments()[0];
        _types.Add(navigationViewModelType, new Item
        {
            ViewType = viewType,
            ViewModelType = viewModelType,
        });

        var tvm = typeof(TVm);
        serviceDescriptors.AddTransient(tvm);
    }

    public class Item
    {
        public required Type ViewType { get; init; }
        public required Type ViewModelType { get; init; }
    }
}


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

    public T Resolve<T>(IKey<T> navigationKey) where T : BaseVm
    {
        return (T)Resolve((object)navigationKey);
    }
}

public static class ViewModelResolverReg
{
    public static IServiceCollection RegisterNav<TVm, TView>(this IServiceCollection serviceDescriptors)
        where TVm : BaseVm
    {
        IViewModelResolver.Reg<TVm, TView>(serviceDescriptors);
        return serviceDescriptors;
    }
}