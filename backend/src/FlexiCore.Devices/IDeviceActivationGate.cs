namespace FlexiCore.Devices;

/// <summary>Lets the host say whether this installation is currently activated, so Devices need not depend on Licensing.</summary>
public interface IDeviceActivationGate
{
    Task<bool> IsActivatedAsync(CancellationToken cancellationToken);
}