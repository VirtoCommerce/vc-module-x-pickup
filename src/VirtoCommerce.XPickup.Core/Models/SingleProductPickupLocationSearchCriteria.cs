using VirtoCommerce.Platform.Core.Common;

namespace VirtoCommerce.XPickup.Core.Models;

public class SingleProductPickupLocationSearchCriteria : SearchCriteriaBase
{
    public string StoreId { get; set; }
    public ProductPickupLocationSearchCriteriaItem Product { get; set; }
}
