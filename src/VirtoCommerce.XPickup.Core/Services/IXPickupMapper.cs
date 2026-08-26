using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.XPickup.Core.Services;

public interface IXPickupMapper
{
    FacetResult ToFacetResult(Aggregation source, FacetMappingContext context);
}
