using GraphQL.DI;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XPickup.Core.Services;
using VirtoCommerce.XPickup.Data.Services;

namespace VirtoCommerce.XPickup.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXPickup(this IServiceCollection services, IGraphQLBuilder graphQLBuilder)
    {
        services.AddSingleton<ScopedSchemaFactory<DataAssemblyMarker>>();

        services.AddTransient<IProductPickupLocationService, ProductPickupLocationService>();

        return services;
    }
}
