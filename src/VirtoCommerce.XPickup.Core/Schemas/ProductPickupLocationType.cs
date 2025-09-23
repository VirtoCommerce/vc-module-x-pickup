using VirtoCommerce.Xapi.Core.Schemas;
using VirtoCommerce.XPickup.Core.Models;

namespace VirtoCommerce.XPickup.Core.Schemas;

public class ProductPickupLocationType : ExtendableGraphType<ProductPickupLocation>
{
    public ProductPickupLocationType()
    {
        Name = "ProductPickupLocation";

        Field(x => x.PickupLocation.Id).Description("Id");
        Field(x => x.PickupLocation.IsActive, false).Description("IsActive");
        Field(x => x.PickupLocation.Name, false).Description("Name");
        Field(x => x.PickupLocation.Description, nullable: true).Description("Description");
        Field(x => x.PickupLocation.ContactEmail, nullable: true).Description("ContactEmail");
        Field(x => x.PickupLocation.ContactPhone, nullable: true).Description("ContactPhone");
        Field(x => x.PickupLocation.WorkingHours, nullable: true).Description("WorkingHours");
        Field(x => x.PickupLocation.DeliveryDays, nullable: true).Description("Days until ready for pickup");
        Field(x => x.PickupLocation.StorageDays, nullable: true).Description("How long an order will be stored at a pickup point");
        Field(x => x.PickupLocation.GeoLocation, nullable: true).Description("GeoLocation");
        ExtendableField<PickupLocationAddressType>("address", "Address", resolve: context => context.Source.PickupLocation.Address);

        Field<ProductPickupAvailabilityType>("AvailabilityType").Resolve(context => context.Source.AvailabilityType);
        Field(x => x.AvailabilityNote, nullable: true);
        Field(x => x.AvailableQuantity, nullable: true);
    }
}
