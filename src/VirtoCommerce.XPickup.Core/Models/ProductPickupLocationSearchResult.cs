using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Models.Facets;

namespace VirtoCommerce.XPickup.Core.Models;

public class ProductPickupLocationSearchResult : GenericSearchResult<ProductPickupLocation>
{
    public IList<FacetResult> Facets { get; set; }
}
