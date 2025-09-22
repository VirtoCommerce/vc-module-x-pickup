using VirtoCommerce.ShippingModule.Core.Model;

namespace VirtoCommerce.XPickup.Core.Models;

public class ProductPickupLocation
{
    public PickupLocation PickupLocation { get; set; }

    public string AvailabilityType { get; set; }
    public string AvailabilityNote { get; set; }
    public long? AvailableQuantity { get; set; }
}
