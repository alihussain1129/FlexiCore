using System.Globalization;
using System.Security.Cryptography;

namespace FlexiCore.ActivationEngine.Services;

public static class LicenseIdGenerator
{
    public static string Next(DateTimeOffset now) =>
        "FC-" + now.UtcDateTime.ToString("yyyyMMdd", CultureInfo.InvariantCulture)
        + "-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(3));
}
