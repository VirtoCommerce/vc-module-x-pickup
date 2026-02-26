using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using VirtoCommerce.CartModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Xapi.Core.Infrastructure;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Queries;
using VirtoCommerce.XPickup.Core.Services;

namespace VirtoCommerce.XPickup.Data.Queries;

public class SearchCartPickupLocationsQueryHandler(IProductPickupLocationService productPickupLocationService, IShoppingCartService shoppingCartService)
    : IQueryHandler<SearchCartPickupLocationsQuery, ProductPickupLocationSearchResult>
{
    public async Task<ProductPickupLocationSearchResult> Handle(SearchCartPickupLocationsQuery request, CancellationToken cancellationToken)
    {
        var searchCriteria = await CreateSearchCriteriaAsync(request);

        return await productPickupLocationService.SearchPickupLocationsAsync(searchCriteria);
    }

    protected virtual async Task<MultipleProductsPickupLocationSearchCriteria> CreateSearchCriteriaAsync(SearchCartPickupLocationsQuery request)
    {
        var cart = await shoppingCartService.GetNoCloneAsync(request.CartId);
        if (cart == null)
        {
            throw new InvalidOperationException($"Cart with id {request.CartId} not found");
        }

        var result = AbstractTypeFactory<MultipleProductsPickupLocationSearchCriteria>.TryCreateInstance();

        result.StoreId = request.StoreId;

        result.Products = cart.Items
            .Where(x => x.SelectedForCheckout)
            .GroupBy(x => x.ProductId)
            .Select(g => new ProductPickupLocationSearchCriteriaItem
            {
                ProductId = g.Key,
                Quantity = g.Sum(x => x.Quantity)
            })
            .ToDictionary(x => x.ProductId);

        result.Keyword = request.Keyword;
        result.LanguageCode = request.CultureName;

        result.Facet = request.Facet;
        result.Filter = request.Filter;

        result.Sort = request.Sort;
        result.Skip = request.Skip;
        result.Take = request.Take;

        return result;
    }
}
