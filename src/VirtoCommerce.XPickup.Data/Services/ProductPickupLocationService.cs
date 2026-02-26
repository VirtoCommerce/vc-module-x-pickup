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
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
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
    IOptionalDependency<IPickupLocationIndexedSearchService> pickupLocationIndexedSearchService,
    ILocalizableSettingService localizableSettingService,
    ISearchPhraseParser searchPhraseParser)
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
        var resultItems = await SearchProductPickupLocationsAsync(product, pickupLocations.Results, productInventories, searchCriteria, globalTransferEnabled);

        result.TotalCount = resultItems.Count;
        result.Results = resultItems;

        ApplySort(result, searchCriteria);
        ApplyPaging(result, searchCriteria);

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
        result.Facets = [];

        if (searchCriteria.Products.Count == 0)
        {
            return result;
        }

        var productIds = searchCriteria.Products.Keys.ToList();
        var products = await itemService.GetAsync(productIds);

        if (products.Count == 0 || !await IsPickupInStoreEnabledAsync(searchCriteria.StoreId))
        {
            return result;
        }

        var globalTransferEnabled = GlobalTransferEnabled(store);

        var pickupLocations = await SearchProductPickupLocationsIndexedAsync(searchCriteria);
        var productInventories = await SearchProductInventoriesAsync(productIds);
        var resultItems = await SearchProductPickupLocationsAsync(products, pickupLocations, productInventories, searchCriteria, globalTransferEnabled);

        result.TotalCount = resultItems.Count;
        result.Results = resultItems;

        if (pickupLocations.Aggregations != null)
        {
            result.Facets.AddRange(pickupLocations.Aggregations
                .Select(x => mapper.Map<FacetResult>(x, options =>
                {
                    options.Items["cultureName"] = searchCriteria.LanguageCode;
                }))
            );

            IList<ProductPickupLocation> allResultItems;
            if (!searchCriteria.Keyword.IsNullOrEmpty() || !searchCriteria.Filter.IsNullOrEmpty())
            {
                var allPickupLocations = await SearchAllProductPickupLocationsIndexedAsync(searchCriteria);

                allResultItems = await SearchProductPickupLocationsAsync(products, allPickupLocations, productInventories, searchCriteria, globalTransferEnabled);
            }
            else
            {
                allResultItems = resultItems;
            }

            CleanupFacets(result, searchCriteria, allResultItems);
        }

        ApplySort(result, searchCriteria);
        ApplyPaging(result, searchCriteria);

        return result;
    }

    private async Task<IList<ProductPickupLocation>> SearchProductPickupLocationsAsync(
        CatalogProduct product,
        IList<PickupLocation> pickupLocations,
        IList<InventoryInfo> productInventories,
        SingleProductPickupLocationSearchCriteria searchCriteria,
        bool globalTransferEnabled)
    {
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

        return resultItems;
    }

    private async Task<IList<ProductPickupLocation>> SearchProductPickupLocationsAsync(
        IList<CatalogProduct> products,
        PickupLocationIndexedSearchResult pickupLocations,
        IList<InventoryInfo> productInventories,
        MultipleProductsPickupLocationSearchCriteria searchCriteria,
        bool globalTransferEnabled)
    {
        var resultItems = new List<ProductPickupLocation>();

        foreach (var pickupLocation in pickupLocations.Results)
        {
            var worstProductAvailability = GetWorstProductAvailability(products, pickupLocation, productInventories, searchCriteria, globalTransferEnabled);

            if (worstProductAvailability != null)
            {
                var productPickupLocation = await CreatePickupLocationFromProductInventoryAsync(pickupLocation, availableQuantity: null, worstProductAvailability, searchCriteria.LanguageCode);
                resultItems.Add(productPickupLocation);
            }
        }

        return resultItems;
    }

    private static string GetWorstProductAvailability(
        IList<CatalogProduct> products,
        PickupLocation pickupLocation,
        IList<InventoryInfo> productInventories,
        MultipleProductsPickupLocationSearchCriteria searchCriteria,
        bool globalTransferEnabled)
    {
        string worstProductAvailability = null;
        var worstAvailabilityPossible = globalTransferEnabled ? ProductPickupAvailability.GlobalTransfer : null;

        foreach (var product in products)
        {
            var pickupLocationProductInventories = productInventories
                .Where(x => x.ProductId == product.Id)
                .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId || pickupLocation.TransferFulfillmentCenterIds.Contains(x.FulfillmentCenterId))
                .ToList();

            var productAvailability = GetProductPickupLocationAvailability(product, pickupLocation, pickupLocationProductInventories, searchCriteria.Products[product.Id].Quantity, globalTransferEnabled);

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

    private async Task<ProductPickupLocation> GetProductPickupLocationAsync(
        CatalogProduct product,
        PickupLocation pickupLocation,
        IList<InventoryInfo> pickupLocationProductInventories,
        long minQuantity,
        string cultureName,
        bool globalTransferEnabled)
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

        var transferPickupLocationsProductQuantity = GetTransferPickupLocationsProductQuantity(pickupLocationProductInventories);
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

    private static string GetProductPickupLocationAvailability(CatalogProduct product,
        PickupLocation pickupLocation,
        IList<InventoryInfo> pickupLocationProductInventories,
        long minQuantity,
        bool globalTransferEnabled)
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

        var transferPickupLocationsProductQuantity = GetTransferPickupLocationsProductQuantity(pickupLocationProductInventories);
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

    private async Task<bool> IsPickupInStoreEnabledAsync(string storeId)
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

    private async Task<PickupLocationIndexedSearchResult> SearchProductPickupLocationsAsync(SingleProductPickupLocationSearchCriteria searchCriteria)
    {
        if (pickupLocationIndexedSearchService.Value == null)
        {
            return AbstractTypeFactory<PickupLocationIndexedSearchResult>.TryCreateInstance();
        }

        var pickupLocationSearchCriteria = AbstractTypeFactory<PickupLocationIndexedSearchCriteria>.TryCreateInstance();

        pickupLocationSearchCriteria.StoreId = searchCriteria.StoreId;
        pickupLocationSearchCriteria.IsActive = true;

        pickupLocationSearchCriteria.Keyword = searchCriteria.Keyword;

        pickupLocationSearchCriteria.Take = PickupLocationsSearchTake;

        return await pickupLocationIndexedSearchService.Value.SearchAsync(pickupLocationSearchCriteria);
    }

    private async Task<PickupLocationIndexedSearchResult> SearchProductPickupLocationsIndexedAsync(MultipleProductsPickupLocationSearchCriteria searchCriteria)
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

    private async Task<PickupLocationIndexedSearchResult> SearchAllProductPickupLocationsIndexedAsync(MultipleProductsPickupLocationSearchCriteria searchCriteria)
    {
        if (pickupLocationIndexedSearchService.Value == null)
        {
            return AbstractTypeFactory<PickupLocationIndexedSearchResult>.TryCreateInstance();
        }

        var pickupLocationSearchCriteria = AbstractTypeFactory<PickupLocationIndexedSearchCriteria>.TryCreateInstance();

        pickupLocationSearchCriteria.StoreId = searchCriteria.StoreId;
        pickupLocationSearchCriteria.IsActive = true;

        pickupLocationSearchCriteria.Take = PickupLocationsSearchTake;

        return await pickupLocationIndexedSearchService.Value.SearchAsync(pickupLocationSearchCriteria);
    }

    private async Task<IList<InventoryInfo>> SearchProductInventoriesAsync(IList<string> productIds)
    {
        if (productInventorySearchService.Value == null)
        {
            return new List<InventoryInfo>();
        }

        var productInventorySearchCriteria = AbstractTypeFactory<ProductInventorySearchCriteria>.TryCreateInstance();
        productInventorySearchCriteria.ProductIds = productIds;
        productInventorySearchCriteria.WithInventoryOnly = true;

        return await productInventorySearchService.Value.SearchAllNoCloneAsync(productInventorySearchCriteria);
    }

    private static long GetMainPickupLocationProductQuantity(PickupLocation pickupLocation, IList<InventoryInfo> pickupLocationProductInventories)
    {
        return pickupLocationProductInventories
            .Where(x => x.FulfillmentCenterId == pickupLocation.FulfillmentCenterId)
            .Select(x => x.InStockQuantity)
            .FirstOrDefault();
    }

    private static long GetTransferPickupLocationsProductQuantity(IList<InventoryInfo> pickupLocationProductInventories)
    {
        return pickupLocationProductInventories
            .Select(x => x.InStockQuantity)
            .DefaultIfEmpty(0)
            .Sum();
    }

    private async Task<ProductPickupLocation> CreatePickupLocationFromProductInventoryAsync(PickupLocation pickupLocation, long? availableQuantity, string productPickupAvailability, string cultureName)
    {
        var result = AbstractTypeFactory<ProductPickupLocation>.TryCreateInstance();

        result.PickupLocation = pickupLocation;
        result.AvailabilityType = productPickupAvailability;
        result.AvailableQuantity = availableQuantity;
        result.AvailabilityNote = await GetProductPickupLocationNoteAsync(productPickupAvailability, cultureName);

        return result;
    }

    private async Task<string> GetProductPickupLocationNoteAsync(string productPickupAvailability, string cultureName)
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

        if (productPickupAvailability == ProductPickupAvailability.Transfer)
        {
            var result = (await localizableSettingService.GetValuesAsync(XPickupConstants.Settings.TransferAvailabilityNote.Name, cultureName)).FirstOrDefault()?.Value;
            if (string.IsNullOrEmpty(result))
            {
                result = "Via transfer";
            }
            return result;
        }

        if (productPickupAvailability == ProductPickupAvailability.GlobalTransfer)
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

    protected static bool GlobalTransferEnabled(Store store)
    {
        return store.Settings.GetValue<bool>(XPickupConstants.Settings.GlobalTransferEnabled);
    }

    private static void ApplySort(ProductPickupLocationSearchResult searchResult, SearchCriteriaBase searchCriteria)
    {
        if (searchCriteria.Sort.IsNullOrEmpty())
        {
            searchResult.Results = searchResult.Results
                .OrderByDescending(x => GetAvailabilityScore(x.AvailabilityType))
                .ThenByDescending(x => x.AvailableQuantity)
                .ThenBy(x => x.PickupLocation.Name)
                .ToList();
        }
    }

    private static void ApplyPaging(ProductPickupLocationSearchResult searchResult, SearchCriteriaBase searchCriteria)
    {
        searchResult.Results = searchResult.Results
             .Skip(searchCriteria.Skip)
             .Take(searchCriteria.Take)
             .ToList();
    }

    private static int GetAvailabilityScore(string availabilityType)
    {
        return availabilityType switch
        {
            ProductPickupAvailability.Today => 30,
            ProductPickupAvailability.Transfer => 20,
            ProductPickupAvailability.GlobalTransfer => 10,
            _ => 0,
        };
    }

    private static string GetWorstAvailability(string productAvailability1, string productAvailability2)
    {
        var score1 = GetAvailabilityScore(productAvailability1);
        var score2 = GetAvailabilityScore(productAvailability2);

        return score1 < score2 ? productAvailability1 : productAvailability2;
    }

    private void CleanupFacets(ProductPickupLocationSearchResult searchResult, MultipleProductsPickupLocationSearchCriteria searchCriteria, IList<ProductPickupLocation> allProductPickupLocations)
    {
        TermFilter countryNameFilter = null;
        TermFilter regionNameFilter = null;
        TermFilter cityFilter = null;

        if (!searchCriteria.Filter.IsNullOrEmpty())
        {
            var parsedFilter = searchPhraseParser.Parse(searchCriteria.Filter);
            var termFilters = parsedFilter.Filters.OfType<TermFilter>().ToList();

            countryNameFilter = termFilters.FirstOrDefault(x => x.FieldName.EqualsIgnoreCase(PickupLocationIndexFields.AddressCountryName));
            regionNameFilter = termFilters.FirstOrDefault(x => x.FieldName.EqualsIgnoreCase(PickupLocationIndexFields.AddressRegionName));
            cityFilter = termFilters.FirstOrDefault(x => x.FieldName.EqualsIgnoreCase(PickupLocationIndexFields.AddressCity));
        }

        var countryNameFacet = searchResult.Facets.FirstOrDefault(x => x.Name.EqualsIgnoreCase(PickupLocationIndexFields.AddressCountryName)) as TermFacetResult;
        var regionNameFacet = searchResult.Facets.FirstOrDefault(x => x.Name.EqualsIgnoreCase(PickupLocationIndexFields.AddressRegionName)) as TermFacetResult;
        var cityFacet = searchResult.Facets.FirstOrDefault(x => x.Name.EqualsIgnoreCase(PickupLocationIndexFields.AddressCity)) as TermFacetResult;

        if (countryNameFacet != null || regionNameFacet != null || cityFacet != null)
        {
            var filteredAddresses = searchResult.Results.Where(x => x.PickupLocation.Address != null).Select(x => x.PickupLocation.Address).ToList();
            var allAddresses = allProductPickupLocations.Where(x => x.PickupLocation.Address != null).Select(x => x.PickupLocation.Address).ToList();

            if (countryNameFacet != null)
            {
                CleanupCountryNameFacet(countryNameFacet, countryNameFilter, filteredAddresses, allAddresses);
            }

            if (regionNameFacet != null)
            {
                CleanupRegionNameFacet(regionNameFacet, regionNameFilter, filteredAddresses, allAddresses);
            }

            if (cityFacet != null)
            {
                CleanupCityFacet(cityFacet, cityFilter, filteredAddresses, allAddresses);
            }
        }
    }

    private static void CleanupCountryNameFacet(TermFacetResult countryNameFacet, TermFilter countryNameFilter, IList<PickupLocationAddress> filteredAddresses, IList<PickupLocationAddress> allAddresses)
    {
        var filterApplied = countryNameFilter != null;

        var countryNames = new HashSet<string>((filterApplied ? allAddresses : filteredAddresses).Select(x => x.CountryName), StringComparer.OrdinalIgnoreCase);

        countryNameFacet.Terms = countryNameFacet.Terms.Where(x => countryNames.Contains(x.Term)).ToList();

        foreach (var term in countryNameFacet.Terms)
        {
            var termApplied = filterApplied && countryNameFilter.Values.Contains(term.Term, StringComparer.OrdinalIgnoreCase);
            term.Count = (termApplied || !filterApplied ? filteredAddresses : allAddresses).Count(x => term.Term.EqualsIgnoreCase(x.CountryName));
        }
    }

    private static void CleanupRegionNameFacet(TermFacetResult regionNameFacet, TermFilter regionNameFilter, IList<PickupLocationAddress> filteredAddresses, IList<PickupLocationAddress> allAddresses)
    {
        var filterApplied = regionNameFilter != null;

        var regionNames = new HashSet<string>((filterApplied ? allAddresses : filteredAddresses).Select(x => x.RegionName), StringComparer.OrdinalIgnoreCase);

        regionNameFacet.Terms = regionNameFacet.Terms.Where(x => regionNames.Contains(x.Term)).ToList();

        foreach (var term in regionNameFacet.Terms)
        {
            var termApplied = filterApplied && regionNameFilter.Values.Contains(term.Term, StringComparer.OrdinalIgnoreCase);
            term.Count = (termApplied || !filterApplied ? filteredAddresses : allAddresses).Count(x => term.Term.EqualsIgnoreCase(x.RegionName));
        }
    }

    private static void CleanupCityFacet(TermFacetResult cityFacet, TermFilter cityFilter, IList<PickupLocationAddress> filteredAddresses, IList<PickupLocationAddress> allAddresses)
    {
        var filterApplied = cityFilter != null;

        var cities = new HashSet<string>((filterApplied ? allAddresses : filteredAddresses).Select(x => x.City), StringComparer.OrdinalIgnoreCase);

        cityFacet.Terms = cityFacet.Terms.Where(x => cities.Contains(x.Term)).ToList();

        foreach (var term in cityFacet.Terms)
        {
            var termApplied = filterApplied && cityFilter.Values.Contains(term.Term, StringComparer.OrdinalIgnoreCase);
            term.Count = (termApplied || !filterApplied ? filteredAddresses : allAddresses).Count(x => term.Term.EqualsIgnoreCase(x.City));
        }
    }
}
