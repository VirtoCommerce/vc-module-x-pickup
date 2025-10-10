using MediatR;
using Microsoft.AspNetCore.Authorization;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Queries;
using VirtoCommerce.XPickup.Core.Schemas;

namespace VirtoCommerce.XPickup.Data.Queries;

public class SearchCartPickupLocationsQueryBuilder(IMediator mediator, IAuthorizationService authorizationService)
    : SearchQueryBuilder<SearchCartPickupLocationsQuery, ProductPickupLocationSearchResult, ProductPickupLocation, ProductPickupLocationType>(mediator, authorizationService)
{
    protected override string Name => "cartPickupLocations";
}
