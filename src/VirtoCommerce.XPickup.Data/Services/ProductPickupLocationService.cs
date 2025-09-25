using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using VirtoCommerce.ShippingModule.Core.Services;
using VirtoCommerce.StoreModule.Core.Model;
using VirtoCommerce.StoreModule.Core.Services;
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Services;
using ShippingConstants = VirtoCommerce.ShippingModule.Core.ModuleConstants;
using XPickupConstants = VirtoCommerce.XPickup.Core.ModuleConstants;

namespace VirtoCommerce.XPickup.Data.Services;

public class ProductPickupLocationService(
    IStoreService storeService,
    IItemService itemService,
    IOptionalDependency<IProductInventorySearchService> productInventorySearchService,
    IOptionalDependency<IShippingMethodsSearchService> shippingMethodsSearchService,
    IOptionalDependency<IPickupLocationSearchService> pickupLocationSearchService,
    ILocalizableSettingService localizableSettingService)
    : IProductPickupLocationService
{
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

        if (await IsPickupInStoreEnabledAsync(searchCriteria.StoreId))
        {
            var globalTransferEnabled = GlobalTransferEnabled(store);

            var pickupLocations = await SearchProductPickupLocationsAsync(searchCriteria.StoreId, searchCriteria.Keyword);

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
        }

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

        var globalTransferEnabled = GlobalTransferEnabled(store);

        var productIds = searchCriteria.Products.Keys.ToList();

        var products = await itemService.GetByIdsAsync(productIds, responseGroup: null, catalogId: null);

        var pickupLocations = await SearchProductPickupLocationsAsync(searchCriteria.StoreId, searchCriteria.Keyword);

        var productInventories = await SearchProductInventoriesAsync(productIds);

        var resultItems = new List<ProductPickupLocation>();

        var worstAvailability = globalTransferEnabled ? ProductPickupAvailability.GlobalTransfer : null;

        foreach (var pickupLocation in pickupLocations)
        {
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

                if (worstProductAvailability == worstAvailability)
                {
                    break;
                }
            }

            if (worstProductAvailability != null)
            {
                var productPickupLocation = await CreatePickupLocationFromProductInventoryAsync(pickupLocation, productInventoryInfo: null, worstProductAvailability, searchCriteria.LanguageCode);
                resultItems.Add(productPickupLocation);
            }
        }

        result.TotalCount = resultItems.Count;
        result.Results = ApplySort(resultItems, searchCriteria.Sort).Skip(searchCriteria.Skip).Take(searchCriteria.Take).ToList();

        return result;
    }

    protected virtual async Task<ProductPickupLocation> GetProductPickupLocationAsync(CatalogProduct product, PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, string cultureName, bool globalTransferEnabled)
    {
        if (!product.TrackInventory.GetValueOrDefault())
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, productInventoryInfo: null, ProductPickupAvailability.Today, cultureName);
        }

        var mainPickupLocationProductInventory = GetMainPickupLocationProductInventory(pickupLocation, pickupLocationProductInventories, minQuantity, order: true);
        if (mainPickupLocationProductInventory != null)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, mainPickupLocationProductInventory, ProductPickupAvailability.Today, cultureName);
        }

        var transferPickupLocationProductInventory = GetTransferPickupLocationProductInventory(pickupLocation, pickupLocationProductInventories, minQuantity, order: true);
        if (transferPickupLocationProductInventory != null)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, transferPickupLocationProductInventory, ProductPickupAvailability.Transfer, cultureName);
        }

        if (globalTransferEnabled)
        {
            return await CreatePickupLocationFromProductInventoryAsync(pickupLocation, productInventoryInfo: null, ProductPickupAvailability.GlobalTransfer, cultureName);
        }

        return null;
    }

    protected virtual string GetProductPickupLocationAvailability(Store store, CatalogProduct product, PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, string cultureName, bool globalTransferEnabled)
    {
        if (!product.TrackInventory.GetValueOrDefault())
        {
            return ProductPickupAvailability.Today;
        }

        var mainPickupLocationProductInventory = GetMainPickupLocationProductInventory(pickupLocation, pickupLocationProductInventories, minQuantity, order: true);
        if (mainPickupLocationProductInventory != null)
        {
            return ProductPickupAvailability.Today;
        }

        var transferPickupLocationProductInventory = GetTransferPickupLocationProductInventory(pickupLocation, pickupLocationProductInventories, minQuantity, order: true);
        if (transferPickupLocationProductInventory != null)
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
        shippingMethodsSearchCriteria.Skip = 0;
        shippingMethodsSearchCriteria.Take = 1;

        return (await shippingMethodsSearchService.Value.SearchNoCloneAsync(shippingMethodsSearchCriteria)).TotalCount > 0;
    }

    protected virtual async Task<IList<PickupLocation>> SearchProductPickupLocationsAsync(string storeId, string keyword)
    {
        if (pickupLocationSearchService.Value == null)
        {
            return new List<PickupLocation>();
        }

        var pickupLocationSearchCriteria = AbstractTypeFactory<PickupLocationSearchCriteria>.TryCreateInstance();
        pickupLocationSearchCriteria.StoreId = storeId;
        pickupLocationSearchCriteria.IsActive = true;
        pickupLocationSearchCriteria.Keyword = keyword;

        return await pickupLocationSearchService.Value.SearchAllNoCloneAsync(pickupLocationSearchCriteria);
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

    protected virtual InventoryInfo GetMainPickupLocationProductInventory(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, bool order)
    {
        var query = pickupLocationProductInventories
            .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId)
            .Where(x => x.InStockQuantity >= minQuantity);

        if (order)
        {
            query = query.OrderByDescending(x => x.InStockQuantity);
        }

        return query.FirstOrDefault();
    }

    protected virtual InventoryInfo GetTransferPickupLocationProductInventory(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, bool order)
    {
        var query = pickupLocationProductInventories
            .Where(x => pickupLocation.TransferFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
            .Where(x => x.InStockQuantity >= minQuantity);

        if (order)
        {
            query = query.OrderByDescending(x => x.InStockQuantity);
        }

        return query.FirstOrDefault();
    }

    protected virtual async Task<ProductPickupLocation> CreatePickupLocationFromProductInventoryAsync(PickupLocation pickupLocation, InventoryInfo productInventoryInfo, string productPickupAvailability, string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.PickupLocation = pickupLocation;
        result.AvailabilityType = productPickupAvailability;
        result.AvailableQuantity = productInventoryInfo?.InStockQuantity;
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
                .OrderByDescending(x => GetAvaiabilityScore(x.AvailabilityType))
                .ThenByDescending(x => x.AvailableQuantity)
                .ThenBy(x => x.PickupLocation.Name);
        }

        return items;
    }

    protected virtual int GetAvaiabilityScore(string availabilityType)
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
        var score1 = GetAvaiabilityScore(productAvailability1);
        var score2 = GetAvaiabilityScore(productAvailability2);

        return score1 < score2 ? productAvailability1 : productAvailability2;
    }
}
