using System.Threading.Tasks;
using VirtoCommerce.XPickup.Core.Models;

namespace VirtoCommerce.XPickup.Core.Services;

public interface IProductPickupLocationService
{
    Task<ProductPickupLocationSearchResult> SearchPickupLocations(SingleProductPickupLocationSearchCriteria searchCriteria);
    Task<ProductPickupLocationSearchResult> SearchPickupLocations(MultipleProductsPickupLocationSearchCriteria searchCriteria);
}
