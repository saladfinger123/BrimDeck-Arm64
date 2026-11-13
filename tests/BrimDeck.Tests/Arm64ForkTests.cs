using System.Net;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using BrimDeck.Core;

internal static class Arm64ForkTests
{
    public static async Task Run(Action<string, bool> check)
    {
        Capabilities(check);
        Layouts(check);
        Identity(check);
        Sampling(check);
        Mailbox(check);
        await Updates(check);
    }

    private static void Capabilities(Action<string, bool> check)
    {
        check("x86 CPUID is limited to x86 and x64 processes",
            ArchitectureCapabilities.AllowsX86CpuId(Architecture.X64) && ArchitectureCapabilities.AllowsX86CpuId(Architecture.X86)
            && !ArchitectureCapabilities.AllowsX86CpuId(Architecture.Arm64) && !ArchitectureCapabilities.AllowsX86CpuId(Architecture.Arm));
        check("Intel MSR and AMD SMN follow the same process gate",
            ArchitectureCapabilities.AllowsIntelMsr(Architecture.X64) && ArchitectureCapabilities.AllowsAmdSmn(Architecture.X64)
            && !ArchitectureCapabilities.AllowsIntelMsr(Architecture.Arm64) && !ArchitectureCapabilities.AllowsAmdSmn(Architecture.Arm64));
        check("the enhanced CPU service is not offered to an ARM64 process",
            ArchitectureCapabilities.AllowsEnhancedCpuService(Architecture.X64) && !ArchitectureCapabilities.AllowsEnhancedCpuService(Architecture.Arm64));
        check("no architecture auto-installs a driver",
            !ArchitectureCapabilities.AllowsAutomaticDriverInstall(Architecture.X64) && !ArchitectureCapabilities.AllowsAutomaticDriverInstall(Architecture.Arm64));
        check("x64 may load the vendor libraries this product already binds, and not nvml.dll",
            ArchitectureCapabilities.VendorNative(Architecture.X64, "nvapi64.dll") == NativeDependencyVerdict.Allowed
            && ArchitectureCapabilities.VendorNative(Architecture.X64, "nvidia-smi.exe") == NativeDependencyVerdict.Allowed
            && ArchitectureCapabilities.VendorNative(Architecture.X64, "atiadlxx.dll") == NativeDependencyVerdict.Allowed
            && ArchitectureCapabilities.VendorNative(Architecture.X64, "ControlLib.dll") == NativeDependencyVerdict.Allowed
            && ArchitectureCapabilities.VendorNative(Architecture.X64, "nvml.dll") == NativeDependencyVerdict.Mismatch);
        check("ARM64 does not load NVIDIA or other vendor binaries that have not been verified",
            ArchitectureCapabilities.VerifiedWindowsArm64VendorFiles.Count == 0
            && ArchitectureCapabilities.VendorNative(Architecture.Arm64, "nvapi64.dll") == NativeDependencyVerdict.Unverified
            && ArchitectureCapabilities.VendorNative(Architecture.Arm64, "nvidia-smi.exe") == NativeDependencyVerdict.Unverified
            && ArchitectureCapabilities.VendorNative(Architecture.Arm64, "nvml.dll") == NativeDependencyVerdict.Unverified
            && ArchitectureCapabilities.VendorNative(Architecture.Arm64, "atiadlxx.dll") == NativeDependencyVerdict.Unverified
            && !ArchitectureCapabilities.AllowsVendorNativeLoad(Architecture.Arm64, "nvapi64.dll"));
        check("runtime ids used by updates are only win-x64 and win-arm64",
            ArchitectureCapabilities.RuntimeId(Architecture.X64) == ArchitectureCapabilities.WinX64
            && ArchitectureCapabilities.RuntimeId(Architecture.Arm64) == ArchitectureCapabilities.WinArm64
            && ArchitectureCapabilities.RuntimeId(Architecture.X86) is null);
        check("the fork channel version is a release, and a beta is not",
            !ApplicationVersion.IsPrerelease("1.4.0-arm64.1") && ApplicationVersion.IsForkRelease("1.4.0-arm64.1")
            && ApplicationVersion.IsPrerelease("1.4.0-beta.1") && !ApplicationVersion.IsForkRelease("1.4.0-beta.1")
            && ApplicationVersion.Compare("1.4.0-arm64.1", "1.4.0-arm64.2") < 0
            && ApplicationVersion.Compare("1.3.1-alpha.1", "1.4.0-arm64.1") < 0);
    }

    private static void Layouts(Action<string, bool> check)
    {
        check("MIB_IF_ROW2 stays 1352 bytes with the sampler's fields at their 64-bit offsets",
            Marshal.SizeOf<MibIfRow2Layout>() == 1352
            && Marshal.OffsetOf<MibIfRow2Layout>(nameof(MibIfRow2Layout.InterfaceLuid)).ToInt32() == 0
            && Marshal.OffsetOf<MibIfRow2Layout>(nameof(MibIfRow2Layout.InterfaceIndex)).ToInt32() == 8
            && Marshal.OffsetOf<MibIfRow2Layout>(nameof(MibIfRow2Layout.Type)).ToInt32() == 1128
            && Marshal.OffsetOf<MibIfRow2Layout>(nameof(MibIfRow2Layout.Flags)).ToInt32() == 1152
            && Marshal.OffsetOf<MibIfRow2Layout>(nameof(MibIfRow2Layout.InOctets)).ToInt32() == 1208
            && Marshal.OffsetOf<MibIfRow2Layout>(nameof(MibIfRow2Layout.OutOctets)).ToInt32() == 1280);
        check("64-bit process information keeps pid, image and times at the offsets the sampler reads",
            Marshal.SizeOf<SystemProcessInformation64>() == 88
            && Marshal.OffsetOf<SystemProcessInformation64>(nameof(SystemProcessInformation64.WorkingSetSize)).ToInt32() == 8
            && Marshal.OffsetOf<SystemProcessInformation64>(nameof(SystemProcessInformation64.CreateTime)).ToInt32() == 32
            && Marshal.OffsetOf<SystemProcessInformation64>(nameof(SystemProcessInformation64.UserTime)).ToInt32() == 40
            && Marshal.OffsetOf<SystemProcessInformation64>(nameof(SystemProcessInformation64.KernelTime)).ToInt32() == 48
            && Marshal.OffsetOf<SystemProcessInformation64>(nameof(SystemProcessInformation64.ImageNameLength)).ToInt32() == 56
            && Marshal.OffsetOf<SystemProcessInformation64>(nameof(SystemProcessInformation64.ImageNameBuffer)).ToInt32() == 64
            && Marshal.OffsetOf<SystemProcessInformation64>(nameof(SystemProcessInformation64.UniqueProcessId)).ToInt32() == 80);
        check("MEMORYSTATUSEX is 64 bytes and the first counter is 8-byte aligned",
            Marshal.SizeOf<MemoryStatusExLayout>() == 64
            && Marshal.OffsetOf<MemoryStatusExLayout>(nameof(MemoryStatusExLayout.TotalPhys)).ToInt32() == 8
            && Marshal.OffsetOf<MemoryStatusExLayout>(nameof(MemoryStatusExLayout.AvailExtendedVirtual)).ToInt32() == 56);
        int performanceSize = Marshal.SizeOf<PerformanceInformationLayout>();
        int pageSizeAt = Marshal.OffsetOf<PerformanceInformationLayout>(nameof(PerformanceInformationLayout.PageSize)).ToInt32();
        int commitAt = Marshal.OffsetOf<PerformanceInformationLayout>(nameof(PerformanceInformationLayout.CommitTotal)).ToInt32();
        check("performance counters use 8-byte SIZE_T and end on an 8-byte boundary",
            nuint.Size == 8 && performanceSize == 104 && commitAt == 8 && pageSizeAt == 80
            && Marshal.OffsetOf<PerformanceInformationLayout>(nameof(PerformanceInformationLayout.HandleCount)).ToInt32() == 88);
    }

    private static void Identity(Action<string, bool> check)
    {
        var local = Path.Combine(Path.GetTempPath(), "local");
        check("fork data lives beside the upstream folder and does not replace it",
            ProductIdentity.DataDirectory(local) == Path.Combine(local, "BrimDeck-Arm64")
            && ProductIdentity.DataFolderName != ProductIdentity.UpstreamDataFolderName
            && ProductIdentity.InstallerAppId != ProductIdentity.UpstreamInstallerAppId
            && ProductIdentity.Repository != ProductIdentity.UpstreamRepository
            && ProductIdentity.ServiceName != ProductIdentity.UpstreamServiceName);
        check("an upgrade never deletes user data, and uninstall deletes it only when asked",
            !ProductIdentity.UninstallRemovesUserData(userAsked: false, upgrade: false)
            && !ProductIdentity.UninstallRemovesUserData(userAsked: true, upgrade: true)
            && !ProductIdentity.UninstallRemovesUserData(userAsked: false, upgrade: true)
            && ProductIdentity.UninstallRemovesUserData(userAsked: true, upgrade: false));
    }

    private static void Sampling(Action<string, bool> check)
    {
        check("a network gap inside the sampling window is a current rate",
            SampleFreshness.Accept(10, 12, SampleFreshness.NetworkMaximumGapSeconds)
            && SampleFreshness.Accept(10, 15, SampleFreshness.NetworkMaximumGapSeconds));
        check("a sleep-length gap is not turned into a current rate",
            !SampleFreshness.Accept(10, 10 + SampleFreshness.NetworkMaximumGapSeconds + 0.01, SampleFreshness.NetworkMaximumGapSeconds)
            && !SampleFreshness.Accept(10, 40, SampleFreshness.NetworkMaximumGapSeconds)
            && !SampleFreshness.Accept(20, 20, SampleFreshness.NetworkMaximumGapSeconds));
        check("two adapters add their byte rates",
            NetworkDelta.Sum([(1_000, 3_000), (5_000, 5_500)], 2) == 1_250);
        check("zero traffic stays zero", NetworkDelta.Sum([(100, 100), (8, 8)], 1) == 0);
        check("a counter that went backwards is unavailable rather than zero or a partial rate",
            NetworkDelta.Sum([(9_000, 1_000), (0, 4_000)], 1) is null && NetworkDelta.Sum([], 1) is null && NetworkDelta.Sum([(0, 10)], 0) is null);
    }

    private static void Mailbox(Action<string, bool> check)
    {
        var box = new LatestWinsMailbox<int>();
        check("the first sample schedules the only delivery", box.Submit(1) && !box.Submit(2) && !box.Submit(3));
        check("the delivery sees the newest sample and not the ones it replaced", box.TryTake(out var taken) && taken == 3 && !box.TryTake(out _));
        check("a sample after the delivery schedules again", box.Submit(4) && box.TryTake(out taken) && taken == 4);

        box.Submit(10);
        box.Submit(11);
        box.Invalidate();
        check("stopping drops a sample that was waiting", !box.TryTake(out _));
        check("a sample after the restart is delivered on its own", box.Submit(12) && box.TryTake(out taken) && taken == 12);

        var raced = new LatestWinsMailbox<string>();
        check("a newer sample submitted while one delivery is pending replaces it", raced.Submit("old") && !raced.Submit("new"));
        raced.Invalidate();
        check("invalidate then a new submit still delivers only the new sample", !raced.Submit("newer") && raced.TryTake(out var text) && text == "newer");
    }

    private static async Task Updates(Action<string, bool> check)
    {
        var setup = RandomNumberGenerator.GetBytes(4096);
        var zip = RandomNumberGenerator.GetBytes(2048);
        string hash = Convert.ToHexString(SHA256.HashData(setup)).ToLowerInvariant();
        string zipHash = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        var arm = Package(ArchitectureCapabilities.WinArm64, "BrimDeck-Arm64-1.4.0-arm64.1-win-arm64-Setup.exe", setup.Length, hash,
            "BrimDeck-Arm64-1.4.0-arm64.1-win-arm64-Portable.zip", zip.Length, zipHash);
        var x64 = Package(ArchitectureCapabilities.WinX64, "BrimDeck-Arm64-1.4.0-arm64.1-win-x64-Setup.exe", setup.Length, hash, null, 0, "");
        var manifest = Fork("1.4.0-arm64.1", arm, [arm, x64]);

        var directory = Path.Combine(Path.GetTempPath(), "BrimDeck-Arm64-update-" + Guid.NewGuid().ToString("N"));
        var server = new Server { Manifest = manifest.ToJson(), Body = setup };
        using var http = new HttpClient(server);
        InstallerUpdateBackend Backend(string architecture, string version = "1.3.1-alpha.1") => new(http, Server.ManifestUri,
            (m, p) => new Uri($"https://updates.test/v{m.Version}/{p.File}"), version, directory, UpdateMode.Installer,
            _ => true, clientArchitecture: architecture);

        var backend = Backend(ArchitectureCapabilities.WinArm64);
        var release = await backend.CheckAsync(CancellationToken.None);
        check("an ARM64 copy is offered the ARM64 package", release == new AppRelease("1.4.0-arm64.1", "Fork notes", setup.Length));
        await backend.DownloadAsync(release!, _ => { }, CancellationToken.None);
        var saved = Path.Combine(directory, arm.File);
        check("the download is the ARM64 installer", File.Exists(saved) && new FileInfo(saved).Length == setup.Length && server.Path.EndsWith(arm.File, StringComparison.Ordinal));

        bool rejected = false;
        try { await Backend(ArchitectureCapabilities.WinX64).CheckAsync(CancellationToken.None); }
        catch (InvalidDataException) { rejected = true; }
        // The x64 package is in the manifest, so this copy is offered its own file rather than rejected.
        var x64Release = await Backend(ArchitectureCapabilities.WinX64).CheckAsync(CancellationToken.None);
        check("an x64 copy is offered the x64 package from the same manifest", !rejected && x64Release?.Size == setup.Length);

        server.Manifest = Fork("1.4.0-arm64.1", x64, [x64]).ToJson();
        rejected = false;
        try { await Backend(ArchitectureCapabilities.WinArm64).CheckAsync(CancellationToken.None); }
        catch (InvalidDataException) { rejected = true; }
        check("an ARM64 copy refuses a manifest that only has an x64 package", rejected);

        foreach (var (name, json) in new[]
        {
            ("upstream repository", Fork("1.4.0-arm64.1", arm, [arm]) with { Repository = ProductIdentity.UpstreamRepository }),
            ("missing product", Fork("1.4.0-arm64.1", arm, [arm]) with { Product = null }),
            ("other product", Fork("1.4.0-arm64.1", arm, [arm]) with { Product = "BrimDeck" }),
            ("file name without its architecture", Fork("1.4.0-arm64.1", arm with { File = "BrimDeck-Setup.exe" }, null) with { Packages = null, Architecture = ArchitectureCapabilities.WinArm64, File = "BrimDeck-Setup.exe" }),
        })
        {
            server.Manifest = json.ToJson();
            rejected = false;
            try { await Backend(ArchitectureCapabilities.WinArm64).CheckAsync(CancellationToken.None); }
            catch (InvalidDataException) { rejected = true; }
            check("manifest with " + name + " is rejected", rejected);
        }

        server.Manifest = Fork("1.4.0-arm64.1", arm, [arm]).ToJson();
        rejected = false;
        try { await Backend("win-x86").CheckAsync(CancellationToken.None); }
        catch (InvalidDataException) { rejected = true; }
        check("an architecture this fork does not publish cannot take the manifest", rejected);
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }

    private static UpdateArchitecturePackage Package(string architecture, string file, long size, string hash, string? portable, long portableSize, string portableHash) =>
        new(architecture, file, size, hash, portable is null ? null : new UpdatePackage(portable, portableSize, portableHash, architecture));

    private static UpdateManifest Fork(string version, UpdateArchitecturePackage primary, IReadOnlyList<UpdateArchitecturePackage>? packages) =>
        new(version, "Fork notes", primary.File, primary.Size, primary.Sha256, primary.Portable, ProductIdentity.ProductId, ProductIdentity.Repository, primary.Architecture, packages);

    private sealed class Server : HttpMessageHandler
    {
        public static readonly Uri ManifestUri = new("https://updates.test/latest/update.json");
        public string Manifest { get; set; } = "";
        public byte[] Body { get; set; } = [];
        public string Path { get; private set; } = "";
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Path = request.RequestUri?.AbsolutePath ?? "";
            HttpContent content = request.RequestUri == ManifestUri ? new StringContent(Manifest) : new ByteArrayContent(Body);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = content });
        }
    }
}
