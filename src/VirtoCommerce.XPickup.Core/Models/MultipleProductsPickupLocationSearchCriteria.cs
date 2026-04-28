using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.XPickup.Core.Models;

public class MultipleProductsPickupLocationSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }
    public IDictionary<string, ProductPickupLocationSearchCriteriaItem> Products { get; set; }

    public string Facet { get; set; }
    public string Filter { get; set; }

    /// <summary>
    /// Pickup location ids that must appear in the response regardless of paging, keyword or filter.
    /// Missing ones are fetched and prepended after the regular search and paging are applied.
    /// </summary>
    public IList<string> IncludeLocationIds { get; set; }
}
