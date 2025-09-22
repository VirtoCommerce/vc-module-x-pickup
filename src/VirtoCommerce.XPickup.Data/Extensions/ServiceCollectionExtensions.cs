using GraphQL.DI;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Xapi.Core.Infrastructure;

namespace VirtoCommerce.XPickup.Data.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddXPickup(this IServiceCollection services, IGraphQLBuilder graphQLBuilder)
    {
        services.AddSingleton<ScopedSchemaFactory<DataAssemblyMarker>>();

        return services;
    }
}
