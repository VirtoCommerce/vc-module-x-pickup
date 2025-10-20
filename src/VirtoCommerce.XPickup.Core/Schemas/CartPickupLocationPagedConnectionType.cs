using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using GraphQL.Types.Relay;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XPickup.Core.Models;
using CoreFacets = VirtoCommerce.Xapi.Core.Schemas.Facets;

namespace VirtoCommerce.XPickup.Core.Schemas;

public class CartPickupLocationPagedConnectionType<TNodeType> : ConnectionType<TNodeType>
    where TNodeType : IGraphType
{
    public CartPickupLocationPagedConnectionType()
    {
        Name = "CartPickupLocationConnection";

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<CoreFacets.TermFacetResultType>>>>("term_facets").Description("Term facets")
            .Resolve(context => ((CartPickupLocationPagedConnection<ProductPickupLocation>)context.Source).Facets.OfType<TermFacetResult>());

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<CoreFacets.RangeFacetResultType>>>>("range_facets").Description("Range facets")
            .Resolve(context => ((CartPickupLocationPagedConnection<ProductPickupLocation>)context.Source).Facets.OfType<RangeFacetResult>());

        Field<NonNullGraphType<ListGraphType<NonNullGraphType<CoreFacets.FilterFacetResultType>>>>("filter_facets").Description("Filter facets")
            .Resolve(context => ((CartPickupLocationPagedConnection<ProductPickupLocation>)context.Source).Facets.OfType<FilterFacetResult>());
    }
}

public class CartPickupLocationPagedConnection<TNode> : PagedConnection<TNode>
{
    public CartPickupLocationPagedConnection(IEnumerable<TNode> superset, int skip, int take, int totalCount)
        : base(superset, skip, take, totalCount)
    {
    }

    public IList<FacetResult> Facets { get; set; }
}

