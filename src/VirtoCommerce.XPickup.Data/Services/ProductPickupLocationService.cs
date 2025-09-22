using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using VirtoCommerce.XPickup.Core.Models;
using VirtoCommerce.XPickup.Core.Services;
using ShippingConstants = VirtoCommerce.ShippingModule.Core.ModuleConstants;
using XPickupConstants = VirtoCommerce.XPickup.Core.ModuleConstants;

namespace VirtoCommerce.XPickup.Data.Services;

public class ProductPickupLocationService(
    IOptionalDependency<IProductInventorySearchService> productInventorySearchService,
    IOptionalDependency<IShippingMethodsSearchService> shippingMethodsSearchService,
    IOptionalDependency<IPickupLocationSearchService> pickupLocationSearchService,
    ILocalizableSettingService localizableSettingService)
    : IProductPickupLocationService
{
    public virtual async Task<bool> IsPickupInStoreEnabledAsync(string storeId)
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

    public virtual async Task<IList<PickupLocation>> SearchProductPickupLocationsAsync(string storeId, string keyword)
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

    public virtual async Task<IList<InventoryInfo>> SearchProductInventoriesAsync(IList<string> productIds)
    {
        if (productInventorySearchService.Value == null)
        {
            return new List<InventoryInfo>();
        }

        var productInventorySearchCriteria = AbstractTypeFactory<ProductInventorySearchCriteria>.TryCreateInstance();
        productInventorySearchCriteria.ProductIds = productIds;

        return await productInventorySearchService.Value.SearchAllProductInventoriesNoCloneAsync(productInventorySearchCriteria);
    }

    public virtual InventoryInfo GetMainPickupLocationProductInventory(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, bool order)
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

    public virtual InventoryInfo GetTransferPickupLocationProductInventory(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories, long minQuantity, bool order)
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

    public virtual async Task<ProductPickupLocation> CreatePickupLocationFromProductInventoryAsync(PickupLocation pickupLocation, InventoryInfo productInventoryInfo, string productPickupAvailability, string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.Id = pickupLocation.Id;
        result.Name = pickupLocation.Name;
        result.Address = pickupLocation.Address?.ToString();
        result.GeoLocation = pickupLocation.GeoLocation;
        result.AvailabilityType = productPickupAvailability;
        result.AvailableQuantity = productInventoryInfo?.InStockQuantity;
        result.Note = await GetProductPickupLocationNoteAsync(productPickupAvailability, cultureName);

        return result;
    }

    protected virtual async Task<string> GetProductPickupLocationNoteAsync(string productPickupAvailability, string cultureName)
    {
        if (productPickupAvailability == ProductPickupAvailabilityType.Today)
        {
            var result = (await localizableSettingService.GetValuesAsync(XPickupConstants.Settings.TodayAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Today";
            }
            return result;
        }
        else if (productPickupAvailability == ProductPickupAvailabilityType.Transfer)
        {
            var result = (await localizableSettingService.GetValuesAsync(XPickupConstants.Settings.TransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Via transfer";
            }
            return result;
        }
        else if (productPickupAvailability == ProductPickupAvailabilityType.GlobalTransfer)
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

    public bool GlobalTransferEnabled(Store store)
    {
        return store.Settings.GetValue<bool>(XPickupConstants.Settings.GlobalTransferEnabled);
    }

    public virtual IEnumerable<ProductPickupLocation> ApplySort(IList<ProductPickupLocation> items, string sort)
    {
        if (sort.IsNullOrEmpty())
        {
            return items
                .OrderBy(x => GetAvaiabilitySortOrder(x.AvailabilityType))
                .ThenByDescending(x => x.AvailableQuantity)
                .ThenBy(x => x.Name);
        }

        return items;
    }

    protected virtual int GetAvaiabilitySortOrder(string availabilityType)
    {
        return availabilityType switch
        {
            ProductPickupAvailabilityType.Today => 10,
            ProductPickupAvailabilityType.Transfer => 20,
            ProductPickupAvailabilityType.GlobalTransfer => 30,
            _ => 100
        };
    }
}
