using Microsoft.Extensions.Configuration;

namespace FlexiCore.Identity.Settings;

public sealed class IdentitySettings
{
    public const int MinPbkdf2Iterations = 100_000;
    public const int DefaultPbkdf2Iterations = 210_000;

    public int Pbkdf2Iterations { get; init; } = DefaultPbkdf2Iterations;
    public bool RegistrationEnabled { get; init; }

    /// <summary>Registration defaults to enabled in Development only; any other environment must opt in explicitly.</summary>
    public static IdentitySettings Load(IConfiguration configuration, bool isDevelopment)
    {
        var iterations = configuration.GetValue("Identity:Pbkdf2Iterations", DefaultPbkdf2Iterations);
        if (iterations < MinPbkdf2Iterations)
            throw new InvalidOperationException($"Identity:Pbkdf2Iterations must be at least {MinPbkdf2Iterations}.");

        var enabled = configuration.GetValue<bool?>("Registration:Enabled") ?? isDevelopment;
        return new IdentitySettings { Pbkdf2Iterations = iterations, RegistrationEnabled = enabled };
    }
}