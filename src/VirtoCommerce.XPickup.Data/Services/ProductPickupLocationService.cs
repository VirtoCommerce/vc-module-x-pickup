using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using VirtoCommerce.CatalogModule.Core.Model;
using VirtoCommerce.CatalogModule.Core.Services;
using VirtoCommerce.InventoryModule.Core.Model;
using VirtoCommerce.InventoryModule.Core.Model.Search;
using VirtoCommerce.InventoryModule.Core.Services;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.Platform.Core.Modularity;
using VirtoCommerce.Platform.Core.Settings;
using VirtoCommerce.ShippingModule.Core.Model;
using VirtoCommerce.ShippingModule.Core.Model.Search;
using VirtoCommerce.ShippingModule.Core.Model.Search.Indexed;
using VirtoCommerce.ShippingModule.Core.Search.Indexed;
using VirtoCommerce.ShippingModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.Xapi.Core.Models.Facets;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Services;
using ShippingConstants = VirtoCommerce.ShippingModule.Core.ModuleConstants;
using XPickupConstants = VirtoCommerce.XPickup.Core.ModuleConstants;

namespace VirtoCommerce.XPickup.Data.Services;

public class ProductPickupLocationService(
    IMapper mapper,
    IStoreService storeService,
    IItemService itemService,
    IOptionalDependency<IProductInventorySearchService> productInventorySearchService,
    IOptionalDependency<IShippingMethodsSearchService> shippingMethodsSearchService,
    IOptionalDependency<IPickupLocationSearchService> pickupLocationSearchService,
    IOptionalDependency<IPickupLocationIndexedSearchService> pickupLocationIndexedSearchService,
    ILocalizableSettingService localizableSettingService)
    : IProductPickupLocationService
{
    private const int PickupLocationsSearchTake = 500;

    public virtual async Task<ProductPickupLocationSearchResult> SearchPickupLocationsAsync(SingleProductPickupLocationSearchCriteria searchCriteria)
    {
        ArgumentNullException.ThrowIfNull(searchCriteria);
        ArgumentNullException.ThrowIfNull(searchCriteria.Product);

        ArgumentException.ThrowIfNullOrEmpty(searchCriteria.StoreId);
        ArgumentException.ThrowIfNullOrEmpty(searchCriteria.Product.ProductId);

        var store = await storeService.GetNoCloneAsync(searchCriteria.StoreId);
        if (store == null)
        {
            throw new InvalidOperationException($"Store with id {searchCriteria.StoreId} not found");
        }

        var product = await itemService.GetNoCloneAsync(searchCriteria.Product.ProductId);
        if (product == null)
        {
            throw new InvalidOperationException($"Product with id {searchCriteria.Product.ProductId} not found");
        }

        var result = AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();

        if (!await IsPickupInStoreEnabledAsync(searchCriteria.StoreId))
        {
            return result;
        }

        var globalTransferEnabled = GlobalTransferEnabled(store);

        var pickupLocations = await SearchProductPickupLocationsAsync(searchCriteria);

        var productInventories = await SearchProductInventoriesAsync([searchCriteria.Product.ProductId]);

        var resultItems = new List<ProductPickupLocation>();

        foreach (var pickupLocation in pickupLocations)
        {
            var pickupLocationProductInventories = productInventories
                .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId || pickupLocation.TransferFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
                .ToList();

            var productPickupLocation = await GetProductPickupLocationAsync(product, pickupLocation, pickupLocationProductInventories, searchCriteria.Product.Quantity, searchCriteria.LanguageCode, globalTransferEnabled);
            if (productPickupLocation != null)
            {
                resultItems.Add(productPickupLocation);
            }
        }

        result.TotalCount = resultItems.Count;
        result.Results = ApplySort(resultItems, searchCriteria.Sort).Skip(searchCriteria.Skip).Take(searchCriteria.Take).ToList();

        return result;
    }

    public virtual async Task<ProductPickupLocationSearchResult> SearchPickupLocationsAsync(MultipleProductsPickupLocationSearchCriteria searchCriteria)
    {
        ArgumentNullException.ThrowIfNull(searchCriteria);
        ArgumentNullException.ThrowIfNull(searchCriteria.Products);

        ArgumentException.ThrowIfNullOrEmpty(searchCriteria.StoreId);

        var store = await storeService.GetNoCloneAsync(searchCriteria.StoreId);
        if (store == null)
        {
            throw new InvalidOperationException($"Store with id {searchCriteria.StoreId} not found");
        }

        var result = AbstractTypeFactory<ProductPickupLocationSearchResult>.TryCreateInstance();

        if (!await IsPickupInStoreEnabledAsync(searchCriteria.StoreId))
        {
            return result;
        }

        if (searchCriteria.Products.Count == 0)
        {
            return result;
        }

        var globalTransferEnabled = GlobalTransferEnabled(store);

        var productIds = searchCriteria.Products.Keys.ToList();

        var products = await itemService.GetByIdsAsync(productIds, responseGroup: null, catalogId: null);

        var pickupLocations = await SearchProductPickupLocationsIndexedAsync(searchCriteria);

        var productInventories = await SearchProductInventoriesAsync(productIds);

        var resultItems = new List<ProductPickupLocation>();

        foreach (var pickupLocation in pickupLocations.Results)
        {
            var worstProductAvailability = GetWorstProductAvailability(store, products, pickupLocation, productInventories, searchCriteria, globalTransferEnabled);

            if (worstProductAvailability != null)
            {
                var productPickupLocation = await CreatePickupLocationFromProductInventoryAsync(pickupLocation, availableQuantity: null, worstProductAvailability, searchCriteria.LanguageCode);
                resultItems.Add(productPickupLocation);
            }
        }

        result.TotalCount = resultItems.Count;
        result.Results = ApplySort(resultItems, searchCriteria.Sort).Skip(searchCriteria.Skip).Take(searchCriteria.Take).ToList();

        result.Facets = pickupLocations.Aggregations?
            .Select(x => mapper.Map<FacetResult>(x, options =>
            {
                options.Items["cultureName"] = searchCriteria.LanguageCode;
            }))
            .ToList() ?? [];

        return result;
    }

    protected virtual string GetWorstProductAvailability(Store store, IList<CatalogProduct> products, PickupLocation pickupLocation, IList<InventoryInfo> productInventories, MultipleProductsPickupLocationSearchCriteria searchCriteria, bool globalTransferEnabled)
    {
        var worstAvailabilityPossible = globalTransferEnabled ? ProductPickupAvailability.GlobalTransfer : null;

        var worstProductAvailability = default(string);

        foreach (var product in products)
        {
            var pickupLocationProductInventories = productInventories
                .Where(x => x.ProductId == product.Id)
                .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId || pickupLocation.TransferFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
                .ToList();

            var productAvailability = GetProductPickupLocationAvailability(store, product, pickupLocation, pickupLocationProductInventories, searchCriteria.Products[product.Id].Quantity, searchCriteria.LanguageCode, globalTransferEnabled);

            if (worstProductAvailability == null)
            {
                worstProductAvailability = productAvailability;
            }
            else
            {
                worstProductAvailability = GetWorstAvailability(worstProductAvailability, productAvailability);
            }

            if (worstProductAvailability == worstAvailabilityPossible)
            {
                break;
            }
        }

        return worstProductAvailability;
    }

    protected virtual async Task<ProductPickupLocation> GetProductPickupLocationAsync(CatalogProduct product, PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, string cultureName, bool globalTransferEnabled)
    {
        if (!product.TrackInventory.GetValueOrDefault())
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, availableQuantity: null, ProductPickupAvailability.Today, cultureName);
        }

        var mainPickupLocationProductQuantity = GetMainPickupLocationProductQuantity(pickupLocation, pickupLocationProductInventories);
        if (mainPickupLocationProductQuantity >= minQuantity)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, mainPickupLocationProductQuantity, ProductPickupAvailability.Today, cultureName);
        }

        var transferPickupLocationsProductQuantity = GetTransferPickupLocationsProductQuantity(pickupLocation, pickupLocationProductInventories);
        if (transferPickupLocationsProductQuantity >= minQuantity)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, transferPickupLocationsProductQuantity, ProductPickupAvailability.Transfer, cultureName);
        }

        if (globalTransferEnabled)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, availableQuantity: null, ProductPickupAvailability.GlobalTransfer, cultureName);
        }

        return null;
    }

    protected virtual string GetProductPickupLocationAvailability(Store store, CatalogProduct product, PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, string cultureName, bool globalTransferEnabled)
    {
        if (!product.TrackInventory.GetValueOrDefault())
        {
            return ProductPickupAvailability.Today;
        }

        var mainPickupLocationProductQuantity = GetMainPickupLocationProductQuantity(pickupLocation, pickupLocationProductInventories);
        if (mainPickupLocationProductQuantity >= minQuantity)
        {
            return ProductPickupAvailability.Today;
        }

        var transferPickupLocationsProductQuantity = GetTransferPickupLocationsProductQuantity(pickupLocation, pickupLocationProductInventories);
        if (transferPickupLocationsProductQuantity >= minQuantity)
        {
            return ProductPickupAvailability.Transfer;
        }

        if (globalTransferEnabled)
        {
            return ProductPickupAvailability.GlobalTransfer;
        }

        return null;
    }

    protected virtual async Task<bool> IsPickupInStoreEnabledAsync(string storeId)
    {
        if (shippingMethodsSearchService.Value == null)
        {
            return false;
        }

        var shippingMethodsSearchCriteria = AbstractTypeFactory<ShippingMethodsSearchCriteria>.TryCreateInstance();
        shippingMethodsSearchCriteria.StoreId = storeId;
        shippingMethodsSearchCriteria.IsActive = true;
        shippingMethodsSearchCriteria.Codes = [ShippingConstants.BuyOnlinePickupInStoreShipmentCode];
        shippingMethodsSearchCriteria.Take = 1;

        return (await shippingMethodsSearchService.Value.SearchNoCloneAsync(shippingMethodsSearchCriteria)).TotalCount > 0;
    }

    protected virtual async Task<IList<PickupLocation>> SearchProductPickupLocationsAsync(SingleProductPickupLocationSearchCriteria searchCriteria)
    {
        if (pickupLocationSearchService.Value == null)
        {
            return new List<PickupLocation>();
        }

        var pickupLocationSearchCriteria = AbstractTypeFactory<PickupLocationSearchCriteria>.TryCreateInstance();

        pickupLocationSearchCriteria.StoreId = searchCriteria.StoreId;
        pickupLocationSearchCriteria.IsActive = true;

        pickupLocationSearchCriteria.Keyword = searchCriteria.Keyword;

        pickupLocationSearchCriteria.Take = PickupLocationsSearchTake;

        return await pickupLocationSearchService.Value.SearchAllNoCloneAsync(pickupLocationSearchCriteria);
    }

    protected virtual async Task<PickupLocationIndexedSearchResult> SearchProductPickupLocationsIndexedAsync(MultipleProductsPickupLocationSearchCriteria searchCriteria)
    {
        if (pickupLocationIndexedSearchService.Value == null)
        {
            return AbstractTypeFactory<PickupLocationIndexedSearchResult>.TryCreateInstance();
        }

        var pickupLocationSearchCriteria = AbstractTypeFactory<PickupLocationIndexedSearchCriteria>.TryCreateInstance();

        pickupLocationSearchCriteria.StoreId = searchCriteria.StoreId;
        pickupLocationSearchCriteria.IsActive = true;

        pickupLocationSearchCriteria.Facet = searchCriteria.Facet;
        pickupLocationSearchCriteria.Filter = searchCriteria.Filter;
        pickupLocationSearchCriteria.Keyword = searchCriteria.Keyword;

        pickupLocationSearchCriteria.Take = PickupLocationsSearchTake;

        return await pickupLocationIndexedSearchService.Value.SearchAsync(pickupLocationSearchCriteria);
    }

    protected virtual async Task<IList<InventoryInfo>> SearchProductInventoriesAsync(IList<string> productIds)
    {
        if (productInventorySearchService.Value == null)
        {
            return new List<InventoryInfo>();
        }

        var productInventorySearchCriteria = AbstractTypeFactory<ProductInventorySearchCriteria>.TryCreateInstance();
        productInventorySearchCriteria.ProductIds = productIds;
        productInventorySearchCriteria.WithInventoryOnly = true;

        return await productInventorySearchService.Value.SearchAllProductInventoriesNoCloneAsync(productInventorySearchCriteria);
    }

    protected virtual long GetMainPickupLocationProductQuantity(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories)
    {
        return pickupLocationProductInventories
            .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId)
            .Select(x => x.InStockQuantity)
            .FirstOrDefault();
    }

    protected virtual long GetTransferPickupLocationsProductQuantity(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories)
    {
        return pickupLocationProductInventories
            .Select(x => x.InStockQuantity)
            .DefaultIfEmpty(0)
            .Sum();
    }

    protected virtual async Task<ProductPickupLocation> CreatePickupLocationFromProductInventoryAsync(PickupLocation pickupLocation, long? availableQuantity, string productPickupAvailability, string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.PickupLocation = pickupLocation;
        result.AvailabilityType = productPickupAvailability;
        result.AvailableQuantity = availableQuantity;
        result.AvailabilityNote = await GetProductPickupLocationNoteAsync(productPickupAvailability, cultureName);

        return result;
    }

    protected virtual async Task<string> GetProductPickupLocationNoteAsync(string productPickupAvailability, string cultureName)
    {
        if (productPickupAvailability == ProductPickupAvailability.Today)
        {
            var result = (await localizableSettingService.GetValuesAsync(XPickupConstants.Settings.TodayAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Today";
            }
            return result;
        }
        else if (productPickupAvailability == ProductPickupAvailability.Transfer)
        {
            var result = (await localizableSettingService.GetValuesAsync(XPickupConstants.Settings.TransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Via transfer";
            }
            return result;
        }
        else if (productPickupAvailability == ProductPickupAvailability.GlobalTransfer)
        {
            var result = (await localizableSettingService.GetValuesAsync(XPickupConstants.Settings.GlobalTransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Via transfer";
            }
            return result;
        }

        return null;
    }

    protected bool GlobalTransferEnabled(Store store)
    {
        return store.Settings.GetValue<bool>(XPickupConstants.Settings.GlobalTransferEnabled);
    }

    protected virtual IEnumerable<ProductPickupLocation> ApplySort(IList<ProductPickupLocation> items, string sort)
    {
        if (sort.IsNullOrEmpty())
        {
            return items
                .OrderByDescending(x => GetAvailabilityScore(x.AvailabilityType))
                .ThenByDescending(x => x.AvailableQuantity)
                .ThenBy(x => x.PickupLocation.Name);
        }

        return items;
    }

    protected virtual int GetAvailabilityScore(string availabilityType)
    {
        return availabilityType switch
        {
            ProductPickupAvailability.Today => 30,
            ProductPickupAvailability.Transfer => 20,
            ProductPickupAvailability.GlobalTransfer => 10,
            _ => 0
        };
    }

    protected virtual string GetWorstAvailability(string productAvailability1, string productAvailability2)
    {
        var score1 = GetAvailabilityScore(productAvailability1);
        var score2 = GetAvailabilityScore(productAvailability2);

        return score1 < score2 ? productAvailability1 : productAvailability2;
    }
}
