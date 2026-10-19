namespace FlexiCore.Devices.Security;

/// <summary>Claim names on the principal a paired device authenticates as. A device is not a user and not proof of which person acted.</summary>
public static class DeviceClaims
{
    public const string ActorKind = "actor_kind";
    public const string ActorKindDevice = "device";
    public const string DeviceId = "device_id";
    public const string DeviceName = "device_name";
    public const string DeviceOwner = "device_owner";
}