using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.XPickup.Data.Services;

public interface IXPickupMapper
{
    FacetResult ToFacetResult(Aggregation source, FacetMappingContext context);

    FacetMappingContext CreateFacetMappingContext(string cultureName);
}
