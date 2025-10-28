using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using VirtoCommerce.Xapi.Core.BaseQueries;
using VirtoCommerce.Xapi.Core.Extensions;
using VirtoCommerce.XPickup.Core.Models;

namespace VirtoCommerce.XPickup.Core.Queries;

public class SearchCartPickupLocationsQuery : SearchQuery<ProductPickupLocationSearchResult>
{
    public string CartId { get; set; }

    public string StoreId { get; set; }

    public string CultureName { get; set; }

    public string Facet { get; set; }

    public string Filter { get; set; }

    public override IEnumerable<QueryArgument> GetArguments()
    {
        foreach (var argument in base.GetArguments())
        {
            yield return argument;
        }

        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(CartId), description: "Cart Id");
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(StoreId), description: "Store Id");
        yield return Argument<NonNullGraphType<StringGraphType>>(nameof(CultureName), description: "Culture name (\"en-US\")");

        yield return Argument<StringGraphType>(nameof(Facet), "Facets calculate statistical counts to aid in faceted navigation.");
        yield return Argument<StringGraphType>(nameof(Filter), "Applies a filter to the query results");
    }

    public override void Map(IResolveFieldContext context)
    {
        base.Map(context);

        CartId = context.GetArgument<string>(nameof(CartId));
        StoreId = context.GetArgument<string>(nameof(StoreId));
        CultureName = context.GetArgument<string>(nameof(CultureName));

        Facet = context.GetArgument<string>(nameof(Facet));
        Filter = context.GetArgument<string>(nameof(Filter));
    }
}
