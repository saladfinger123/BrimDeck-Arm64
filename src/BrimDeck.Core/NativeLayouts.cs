using System.Runtime.InteropServices;

namespace BrimDeck.Core;

// 64-bit layouts the sampler reads by offset. ARM64 and x64 both use 8-byte pointers; the explicit offsets are the
// contract, and Marshal.SizeOf/OffsetOf is the check that the runtime accepts that contract. They are not a claim
// that a particular device's counters are present.
[StructLayout(LayoutKind.Explicit, Size = 1352)]
public struct MibIfRow2Layout
{
    [FieldOffset(0)] public ulong InterfaceLuid;
    [FieldOffset(8)] public uint InterfaceIndex;
    [FieldOffset(1128)] public uint Type;
    [FieldOffset(1152)] public byte Flags;
    [FieldOffset(1208)] public ulong InOctets;
    [FieldOffset(1280)] public ulong OutOctets;
}

// SYSTEM_PROCESS_INFORMATION fields the process list reads. The image and pid are pointer-sized; these offsets are
// the 64-bit layout (8-byte pointers), which is what win-x64 and win-arm64 both are.
[StructLayout(LayoutKind.Explicit, Size = 88)]
public struct SystemProcessInformation64
{
    [FieldOffset(0)] public int NextEntryOffset;
    [FieldOffset(8)] public long WorkingSetSize;
    [FieldOffset(32)] public long CreateTime;
    [FieldOffset(40)] public long UserTime;
    [FieldOffset(48)] public long KernelTime;
    [FieldOffset(56)] public ushort ImageNameLength;
    [FieldOffset(64)] public long ImageNameBuffer;
    [FieldOffset(80)] public long UniqueProcessId;
}

// MEMORYSTATUSEX. Two leading 32-bit fields then 8-byte counters, so the first counter stays at 8 on both 64-bit ABIs.
[StructLayout(LayoutKind.Sequential)]
public struct MemoryStatusExLayout
{
    public uint Length;
    public uint MemoryLoad;
    public ulong TotalPhys;
    public ulong AvailPhys;
    public ulong TotalPageFile;
    public ulong AvailPageFile;
    public ulong TotalVirtual;
    public ulong AvailVirtual;
    public ulong AvailExtendedVirtual;
}

// PERFORMANCE_INFORMATION. SIZE_T is nuint: 8 bytes on win-x64 and win-arm64, with the leading DWORD padded out to 8.
[StructLayout(LayoutKind.Sequential)]
public struct PerformanceInformationLayout
{
    public uint Size;
    public nuint CommitTotal;
    public nuint CommitLimit;
    public nuint CommitPeak;
    public nuint PhysicalTotal;
    public nuint PhysicalAvailable;
    public nuint SystemCache;
    public nuint KernelTotal;
    public nuint KernelPaged;
    public nuint KernelNonpaged;
    public nuint PageSize;
    public uint HandleCount;
    public uint ProcessCount;
    public uint ThreadCount;
}
