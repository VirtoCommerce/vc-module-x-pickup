namespace VirtoCommerce.XPickup.Core;

public static class ModuleConstants
{
    public static class Security
    {
        public static class Permissions
        {
            public const string Create = "x-pickup:create";
            public const string Read = "x-pickup:read";
            public const string Update = "x-pickup:update";
            public const string Delete = "x-pickup:delete";

            public static string[] AllPermissions { get; } =
            [
                Create,
                Read,
                Update,
                Delete,
            ];
        }
    }
}
