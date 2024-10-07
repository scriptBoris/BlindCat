using BlindCatCore.Core;
using BlindCatCore.ExternalApi;
using Microsoft.Extensions.DependencyInjection;

namespace BlindCatCore.Services
{
    public interface IPluginHost
    {
        IBlindCatApi MakePublicApi(IPlugin plugin, BaseVm viewModel, CancellationToken token);
    }

    internal class PluginHost : IPluginHost
    {
        private readonly IViewPlatforms viewPlatforms;

        public PluginHost(IViewPlatforms viewPlatforms)
        {
            this.viewPlatforms = viewPlatforms;
        }

        public IBlindCatApi MakePublicApi(IPlugin plugin, BaseVm viewModel, CancellationToken token)
        {
            var res = new BlindCatApi(plugin, viewModel, viewPlatforms, token);
            return res;
        }
    }

    public static class UsingServices
    {
        public static IServiceCollection AddInternalServices(this IServiceCollection services)
        {
            services.AddScoped<IPluginHost, PluginHost>();
            return services;
        }
    }
}
