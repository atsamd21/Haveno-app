using System.Runtime.InteropServices;

namespace Manta.Extensions;

public static class RuntimeInformationExtensions
{
    public static string GetOsArchitectureFullName()
    {
        switch (RuntimeInformation.OSArchitecture)
        {
            case Architecture.X64:
                return "x86_64";
            case Architecture.Arm64:
                return "arm64-v8a";
            case Architecture.Arm:
                return "armeabi-v7a";
            default: throw new NotSupportedException($"Architecture \"{RuntimeInformation.OSArchitecture}\" is not supported.");
        }
    }
}
