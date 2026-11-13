using System.Runtime.InteropServices;

namespace BrimDeck.Core;

public enum NativeDependencyVerdict
{
    // The process architecture matches a user-mode binary this product already loads on that architecture.
    Allowed,
    // No Windows ARM64 PE of this file has been audited. Do not load it and do not report a zero reading.
    Unverified,
    // The file is not a dependency this process is allowed to load.
    Mismatch
}

// What this process may call. x86 CPUID, Intel MSRs and AMD SMN are properties of those processors, not of an
// NVIDIA-branded machine and not of an ARM64 process. A vendor DLL is loaded only when its Windows architecture
// is one this build already ships against, or when a Windows ARM64 file has been added to the verified set.
public static class ArchitectureCapabilities
{
    public const string WinX64 = "win-x64";
    public const string WinArm64 = "win-arm64";
    public const string VendorUnverifiedReason = "Windows ARM64 vendor library is unverified";

    // Official Windows ARM64 builds of nvapi64.dll, nvidia-smi.exe and nvml.dll are not in this repository.
    // A Linux aarch64 package, an SDK header or the NVIDIA name is not an entry. Leave this empty until a
    // signed Windows ARM64 PE is audited (machine type, imports and the driver it needs).
    public static readonly IReadOnlySet<string> VerifiedWindowsArm64VendorFiles =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // Files the x64 build already loads from the driver install. nvml.dll is not one of them: nvidia-smi uses
    // NVML inside its own process, and this product does not bind nvml.dll.
    private static readonly HashSet<string> X64VendorFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "nvapi64.dll", "atiadlxx.dll", "ControlLib.dll", "nvidia-smi.exe"
    };

    public static string? RuntimeId(Architecture architecture) => architecture switch
    {
        Architecture.X64 => WinX64,
        Architecture.Arm64 => WinArm64,
        _ => null
    };

    public static bool IsSupportedRuntime(string? runtimeId) =>
        runtimeId is WinX64 or WinArm64;

    public static bool AllowsX86CpuId(Architecture architecture) =>
        architecture is Architecture.X86 or Architecture.X64;

    public static bool AllowsIntelMsr(Architecture architecture) => AllowsX86CpuId(architecture);

    public static bool AllowsAmdSmn(Architecture architecture) => AllowsX86CpuId(architecture);

    // The sensor service and PawnIO modules read x86 registers. An ARM64 process must not install or start them.
    // PawnIO's source tree containing an ARM64 macro is not a signed driver and not a module for this CPU.
    public static bool AllowsEnhancedCpuService(Architecture architecture) => AllowsX86CpuId(architecture);

    public static bool AllowsAutomaticDriverInstall(Architecture architecture) => false;

    public static NativeDependencyVerdict VendorNative(Architecture process, string fileName)
    {
        if (process == Architecture.X64 && X64VendorFiles.Contains(fileName)) return NativeDependencyVerdict.Allowed;
        if (process == Architecture.Arm64 && VerifiedWindowsArm64VendorFiles.Contains(fileName)) return NativeDependencyVerdict.Allowed;
        if (process == Architecture.Arm64) return NativeDependencyVerdict.Unverified;
        return NativeDependencyVerdict.Mismatch;
    }

    public static bool AllowsVendorNativeLoad(Architecture process, string fileName) =>
        VendorNative(process, fileName) == NativeDependencyVerdict.Allowed;
}
