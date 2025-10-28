using GraphQL.Types;
using GraphQL.Types.Relay;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Helpers;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Queries;
using VirtoCommerce.XPickup.Core.Schemas;
using static VirtoCommerce.Xapi.Core.ModuleConstants;

namespace VirtoCommerce.XPickup.Data.Queries;

public class SearchCartPickupLocationsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
    : SearchQueryBuilder<SearchCartPickupLocationsQuery, ProductPickupLocationSearchResult, ProductPickupLocation, ProductPickupLocationType>(mediator, authorizationService)
{
    protected override string Name => "cartPickupLocations";

    protected override FieldType GetFieldType()
    {
        var builder = GraphTypeExtensionHelper
            .CreateConnection<ProductPickupLocationType, EdgeType<ProductPickupLocationType>, CartPickupLocationPagedConnectionType<ProductPickupLocationType>, ProductPickupLocation>(Name)
            .PageSize(Connections.DefaultPageSize);

        ConfigureArguments(builder.FieldType);

        builder.ResolveAsync(async context =>
        {
            var (query, response) = await Resolve(context);
            return new CartPickupLocationPagedConnection<ProductPickupLocation>(response.Results, query.Skip, query.Take, response.TotalCount)
            {
                Facets = response.Facets,
            };
        });

        return builder.FieldType;
    }
}
