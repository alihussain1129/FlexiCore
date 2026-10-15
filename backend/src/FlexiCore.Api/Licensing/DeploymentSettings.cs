using Microsoft.Extensions.Configuration;

namespace FlexiCore.Api.Licensing;

public enum DeploymentMode
{
    Hosted,
    Local,
}

/// <summary>
/// Deployment:Mode is "Hosted" or "Local". When absent or empty it is Hosted in every environment.
/// Any other explicit value stops startup. Local requires License:Directory to be an absolute path.
/// </summary>
public sealed record DeploymentSettings(DeploymentMode Mode, string? LicenseDirectory)
{
    public static DeploymentSettings From(IConfiguration configuration)
    {
        var raw = configuration["Deployment:Mode"];

        DeploymentMode mode;
        if (string.IsNullOrEmpty(raw)) mode = DeploymentMode.Hosted;
        else if (string.Equals(raw, "Hosted", StringComparison.OrdinalIgnoreCase)) mode = DeploymentMode.Hosted;
        else if (string.Equals(raw, "Local", StringComparison.OrdinalIgnoreCase)) mode = DeploymentMode.Local;
        else throw new InvalidOperationException("Deployment:Mode must be 'Hosted' or 'Local' (or unset, which means Hosted).");

        if (mode == DeploymentMode.Hosted) return new DeploymentSettings(mode, null);

        var directory = configuration["License:Directory"];
        if (string.IsNullOrWhiteSpace(directory))
            throw new InvalidOperationException("License:Directory is required when Deployment:Mode is 'Local'.");
        if (!Path.IsPathFullyQualified(directory))
            throw new InvalidOperationException("License:Directory must be an absolute path.");

        return new DeploymentSettings(mode, directory);
    }
}