using System.Threading.Tasks;
using VirtoCommerce.XPickup.Core.Models;

namespace VirtoCommerce.XPickup.Core.Services;

public interface IProductPickupLocationService
{
    Task<ProductPickupLocationSearchResult> SearchPickupLocationsAsync(SingleProductPickupLocationSearchCriteria searchCriteria);
    Task<ProductPickupLocationSearchResult> SearchPickupLocationsAsync(MultipleProductsPickupLocationSearchCriteria searchCriteria);
}
