namespace FlexiCore.LicenseTool;

public static class Program
{
    public static int Main(string[] args) =>
        LicenseToolApp.Run(args, new SystemToolConsole(), TimeProvider.System);
}