using System.Collections.Generic;
using VirtoCommerce.Platform.Core.Settings;

namespace VirtoCommerce.XPickup.Core;

public static class ModuleConstants
{
    public static class Settings
    {
        public static SettingDescriptor Enabled { get; } = new SettingDescriptor
        {
            Name = "XPickup.Enabled",
            GroupName = "BOPIS",
            ValueType = SettingValueType.Boolean,
            IsPublic = true
        };

        public static SettingDescriptor TodayAvailabilityNote { get; } = new()
        {
            Name = "XPickup.TodayAvailabilityNote",
            GroupName = "BOPIS",
            ValueType = SettingValueType.ShortText,
            IsLocalizable = true,
            IsDictionary = true,
            IsPublic = true,
        };

        public static SettingDescriptor TransferAvailabilityNote { get; } = new()
        {
            Name = "XPickup.TransferAvailabilityNote",
            GroupName = "BOPIS",
            ValueType = SettingValueType.ShortText,
            IsLocalizable = true,
            IsDictionary = true,
            IsPublic = true,
        };

        public static SettingDescriptor GlobalTransferAvailabilityNote { get; } = new()
        {
            Name = "XPickup.GlobalTransferAvailabilityNote",
            GroupName = "BOPIS",
            ValueType = SettingValueType.ShortText,
            IsLocalizable = true,
            IsDictionary = true,
            IsPublic = true,
        };

        public static SettingDescriptor GlobalTransferEnabled { get; } = new()
        {
            Name = "XPickup.GlobalTransferEnabled",
            GroupName = "BOPIS",
            ValueType = SettingValueType.Boolean,
            DefaultValue = false,
            IsPublic = true,
        };

        public static IEnumerable<SettingDescriptor> PickupLocationSettings
        {
            get
            {
                yield return Enabled;
                yield return TodayAvailabilityNote;
                yield return TransferAvailabilityNote;
                yield return GlobalTransferAvailabilityNote;
                yield return GlobalTransferEnabled;
            }
        }
    }
}
