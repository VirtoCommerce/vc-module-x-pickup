using GraphQL.MicrosoftDI;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XPickup.Core;
using VirtoCommerce.XPickup.Data;
using VirtoCommerce.XPickup.Data.Extensions;

namespace VirtoCommerce.XPickup.Web;

public class Module : IModule, IHasConfiguration
{
    public ManifestModuleInfo ModuleInfo { get; set; }
    public IConfiguration Configuration { get; set; }

    public void Initialize(IServiceCollection serviceCollection)
    {
        var graphQlBuilder = new GraphQLBuilder(serviceCollection, builder =>
        {
            builder.AddSchema(serviceCollection, typeof(CoreAssemblyMarker), typeof(DataAssemblyMarker));
        });
        serviceCollection.AddXPickup(graphQlBuilder);
    }

    public void PostInitialize(IApplicationBuilder appBuilder)
    {
        // Register partial GraphQL schema
        appBuilder.UseScopedSchema<DataAssemblyMarker>("pickup");

        var settingsRegistrar = appBuilder.ApplicationServices.GetRequiredService<ISettingsRegistrar>();
        settingsRegistrar.RegisterSettings(ModuleConstants.Settings.PickupLocationSettings, ModuleInfo.Id);
        settingsRegistrar.RegisterSettingsForType(ModuleConstants.Settings.PickupLocationSettings, nameof(Store));
    }

    public void Uninstall()
    {
        // Nothing to do here
    }
}
