using BlindCatCore.Core;
using Microsoft.Extensions.DependencyInjection;

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
