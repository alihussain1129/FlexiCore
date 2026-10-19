using FlexiCore.Devices;
using FlexiCore.Licensing;

namespace FlexiCore.Api.Licensing;

/// <summary>Pairing is allowed only while the installed license is Valid or ValidInGrace, the same rule the enforcement middleware uses.</summary>
internal sealed class LicenseDeviceActivationGate(LicenseService licenses) : IDeviceActivationGate
{
    public async Task<bool> IsActivatedAsync(CancellationToken cancellationToken)
    {
        var report = await licenses.GetStatusAsync(cancellationToken);
        return report.State is LicenseState.Valid or LicenseState.ValidInGrace;
    }
}