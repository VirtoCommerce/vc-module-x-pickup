using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Queries;
using VirtoCommerce.XPickup.Core.Services;

namespace VirtoCommerce.XPickup.Data.Queries;

public class SearchCartPickupLocationsQueryHandler(
    IOptionalDependency<IProductPickupLocationService> productPickupLocationService,
    IShoppingCartService shoppingCartService)
    : IQueryHandler<SearchCartPickupLocationsQuery, ProductPickupLocationSearchResult>
{
    public async Task<ProductPickupLocationSearchResult> Handle(SearchCartPickupLocationsQuery request, CancellationToken cancellationToken)
    {
        var cart = await shoppingCartService.GetNoCloneAsync(request.CartId);
        if (cart == null)
        {
            throw new InvalidOperationException($"Cart with id {request.CartId} not found");
        }

        if (productPickupLocationService.Value == null)
        {
            return AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();
        }

        var searchCriteria = AbstractTypeFactory<MultipleProductsPickupLocationSearchCriteria>.TryCreateInstance();

        searchCriteria.StoreId = request.StoreId;
        searchCriteria.Products = cart.Items
            .Where(x => x.SelectedForCheckout)
            .Select(x => new ProductPickupLocationSearchCriteriaItem { ProductId = x.ProductId, Quantity = x.Quantity })
            .ToDictionary(x => x.ProductId);

        searchCriteria.Keyword = request.Keyword;
        searchCriteria.LanguageCode = request.CultureName;

        searchCriteria.Sort = request.Sort;
        searchCriteria.Skip = request.Skip;
        searchCriteria.Take = request.Take;

        return await productPickupLocationService.Value.SearchPickupLocationsAsync(searchCriteria);
    }
}
