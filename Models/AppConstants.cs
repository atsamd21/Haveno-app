using System.Reflection;

namespace Manta.Models;

public static class AppConstants
{
    public static readonly string DaemonUrl;
    public static readonly string Network;
    public static readonly string HavenoAppName;

    public const string ApplicationId = "com.haveno";
    public const double MakerFeePct = 0.0015;
    public const double TakerFeePct = 0.0075;
    public const decimal MinimumTradeAmount = 0.05m;

    static AppConstants()
    {
        HavenoAppName = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "HavenoAppName")?.Value ?? string.Empty;

        Network = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "Network")?.Value ?? string.Empty;

        DaemonUrl = Assembly.GetExecutingAssembly()
           .GetCustomAttributes<AssemblyMetadataAttribute>()
           .FirstOrDefault(a => a.Key == "DaemonUrl")?.Value ?? string.Empty;
    }
}
