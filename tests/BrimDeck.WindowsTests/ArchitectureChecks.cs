using System.IO;
using System.Runtime.InteropServices;
using BrimDeck.Core;
using BrimDeck.Sensors;

internal static class ArchitectureChecks
{
    public static void Run(Action<string, bool> check)
    {
        var folder = Path.Combine(Path.GetTempPath(), "BrimDeck-Arm64-cpuid-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var called = 0;
            using var blocked = new CpuReader(folder, Architecture.Arm64, (_, _) =>
            {
                called++;
                throw new InvalidOperationException("CPUID must not run");
            });
            check("the sensor service name and pipe match this fork and not upstream",
                SensorIdentity.ServiceName == ProductIdentity.ServiceName && SensorIdentity.PipeName == ProductIdentity.SensorPipeName
                && SensorIdentity.ServiceName != ProductIdentity.UpstreamServiceName);
            check("an ARM64 process does not execute CPUID and reports the sensor unsupported",
                called == 0 && blocked.Status == SensorStatus.Unsupported && blocked.Read(0).Status == SensorStatus.Unsupported);

            using var probed = new CpuReader(folder, Architecture.X64, (leaf, _) =>
            {
                called++;
                return leaf == 0 ? (0, Four("NotA"), Four("Real"), Four("Vend")) : (0, 0, 0, 0);
            });
            check("an x64 process still consults CPUID and does not treat another vendor as Intel or AMD",
                called > 0 && probed.Status == SensorStatus.Unsupported);
        }
        finally { Directory.Delete(folder, true); }
    }

    private static int Four(string text) => BitConverter.ToInt32(System.Text.Encoding.ASCII.GetBytes(text));
}
