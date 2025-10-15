using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.XPickup.Core.Models;

public class MultipleProductsPickupLocationSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }
    public IDictionary<string, ProductPickupLocationSearchCriteriaItem> Products { get; set; }

    public string Facet { get; set; }
    public string Filter { get; set; }
}
